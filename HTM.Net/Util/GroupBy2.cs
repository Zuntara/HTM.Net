﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using MathNet.Numerics.LinearAlgebra.Solvers;

namespace HTM.Net.Util
{
    /**
     * An extension to groupby in Python's itertools. Allows to walk across n sorted lists
     * with respect to their key functions and yields a {@link Tuple} of n lists of the
     * members of the next *smallest* group.
     * 
     * @author cogmission
     * @param <R>   The return type of the user-provided {@link Function}s
     */
    public class GroupBy2<R> : Generator<Tuple>
        where R : IComparable<R>
    {
        /** serial version */
        private const long serialVersionUID = 1L;

        /** stores the user inputted pairs */
        private Tuple<List<object>, Func<object, R>>[] entries;

        /** stores the {@link GroupBy} {@link Generator}s created from the supplied lists */
        private List<GroupBy<object, R>> generatorList;

        /** the current interation's minimum key value */
        private R minKeyVal;

        ///////////////////////
        //    Control Lists  //
        ///////////////////////
        private bool[] advanceList;
        private Slot<Tuple<object, R>>[] nextList;

        private int numEntries;

        /**
         * Private internally used constructor. To instantiate objects of this
         * class, please see the static factory method {@link #of(Pair...)}
         * 
         * @param entries   a {@link Pair} of lists and their key-producing functions
         */
        private GroupBy2(Tuple<List<object>, Func<object, R>>[] entries)
        {
            this.entries = entries;
        }

        /**
         * <p>
         * Returns a {@code GroupBy2} instance which is used to group lists of objects
         * in ascending order using keys supplied by their associated {@link Function}s.
         * </p><p>
         * <b>Here is an example of the usage and output of this object: (Taken from {@link GroupBy2Test})</b>
         * </p>
         * <pre>
         *  List<Integer> sequence0 = Arrays.asList(new Integer[] { 7, 12, 16 });
         *  List<Integer> sequence1 = Arrays.asList(new Integer[] { 3, 4, 5 });
         *  
         *  Function<Integer, Integer> identity = Function.identity();
         *  Function<Integer, Integer> times3 = x -> x * 3;
         *  
         *  @SuppressWarnings({ "unchecked", "rawtypes" })
         *  GroupBy2<Integer> groupby2 = GroupBy2.of(
         *      new Pair(sequence0, identity), 
         *      new Pair(sequence1, times3));
         *  
         *  for(Tuple tuple : groupby2) {
         *      System.out.println(tuple);
         *  }
         * </pre>
         * <br>
         * <b>Will output the following {@link Tuple}s:</b>
         * <pre>
         *  '7':'[7]':'[NONE]'
         *  '9':'[NONE]':'[3]'
         *  '12':'[12]':'[4]'
         *  '15':'[NONE]':'[5]'
         *  '16':'[16]':'[NONE]'
         *  
         *  From row 1 of the output:
         *  Where '7' == Tuple.get(0), 'List[7]' == Tuple.get(1), 'List[NONE]' == Tuple.get(2) == empty list with no members
         * </pre>
         * 
         * <b>Note: Read up on groupby here:</b><br>
         *   https://docs.python.org/dev/library/itertools.html#itertools.groupby
         * <p> 
         * @param entries
         * @return  a n + 1 dimensional tuple, where the first element is the
         *          key of the group and the other n entries are lists of
         *          objects that are a member of the current group that is being
         *          iterated over in the nth list passed in. Note that this
         *          is a generator and a n+1 dimensional tuple is yielded for
         *          every group. If a list has no members in the current
         *          group, {@link Slot#NONE} is returned in place of a generator.
         */
        public static GroupBy2<R> Of(params Tuple<List<object>, Func<object, R>>[] entries)
        {
            return new GroupBy2<R>(entries);
        }

        public override void Reset()
        {
            generatorList = new List<GroupBy<object, R>>();

            for (int i = 0; i < entries.Length; i++)
            {
                generatorList.Add(GroupBy<object, R>.Of(entries[i].Item1, entries[i].Item2));
            }

            numEntries = generatorList.Count;

            advanceList = new bool[numEntries];
            Arrays.Fill(advanceList, true);
            nextList = new Slot<Tuple<object, R>>[numEntries];
            Arrays.Fill(nextList, Slot<Tuple<object, R>>.NONE);
        }

        public override bool MoveNext()
        {
            if (HasNextInternal())
            {
                Current = NextInternal();
                return true;
            }
            return false;
        }

        private bool HasNextInternal()
        {
            if (generatorList == null)
            {
                Reset();
            }
            AdvanceSequences();
            return NextMinKey();
        }

        private Tuple NextInternal()
        {
            object[] objs = ArrayUtils.Range(0, numEntries + 1)
                .Select(i => i == 0 ? minKeyVal as object : new List<object>() as object)
                .ToArray();

            Tuple retVal = new Tuple(objs);

            for (int i = 0; i < numEntries; i++)
            {
                if (IsEligibleList(i, minKeyVal))
                {
                    ((List<object>)retVal.Get(i + 1)).Add(nextList[i].Get().Item1);
                    DrainKey(retVal, i, minKeyVal);
                    advanceList[i] = true;
                }
                else
                {
                    advanceList[i] = false;
                    ((List<object>)retVal.Get(i + 1)).Add(Slot<Tuple<object, R>>.Empty());
                }
            }

            return retVal;
        }

        /**
         * Internal method which advances index of the current
         * {@link GroupBy}s for each group present.
         */
        private void AdvanceSequences()
        {
            for (int i = 0; i < numEntries; i++)
            {
                if (advanceList[i])
                {
                    nextList[i] = generatorList[i].MoveNext() ?
                        Slot<Tuple<object, R>>.Of(generatorList[i].Current) : Slot<Tuple<object, R>>.Empty();
                }
            }
        }

        /**
         * Returns the next smallest generated key.
         * 
         * @return  the next smallest generated key.
         */
        private bool NextMinKey()
        {
            var nl = nextList
                .Where(opt => opt.IsPresent())
                .Select(opt => new { slot = opt, value = opt.Get().Item2 })
                .ToList();

            nl.Sort((k, k2) => k.value.CompareTo(k2.value));

            var first = nl.FirstOrDefault();
            if (first != null)
            {
                minKeyVal = first.value;
                return first.slot.IsPresent();
            }
            return false;
        }

        /**
         * Returns a flag indicating whether the list currently pointed
         * to by the specified index contains a key which matches the
         * specified "targetKey".
         * 
         * @param listIdx       the index pointing to the {@link GroupBy} being
         *                      processed.
         * @param targetKey     the specified key to match.
         * @return  true if so, false if not
         */
        private bool IsEligibleList(int listIdx, object targetKey)
        {
            return nextList[listIdx].IsPresent() && nextList[listIdx].Get().Item2.Equals(targetKey);
        }

        /**
         * Each input grouper may generate multiple members which match the
         * specified "targetVal". This method guarantees that all members 
         * are added to the list residing at the specified Tuple index.
         * 
         * @param retVal        the Tuple being added to
         * @param listIdx       the index specifying the list within the 
         *                      tuple which will have members added to it
         * @param targetVal     the value to match in order to be an added member
         */
        private void DrainKey(Tuple retVal, int listIdx, R targetVal)
        {
            while (generatorList[listIdx].HasNextInternal())
            {
                var peeked = generatorList[listIdx].Peek();
                if (peeked?.Item2.Equals(targetVal) == true)
                {
                    nextList[listIdx] = Slot<Tuple<object, R>>.Of(generatorList[listIdx].NextInternal());
                    ((IList)retVal.Get(listIdx + 1)).Add(nextList[listIdx].Get().Item1);
                }
                else
                {
                    nextList[listIdx] = Slot<Tuple<object, R>>.Empty();
                    break;
                }
            }
        }

        public sealed class Slot<T>
            where T : class
        {
            /**
             * Common instance for {@code empty()}.
             */
            public static Slot<T> NONE = new Slot<T>();

            private readonly T value;

            private Slot() { this.value = null; }

            private Slot(T value)
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                this.value = value;
            }

            public static Slot<T> Of(T value)
            {
                return new Slot<T>(value);
            }

            /**
             * Returns an {@code Slot} describing the specified value, if non-null,
             * otherwise returns an empty {@code Slot}.
             *
             * @param <T> the class of the value
             * @param value the possibly-null value to describe
             * @return an {@code Slot} with a present value if the specified value
             * is non-null, otherwise an empty {@code Slot}
             */
            public static Slot<T> OfNullable(T value)
            {
                return value == null ? (Slot<T>)NONE : Of(value);
            }

            /**
             * If a value is present in this {@code Slot}, returns the value,
             * otherwise throws {@code NoSuchElementException}.
             *
             * @return the non-null value held by this {@code Slot}
             * @throws NoSuchElementException if there is no value present
             *
             * @see Slot#isPresent()
             */
            public T Get()
            {
                if (value == null)
                {
                    throw new IndexOutOfRangeException("No value present");
                }
                return value;
            }

            /**
             * Returns an empty {@code Slot} instance.  No value is present for this
             * Slot.
             *
             * @param <T> Type of the non-existent value
             * @return an empty {@code Slot}
             */
            public static Slot<T> Empty()
            {
                Slot<T> t = (Slot<T>)NONE;
                return t;
            }

            /**
             * Return {@code true} if there is a value present, otherwise {@code false}.
             *
             * @return {@code true} if there is a value present, otherwise {@code false}
             */
            public bool IsPresent()
            {
                return value != null;
            }

            #region Overrides of Object

            public override bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (!(obj is Slot<T>))
                {
                    return false;
                }

                Slot<T> other = (Slot<T>)obj;
                return value.Equals(other.value);
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }


            public override string ToString()
            {
                return value != null ? $"Slot[{value}]" : $"NONE (T = {typeof(T).Name})";
            }

            #endregion
        }
    }
}