using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    public interface ICoordinateOrder
    {
        /**
         * Returns the order for a coordinate.
         * 
         * @param coordinate	coordinate array
         * 
         * @return	A value in the interval [0, 1), representing the
         *          order of the coordinate
         */
        double OrderForCoordinate(int[] coordinate);
    }

    public class CoordinateEncoder : Encoder<Util.Tuple>, ICoordinateOrder
    {
        private static IRandom random = new MersenneTwister(42);

        /**
         * Package private to encourage construction using the Builder Pattern
         * but still allow inheritance.
         */
        internal CoordinateEncoder()
        {
            /*
            *description has a {@link List} of {@link Tuple}s containing
            */
            Tuple desc = new Tuple("coordinate", 0);
            Tuple desc2 = new Tuple("radius", 1);
            description.Add(desc);
            description.Add(desc2);
        }

        /**
         * @see Encoder for more information
         */
        public override int GetWidth()
        {
            return n;
        }

        /**
         * @see Encoder for more information
         */
        public override bool IsDelta()
        {
            return false;
        }


        /**
         * Returns a builder for building ScalarEncoders.
         * This builder may be reused to produce multiple builders
         *
         * @return a {@code CoordinateEncoder.Builder}
         */
        public static IBuilder GetBuilder()
        {
            return new Builder();
        }

        /**
         * Returns coordinates around given coordinate, within given radius.
         * Includes given coordinate.
         *
         * @param coordinate	Coordinate whose neighbors to find
         * @param radius		Radius around `coordinate`
         * @return
         */
        public List<int[]> Neighbors(int[] coordinate, double radiusAroundCoordinate)
        {
            int[][] ranges = new int[coordinate.Length][];
            for (int i = 0; i < coordinate.Length; i++)
            {
                ranges[i] = ArrayUtils.Range(coordinate[i] - (int)radiusAroundCoordinate, coordinate[i] + (int)radiusAroundCoordinate + 1);
            }

            List<int[]> retVal = new List<int[]>();
            int len = ranges.Length == 1 ? 1 : ranges[0].Length;
            for (int k = 0; k < ranges[0].Length; k++)
            {
                for (int j = 0; j < len; j++)
                {
                    int[] entry = new int[ranges.Length];
                    entry[0] = ranges[0][k];
                    for (int i = 1; i < ranges.Length; i++)
                    {
                        entry[i] = ranges[i][j];
                    }
                    retVal.Add(entry);
                }
            }
            return retVal;
        }

        /**
         * Returns the top W coordinates by order.
         *
         * @param co			Implementation of {@link CoordinateOrder}
         * @param coordinates	A 2D array, where each element
                                is a coordinate
         * @param w				(int) Number of top coordinates to return
         * @return
         */
        public int[][] TopWCoordinates(ICoordinateOrder co, int[][] coordinates, int numberOfTopCoordinates)
        {
            KeyValuePair<double, int>[] pairs = new KeyValuePair<double, int>[coordinates.Length];
            for (int i = 0; i < coordinates.Length; i++)
            {
                pairs[i] = new KeyValuePair<double, int>(co.OrderForCoordinate(coordinates[i]), i);
            }

            pairs = pairs.OrderBy(kvp => kvp.Key).ThenBy(kvp => kvp.Value).ToArray();
            //Array.Sort(pairs);

            int[][] topCoordinates = new int[numberOfTopCoordinates][];
            for (int i = 0, wIdx = pairs.Length - numberOfTopCoordinates; i < numberOfTopCoordinates; i++, wIdx++)
            {
                int index = pairs[wIdx].Value;
                topCoordinates[i] = coordinates[index];
            }
            return topCoordinates;
        }

        private Dictionary<string, double> _coordOrder = new Dictionary<string, double>();
        private Dictionary<string, int> _bitOrder = new Dictionary<string, int>();
        private int _lastBitOrderN = 0;

        /**
         * Returns the order for a coordinate.
         *
         * @param coordinate	coordinate array
         *
         * @return	A value in the interval [0, 1), representing the
         *          order of the coordinate
         */
        public double OrderForCoordinate(int[] coordinate)
        {
            // TODO: make the random more predictable with seeds
            string coordinateString = Arrays.ToString(coordinate);
            if (_coordOrder.ContainsKey(coordinateString))
            {
                return _coordOrder[coordinateString];
            }

            //random.SetSeed(coordinate);
            double order = random.NextDouble();
            _coordOrder.Add(coordinateString, order);
            return order;
        }

        /**
         * Returns the order for a coordinate.
         *
         * @param coordinate	coordinate array
         * @param n				the number of available bits in the SDR
         *
         * @return	The index to a bit in the SDR
         */
        public int BitForCoordinate(int[] coordinate, int n)
        {
            // TODO: make the random more predictable with seeds

            if (_lastBitOrderN != n)
            {
                // Reset our cache
                _bitOrder = new Dictionary<string, int>();
                _lastBitOrderN = n;
            }

            string coordinateString = Arrays.ToString(coordinate);
            if (_bitOrder.ContainsKey(coordinateString))
            {
                int bit = _bitOrder[coordinateString];
                return bit;
            }

            //random.SetSeed(coordinate);
            int order = random.NextInt(n);
            _bitOrder.Add(coordinateString, order);
            return order;
            //random.SetSeed(coordinate);
            //return random.NextInt(n);
        }

        /**
         * {@inheritDoc}
         */
        public override void EncodeIntoArray(Tuple inputData, int[] output)
        {
            List<int[]> neighs = Neighbors((int[])inputData.Get(0), (double)inputData.Get(1));
            int[][] neighbors = new int[neighs.Count][];
            for (int i = 0; i < neighs.Count; i++) neighbors[i] = neighs[i];

            int[][] winners = TopWCoordinates(this, neighbors, w);

            for (int i = 0; i < winners.Length; i++)
            {
                int bit = BitForCoordinate(winners[i], n);
                output[bit] = 1;
            }
        }

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            EncodeIntoArray((Tuple)o, tempArray);
        }

        public override List<T> GetBucketValues<T>(Type returnType)
        {
            return null;
        }

        internal static void ResetRandomGenerator()
        {
            random = new MersenneTwister(42);
        }

        /**
         * Returns a {@code Builder} for constructing {@link CoordinateEncoder}s
         *
         * The base class architecture is put together in such a way where boilerplate
         * initialization can be kept to a minimum for implementing subclasses, while avoiding
         * the mistake-proneness of extremely long argument lists.
         *
         * @see ScalarEncoder.Builder#setStuff(int)
         */
        public class Builder : BuilderBase
        {

            internal Builder() { }

            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize
                //boilerplate variables.
                encoder = new CoordinateEncoder();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////

                if (w <= 0 || w % 2 == 0)
                {
                    throw new ArgumentException("w must be odd, and must be a positive integer");
                }

                if (n <= 6 * w)
                {
                    throw new ArgumentException(
                        "n must be an int strictly greater than 6*w. For " +
                           "good results we recommend n be strictly greater than 11*w");
                }

                if (name == null || name.Equals("None"))
                {
                    name = new StringBuilder("[").Append(n).Append(":").Append(w).Append("]").ToString();
                }

                var enc = (CoordinateEncoder)encoder;
                // Reset our existing mappings
                enc._bitOrder = new Dictionary<string, int>();
                enc._coordOrder = new Dictionary<string, double>();
                return enc;
            }
        }
    }
}
