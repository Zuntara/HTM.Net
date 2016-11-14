using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Network.Sensor
{
    /**
 * <p>
 * Specialized {@link Stream} for CSV (Comma Separated Values)
 * stream processing. Configure this Stream with a batch size and
 * a header length, and just treat as normal {@link Stream}.
 * </p>
 * <p>
 * To create a {@code BatchedCsvStream}, call {@link BatchedCsvStream#batch(Stream, int, boolean, int)}
 * handing it the underlying Stream to handle, the batch size, whether it should be parallelized,
 * and the size of the header and it will return a Stream that will handle 
 * batching when "isParallel" is set to true. When "isParallel" is set to false, no batching
 * takes place because there would be no point.
 * </p>
 * <p>
 * A side effect to be aware of when batching is the insertion of a "sequenceNumber" to the first column
 * of every line. This sequenceNumber describes the "encounter order" of the line in question
 * and can reliably be used to "re-order" the entire stream at a later point.
 * </p>
 * 
 * <p>
 * <pre>
 * To reorder the Stream use code such as:
 *      Stream thisStream;
 *      List&lt;String&gt; sortedList = thisStream.sorted(
 *          (String[] i, String[] j) -&gt; {
 *              return Integer.valueOf(i[0]).compareTo(Integer.valueOf(j[0]));
 *          }).collect(Collectors.toList());
 * </pre>
 *
 * 
 * The batching implemented is pretty straight forward. The underlying iterator is
 * advanced to i + min(batchSize, remainingCount), where each line is fed into
 * a queue of Objects, the {@link BatchSpliterator#tryAdvance(Consumer)}
 * is called with a {@link BatchSpliterator.SequencingConsumer} which inserts
 * the sequenceNumber into the head of the line array after calling 
 * {@link System#arraycopy(Object, int, Object, int, int)} to increase its size.
 * 
 *
 * @param <T> The Type of data on each line of this Stream (String[] for this implementation)
 */
    //public class BatchedCsvStream2<T> : IMetaStream<string[]>
    //{
    //    //    // TOP TWO CLASSES ARE THE BatchSpliterator AND THE BatchedCsvHeader //
    //    //    // See main() at bottom for localized mini-test

    //    //    //////////////////////////////////////////////////////////////
    //    //    //                      Inner Classes                       //
    //    //    //////////////////////////////////////////////////////////////
    //    //    /**
    //    //     * The internal batching {@link Spliterator} implementation.
    //    //     * This does all the magic of splitting the stream into "jobs"
    //    //     * that each cpu core can handle.
    //    //     * 
    //    //     * @author David Ray
    //    //     * @see Header
    //    //     * @see BatchedCsvStream
    //    //     */
    //    //    private class BatchSpliterator : Spliterator<string[]>
    //    //    {
    //    //        private readonly int batchSize;
    //    //        private readonly int characteristics;
    //    //        private int sequenceNum;
    //    //        private long est;
    //    //        private BatchedCsvStream<string[]> csv;
    //    //        private Spliterator<string[]> spliterator;


    //    //        /**
    //    //         * Creates a new BatchSpliterator
    //    //         * 
    //    //         * @param characteristics   the bit flags indicating the different
    //    //         *                          {@link Spliterator} configurations
    //    //         * @param batchSize         the size of each "chunk" to hand off to
    //    //         *                          a Thread
    //    //         * @param est               estimation-only, of the remaining size
    //    //         */
    //    //        public BatchSpliterator(int characteristics, int batchSize, long est)
    //    //        {
    //    //            this.characteristics = characteristics | Spliterator.SUBSIZED;
    //    //            this.batchSize = batchSize;
    //    //            this.est = est;
    //    //        }

    //    //        /**
    //    //         * Called internally to store the reference to the parent {@link BatchedCsvStream}.
    //    //         * 
    //    //         * @param csv       the parent {@code BatchedCsvStream}
    //    //         * @return          this {@code BatchSpliterator}
    //    //         */
    //    //        private BatchSpliterator SetCSV(BatchedCsvStream<string[]> csv)
    //    //        {
    //    //            this.csv = csv;
    //    //            return this;
    //    //        }

    //    //        /**
    //    //         * Called internally to store a reference to the functional {@link Spliterator}
    //    //         * @param toWrap
    //    //         * @return
    //    //         */
    //    //        private BatchSpliterator SetToWrap(Spliterator<string[]> toWrap)
    //    //        {
    //    //            this.spliterator = toWrap;
    //    //            return this;
    //    //        }

    //    //        /**
    //    //         * Overridden to call the @delegate {@link Spliterator} and update
    //    //         * this Spliterator's sequence number.
    //    //         * 
    //    //         * @return a flag indicating whether there is a value available
    //    //         */
    //    //        public bool TryAdvance<T>(Consumer<T> action)
    //    //            where T : IEnumerable
    //    //        {
    //    //            bool hasNext;
    //    //            if (hasNext = spliterator.TryAdvance(action))
    //    //            {
    //    //                sequenceNum++;
    //    //            }
    //    //            return hasNext;
    //    //        }

    //    //        /**
    //    //         * Little cousin to {@link #tryAdvance(Consumer)} which is called 
    //    //         * after the spliterator is depleted to see if there are any remaining
    //    //         * values.
    //    //         */
    //    //        public void ForEachRemaining(Consumer<? super string[]> action)
    //    //        {
    //    //            spliterator.forEachRemaining(action);
    //    //        }

    //    //        /**
    //    //         * Called by the Fork/Join mechanism to divide and conquer by 
    //    //         * creating {@link Spliterator}s for each thread. This method
    //    //         * returns a viable Spliterator over the configured number of
    //    //         * lines. see {@link #batchSize}
    //    //         */
    //    //        public Spliterator<string[]> TrySplit()
    //    //        {
    //    //            SequencingConsumer holder = csv.isArrayType ? new SequencingArrayConsumer() : new SequencingConsumer();

    //    //            //This is the line that makes this implementation tricky due to
    //    //            //a side effect in the purpose of this method. The try advance
    //    //            //actually advances so when it is called twice, (because it is
    //    //            //used to query if there is a "next" also) we need to handle it
    //    //            //for the first and last sequence. We also have to make sure our
    //    //            //sequence number is being handled so that we can "re-order" the
    //    //            //parallel pieces later. (They're inserted at the row-heads of each
    //    //            //line).
    //    //            if (!TryAdvance(holder))
    //    //            {
    //    //                return null;
    //    //            }

    //    //            csv.SetBatchOp(true);

    //    //            object[] lines = new object[batchSize];
    //    //            int j = 0;
    //    //            do
    //    //            {
    //    //                lines[j] = holder.Value;
    //    //            } while (++j < batchSize && TryAdvance(holder));

    //    //            if (est != long.MaxValue) est -= j;
    //    //            return Spliterators.spliterator(lines, 0, j, characteristics | SIZED);
    //    //        }

    //    //        /**
    //    //         * Returns a specialized {@link Comparator} if the characteristics are set
    //    //         * to {@link Spliterator#SORTED} and a call to {@link 
    //    //         * @return
    //    //         */
    //    //        public Comparator<T> getComparator<T>()
    //    //            where T : IEnumerable
    //    //        {
    //    //            if (HasCharacteristics(Spliterator.SORTED) && csv.isBatchOp)
    //    //            {
    //    //                return (i, j) -> { return Long.valueOf(i[0]).compareTo(Long.valueOf(j[0])); };
    //    //            }
    //    //            else if (csv.isBatchOp)
    //    //            {
    //    //                return null;
    //    //            }
    //    //            throw new IllegalStateException();
    //    //        }

    //    //        public long EstimateSize()
    //    //        {
    //    //            return est;
    //    //        }

    //    //        public int Characteristics()
    //    //        {
    //    //            return characteristics;
    //    //        }

    //    //        class SequencingConsumer : Consumer<string[]>
    //    //        {
    //    //            string[] value;
    //    //            public void accept(string[] value)
    //    //            {
    //    //                csv.isTerminal = true;
    //    //                this.value = new string[value.Length + 1];
    //    //                Array.Copy(value, 0, this.value, 1, value.Length);
    //    //                this.value[0] = sequenceNum;
    //    //            }
    //    //        }

    //    //        private class SequencingArrayConsumer : SequencingConsumer, Consumer<string[]>
    //    //        {
    //    //            string[] value;
    //    //            public void accept(string[] value)
    //    //            {
    //    //                csv.isTerminal = true;
    //    //                this.value = new string[2];
    //    //                this.value[0] = string.valueOf(sequenceNum);
    //    //                this.value[1] = Arrays.toString(value).trim();
    //    //            }
    //    //        }
    //    //    }

    //    /**
    //     * Implementation of the @FunctionalInterface {@link Header}
    //     * 
    //     * @author David Ray
    //     * @see Header
    //     */
    //    public class BatchedCsvHeader<T> : IValueList
    //    {
    //        /** Container for the field values */
    //        private readonly Tuple[] _headerValues;

    //        /**
    //         * Constructs a new {@code BatchedCsvHeader}
    //         * 
    //         * @param lines                     List of csv strings
    //         * @param configuredHeaderLength    number of header rows
    //         */
    //        public BatchedCsvHeader(List<T> lines, int configuredHeaderLength)
    //        {

    //            if ((configuredHeaderLength < 1 || lines == null || lines.Count < 1) ||
    //                (configuredHeaderLength > 1 && lines.Count != configuredHeaderLength))
    //            {

    //                throw new InvalidOperationException("Actual Header was not the expected size: " +
    //                    (configuredHeaderLength < 1 ? "> 1" : configuredHeaderLength.ToString()) +
    //                        ", but was: " + (lines == null ? "null" : lines.Count.ToString()));
    //            }

    //            _headerValues = new Tuple[configuredHeaderLength];
    //            for (int i = 0; i < _headerValues.Length; i++)
    //            {
    //                if (lines[i] is Array)
    //                {
    //                    _headerValues[i] = new Tuple((Array)((object)lines[i]));
    //                }
    //                else
    //                {
    //                    _headerValues[i] = new Tuple(lines[i]);
    //                }
    //            }
    //        }

    //        public Tuple[] GetHeaderValues()
    //        {
    //            return _headerValues;
    //        }

    //        /**
    //         * Returns the array of values ({@link Tuple}) at the specified
    //         * index.
    //         * 
    //         * @param index     the index of the Tuple to be retrieved.
    //         * @return
    //         */
    //        public Tuple GetRow(int index)
    //        {
    //            if (index >= _headerValues.Length)
    //            {
    //                return null;
    //            }
    //            return _headerValues[index];
    //        }

    //        /**
    //         * Returns the current number of lines in the header.
    //         * 
    //         * @return
    //         */
    //        public int Size()
    //        {
    //            return _headerValues == null ? 0 : _headerValues.Length;
    //        }

    //        public bool IsLearn()
    //        {
    //            throw new NotImplementedException();
    //        }

    //        public bool IsReset()
    //        {
    //            throw new NotImplementedException();
    //        }

    //        public List<FieldMetaType> GetFieldTypes()
    //        {
    //            throw new NotImplementedException();
    //        }

    //        /**
    //         * {@inheritDoc}
    //         * @return
    //         */
    //        public override string ToString()
    //        {
    //            StringBuilder sb = new StringBuilder();
    //            _headerValues.ToList().ForEach(l => sb.Append(l).Append("\n"));
    //            return sb.ToString();
    //        }
    //    }
    //    //////////////////   End Inner Classes  //////////////////////


    //    //////////////////////////////////////////////////////////////
    //    //                      Main Class                          //
    //    //////////////////////////////////////////////////////////////
    //    private static readonly ILog LOGGER = LogManager.GetLogger(typeof(BatchedCsvStream<T>));

    //    private IEnumerator<string[]> it;
    //    private int fence;
    //    private bool isBatchOp;
    //    private bool isTerminal;
    //    private bool isArrayType;
    //    private BatchedCsvHeader<string[]> header;
    //    private Stream<T> @delegate;
    //    private int headerStateTracker = 0;


    //    /**
    //     * Constructs a new {@code BatchedCsvStream}
    //     * 
    //     * @param s                 the underlying JDK {@link Stream}
    //     * @param headerLength      the number of header lines preceding the data.
    //     * @see Header
    //     */
    //    public BatchedCsvStream2(Stream<string> s, int headerLength)
    //    {
    //        this.it = (IEnumerator<string[]>)s.Map(line =>
    //        {
    //            ++headerStateTracker;
    //            return line.Split(',');
    //        }).GetEnumerator();
    //        this.fence = headerLength;
    //        MakeHeader();

    //        LOGGER.Debug("Created BatchedCsvStream");
    //    }

    //    /**
    //     * Called internally to create this csv stream's header
    //     */
    //    private void MakeHeader()
    //    {
    //        List<string[]> contents = new List<string[]>();

    //        int i = 0;
    //        while (i++ < fence)
    //        {
    //            it.MoveNext();
    //            string[] h = it.Current;
    //            contents.Add(h);
    //        }
    //        this.header = new BatchedCsvHeader<string[]>(contents, fence);
    //        this.isArrayType = IsArrayType();

    //        if (LOGGER.IsDebugEnabled)
    //        {
    //            LOGGER.Debug("Created Header:");
    //            foreach (string[] h in contents)
    //            {
    //                LOGGER.Debug("\t" + Arrays.ToString(h));
    //            }
    //            LOGGER.Debug("Successfully created BatchedCsvHeader.");
    //        }
    //    }

    //    /**
    //     * <p>
    //     * Returns a flag indicating whether the underlying stream has had
    //     * a terminal operation called on it, indicating that it can no longer
    //     * have operations built up on it.
    //     * </p><p>
    //     * The "terminal" flag if true does not indicate that the stream has reached
    //     * the end of its data, it just means that a terminating operation has been
    //     * invoked and that it can no longer support intermediate operation creation.
    //     * 
    //     * @return  true if terminal, false if not.
    //     */
    //    public bool IsTerminal()
    //    {
    //        return this.isTerminal;
    //    }

    //    //    /**
    //    //     * Returns a flag indicating whether this {@link Stream} is 
    //    //     * currently batching its operations.
    //    //     * 
    //    //     * @return
    //    //     */
    //    //    public bool IsBatchOp()
    //    //    {
    //    //        return isBatchOp;
    //    //    }

    //    //    /**
    //    //     * Sets a flag indicating that whether this {@code BatchedCsvStream} is
    //    //     * currently batching its operations.
    //    //     * 
    //    //     * @param b
    //    //     */
    //    //    public void SetBatchOp(bool b)
    //    //    {
    //    //        this.isBatchOp = b;
    //    //    }

    //    /**
    //     * Returns the {@link BatchedCsvHeader}
    //     * @return
    //     */
    //    public BatchedCsvHeader<string[]> GetHeader()
    //    {
    //        return header;
    //    }

    //    /**
    //     * Returns the portion of the {@link Stream} <em>not containing</em>
    //     * the header. To obtain the header, refer to: {@link #getHeader()}
    //     * 
    //     * @param parallel                      flag indicating whether the underlying
    //     *                                      stream should be parallelized.
    //     * @return the stream continuation
    //     * @see Header
    //     * @see BatchedCsvHeader
    //     * @see #getHeader()
    //     */
    //    private Stream Continuation(bool parallel)
    //    {
    //        if (it == null)
    //        {
    //            throw new InvalidOperationException("You must first create a BatchCsvStream by calling batch(Stream, int, boolean, int)");
    //        }

    //        MemoryStream stream = new MemoryStream();

    //        var splitIterator = GetSequenceIterator(it);

    //        StreamWriter writer = new StreamWriter(stream);
    //        while (splitIterator.MoveNext())
    //        {
    //            var current = splitIterator.Current;
    //            writer.WriteLine(current);
    //        }
    //        //return StreamSupport.stream(
    //        //    Spliterators.spliteratorUnknownSize(
    //        //        parallel ? it : isArrayType ? getArraySequenceIterator(it) : getSequenceIterator(it), // Return a sequencing iterator if not parallel
    //        //                                                                                              // otherwise the Spliterator handles the sequencing
    //        //                                                                                              // through the special SequencingConsumer
    //        //        Spliterator.ORDERED | Spliterator.NONNULL | Spliterator.IMMUTABLE),
    //        //        parallel);
    //        return stream;
    //    }

    //    /**
    //     * Returns a flag indicating whether the input field is an array
    //     * @return
    //     */
    //    private bool IsArrayType()
    //    {
    //        if (GetHeader().GetHeaderValues().Length < 3)
    //        {
    //            return false;
    //        }
    //        foreach (object o in GetHeader().GetHeaderValues()[1].All())
    //        {
    //            if (o.ToString().ToLower().Equals("sarr") || o.ToString().ToLower().Equals("darr"))
    //            {
    //                return isArrayType = true;
    //            }
    //        }
    //        return false;
    //    }

    //    /**
    //     * Called internally to return a sequencing iterator when this stream
    //     * is configured to be non-parallel because it will skip the BatchedSpliterator
    //     * code which internally does the sequencing. So we must provide it here when
    //     * not parallel.
    //     * 
    //     * @param toWrap    the original iterator to wrap
    //     * @return
    //     */
    //    private IEnumerator<string[]> GetSequenceIterator(IEnumerator<string[]> toWrap)
    //    {
    //        return new SequencingEnumerator(toWrap);
    //        //    return new Iterator<String[]>()
    //        //    {
    //        //        private Iterator<String[]> @delegate = toWrap;
    //        //        private int seq = 0;

    //        //        public boolean hasNext()
    //        //        {
    //        //            return @delegate.hasNext();
    //        //        }

    //        //        public String[] next()
    //        //        {
    //        //            isTerminal = true;
    //        //            String[] value = @delegate.next();
    //        //            String[] retVal = new String[value.Length + 1];
    //        //            System.arraycopy(value, 0, retVal, 1, value.Length);
    //        //            retVal[0] = String.valueOf(seq++);

    //        //            return retVal;
    //        //        }

    //        //};
    //    }

    //    public class SequencingEnumerator : IEnumerator<string[]>
    //    {
    //        private readonly IEnumerator<string[]> _toWrap;
    //        private int seq = 0;

    //        public SequencingEnumerator(IEnumerator<string[]> toWrap)
    //        {
    //            _toWrap = toWrap;
    //        }

    //        public void Dispose()
    //        {

    //        }

    //        public bool MoveNext()
    //        {
    //            bool success = _toWrap.MoveNext();
    //            if (!success) return false;

    //            string[] value = _toWrap.Current;
    //            string[] retVal = new string[value.Length + 1];
    //            Array.Copy(value, 0, retVal, 1, value.Length);
    //            retVal[0] = (seq++).ToString();
    //            Current = retVal;
    //            return true;
    //        }

    //        public void Reset()
    //        {
    //            throw new NotImplementedException();
    //        }

    //        public string[] Current { get; private set; }

    //        object IEnumerator.Current
    //        {
    //            get { return Current; }
    //        }
    //    }

    //    //    /**
    //    //     * Called internally to return a sequencing iterator when this stream
    //    //     * is configured to be non-parallel because it will skip the BatchedSpliterator
    //    //     * code which internally does the sequencing. So we must provide it here when
    //    //     * not parallel.
    //    //     * 
    //    //     * This method differs from {@link #getSequenceIterator(Iterator)} by converting
    //    //     * the parsed String[] to a single string in the 2 index.
    //    //     * 
    //    //     * @param toWrap    the original iterator to wrap
    //    //     * @return
    //    //     */
    //    //    private IEnumerator<string[]> GetArraySequenceIterator(IEnumerator<string[]> toWrap)
    //    //    {
    //    //        return new Iterator<string[]>()
    //    //            //{
    //    //            //    private Iterator<String[]> @delegate = toWrap;
    //    //            //    private int seq = 0;
    //    //            //    
    //    //            //                public boolean hasNext()
    //    //            //    {
    //    //            //        return @delegate.hasNext();
    //    //            //    }

    //    //            //    public String[] next()
    //    //            //    {
    //    //            //        isTerminal = true;
    //    //            //        String[] value = @delegate.next();
    //    //            //        String[] retVal = new String[2];
    //    //            //        retVal[0] = String.valueOf(seq++);
    //    //            //        retVal[1] = Arrays.toString(value).trim();

    //    //            //        return retVal;
    //    //            //    }

    //    //            //};
    //    //    }

    //    //    /**
    //    //     * Returns the @delegate underlying {@link Stream}.
    //    //     * @return stream
    //    //     */
    //    //    public Stream<string[]> Stream()
    //    //    {
    //    //        return (Stream<string[]>)this.@delegate;
    //    //    }

    //    //    /**
    //    //     * Initializes the new spliterator using the specified characteristics.
    //    //     * 
    //    //     * @param csv                   the Stream from which to create the spliterator
    //    //     * @param batchSize             the "chunk" length to be processed by each Threaded task
    //    //     * @param isParallel            if true, batching will take place, otherwise not
    //    //     * @param characteristics       overrides the default characteristics of:
    //    //     *                              {@link Spliterator#ORDERED},{@link Spliterator#NONNULL},
    //    //     *                              {@link Spliterator#IMMUTABLE}, and <em>{@link Spliterator#SUBSIZED}.
    //    //     *                              <p><b>WARNING:</b> This last characteristic [<b>SUBSIZED</b>] is <b>necessary</b> if batching is desired.</p></em>
    //    //     * @return
    //    //     */
    //    //    private static BatchSpliterator BatchedSpliterator<T>(
    //    //        BatchedCsvStream<string[]> csv, int batchSize, bool isParallel, int characteristics)
    //    //    {

    //    //        Spliterator<string[]> toWrap = csv.continuation(isParallel).spliterator();
    //    //        return new BatchSpliterator(
    //    //            characteristics, batchSize, toWrap.estimateSize()).setCSV(csv).setToWrap(toWrap);
    //    //    }

    //    //    /**
    //    //     * Called internally to create the {@link BatchSpliterator} the heart and soul
    //    //     * of this class.
    //    //     * @param csv                   the Stream from which to create the spliterator
    //    //     * @param batchSize             the "chunk" length to be processed by each Threaded task
    //    //     * @param isParallel            if true, batching will take place, otherwise not
    //    //     * @return
    //    //     */
    //    //    private static BatchSpliterator BatchedSpliterator<T>(
    //    //        BatchedCsvStream<string[]> csv, int batchSize, boolean isParallel)
    //    //    {

    //    //        Spliterator<string[]> toWrap = csv.continuation(isParallel).spliterator();
    //    //        return new BatchSpliterator(
    //    //            toWrap.characteristics(), batchSize, toWrap.estimateSize()).setCSV(csv).setToWrap(toWrap);
    //    //    }

    //    /**
    //     * Factory method to create a {@code BatchedCsvStream}. If isParallel is false,
    //     * this stream will behave like a typical stream. See also {@link BatchedCsvStream#batch(Stream, int, boolean, int, int)}
    //     * for more fine grained setting of characteristics.
    //     * 
    //     * @param stream                JDK Stream
    //     * @param batchSize             the "chunk" length to be processed by each Threaded task
    //     * @param isParallel            if true, batching will take place, otherwise not
    //     * @param headerLength          number of header lines
    //     * @return
    //     */
    //    public static BatchedCsvStream<string[]> Batch(Stream<string> stream, int batchSize, bool isParallel, int headerLength)
    //    {
    //        //Notice the Type of the Stream becomes String[] - This is an important optimization for 
    //        //parsing the sequence number later. (to avoid calling String.split() on each entry)
    //        //Initializes and creates the CsvHeader here:
    //        BatchedCsvStream<string[]> csv = new BatchedCsvStream<string[]>(stream, headerLength);
    //        //Stream<string[]> s = !isParallel ? csv.Continuation(isParallel) :
    //        //    StreamSupport.stream(BatchedSpliterator(csv, batchSize, isParallel), isParallel);
    //        if (isParallel)
    //        {
    //            throw new NotImplementedException("Check the spliterator stuff");
    //        }
    //        Stream<string[]> s = !isParallel ? csv.Continuation(isParallel) : null;

    //        //csv.@delegate = s;
    //        return csv;
    //    }

    //    //    /**
    //    //     * Factory method to create a {@code BatchedCsvStream}.
    //    //     *  
    //    //     * @param stream                JDK Stream
    //    //     * @param batchSize             the "chunk" length to be processed by each Threaded task
    //    //     * @param isParallel            if true, batching will take place, otherwise not 
    //    //     * @param headerLength          number of header lines
    //    //     * @param characteristics       stream configuration parameters
    //    //     * @return
    //    //     */
    //    //    public static BatchedCsvStream<string[]> Batch(Stream<string> stream, int batchSize, bool isParallel, int headerLength, int characteristics)
    //    //    {
    //    //        //Notice the Type of the Stream becomes String[] - This is an important optimization for 
    //    //        //parsing the sequence number later. (to avoid calling String.split() on each entry MULTIPLE TIMES (for the eventual sort))
    //    //        //Initializes and creates the CsvHeader here:
    //    //        BatchedCsvStream<string[]> csv = new BatchedCsvStream<>(stream, headerLength);
    //    //        Stream<string[]> s = !isParallel ? csv.continuation(isParallel) :
    //    //            StreamSupport.stream(batchedSpliterator(csv, batchSize, isParallel, characteristics), isParallel);
    //    //        csv.delegate = s;
    //    //        return csv;
    //    //    }

    //    /**
    //     * Implements the {@link MetaStream} {@link FunctionalInterface} enabling
    //     * retrieval of stream meta information.
    //     */
    //    public IValueList GetMeta()
    //    {
    //        return GetHeader();
    //    }

    //    //    //////////////////////////////////////////////////////////////
    //    //    //          Overridden Methods from Parent Class            //
    //    //    //////////////////////////////////////////////////////////////
    //    //public IEnumerator<T> Iterator()
    //    //    {
    //    //        return @delegate.iterator();
    //    //    }

    //    //public Spliterator<T> Spliterator()
    //    //    {
    //    //        return @delegate.spliterator();
    //    //    }

    //    public bool IsParallel()
    //    {
    //        return false;
    //        //return @delegate.IsParallel();
    //    }

    //    public IStream<int[]> Map(Func<string[], int[]> mapFunc)
    //    {
    //        throw new NotImplementedException();
    //    }


    //    //public Stream<T> Sequential()
    //    //    {
    //    //        return @delegate.sequential();
    //    //    }

    //    //public Stream<T> Parallel()
    //    //    {
    //    //        return @delegate.parallel();
    //    //    }

    //    //public Stream<T> Unordered()
    //    //    {
    //    //        return @delegate.unordered();
    //    //    }

    //    //public Stream<T> OnClose(Runnable closeHandler)
    //    //    {
    //    //        return @delegate.onClose(closeHandler);
    //    //    }

    //    //public void Close()
    //    //    {
    //    //        @delegate.close();
    //    //    }

    //    //public Stream<T> Filter(Predicate<T> predicate)
    //    //    {
    //    //        return @delegate.filter(predicate);
    //    //    }

    //    public IStream<TR> Map<TR>(Func<string[], TR> mapper)
    //    {
    //        //throw new NotImplementedException();
    //        return @delegate.Map(i =>
    //        {

    //            return mapper((string[])((object)i));
    //        });
    //    }


    //    //public IntStream MapToInt(ToIntFunction<? super T> mapper)
    //    //    {
    //    //        return @delegate.mapToInt(mapper);
    //    //    }


    //    //public LongStream MapToLong(ToLongFunction<? super T> mapper)
    //    //    {
    //    //        return @delegate.mapToLong(mapper);
    //    //    }


    //    //public DoubleStream MapToDouble(ToDoubleFunction<? super T> mapper)
    //    //    {
    //    //        return @delegate.mapToDouble(mapper);
    //    //    }


    //    //public <R> Stream<R> FlatMap(Function<? super T, ? extends Stream<? extends R>> mapper)
    //    //    {
    //    //        return @delegate.flatMap(mapper);
    //    //    }


    //    //public IntStream FlatMapToInt(Function<? super T, ? extends IntStream> mapper)
    //    //    {
    //    //        return @delegate.flatMapToInt(mapper);
    //    //    }


    //    //public LongStream FlatMapToLong(Function<? super T, ? extends LongStream> mapper)
    //    //    {
    //    //        return @delegate.flatMapToLong(mapper);
    //    //    }


    //    //public DoubleStream FlatMapToDouble(Function<? super T, ? extends DoubleStream> mapper)
    //    //    {
    //    //        return @delegate.flatMapToDouble(mapper);
    //    //    }


    //    //public Stream<T> Distinct()
    //    //    {
    //    //        return @delegate.distinct();
    //    //    }


    //    //public Stream<T> sorted()
    //    //    {
    //    //        return @delegate.sorted();
    //    //    }


    //    //public Stream<T> sorted(Comparator<? super T> comparator)
    //    //    {
    //    //        return @delegate.sorted(comparator);
    //    //    }


    //    //public Stream<T> Peek(Consumer<? super T> action)
    //    //    {
    //    //        return @delegate.peek(action);
    //    //    }


    //    //public Stream<T> Limit(long maxSize)
    //    //    {
    //    //        return @delegate.limit(maxSize);
    //    //    }


    //    //public Stream<T> Skip(long n)
    //    //    {
    //    //        return @delegate.skip(n);
    //    //    }


    //    //public void ForEach(Consumer<? super T> action)
    //    //    {
    //    //        @delegate.forEach(action);
    //    //    }


    //    //public void ForEachOrdered(Consumer<? super T> action)
    //    //    {
    //    //        @delegate.forEachOrdered(action);
    //    //    }


    //    //public object[] toArray()
    //    //    {
    //    //        return @delegate.toArray();
    //    //    }


    //    //public  TA[] toArray<TA>(IntFunction<TA[]> generator)
    //    //    {
    //    //        return @delegate.toArray(generator);
    //    //    }


    //    //public T reduce(T identity, BinaryOperator<T> accumulator)
    //    //    {
    //    //        return @delegate.reduce(identity, accumulator);
    //    //    }


    //    //public Optional<T> reduce(BinaryOperator<T> accumulator)
    //    //    {
    //    //        return @delegate.reduce(accumulator);
    //    //    }


    //    //public  TU reduce<TU>(TU identity, BiFunction<TU, ? super T, U> accumulator, BinaryOperator<U> combiner)
    //    //    {
    //    //        return @delegate.reduce(identity, accumulator, combiner);
    //    //    }


    //    //public <R> R collect(Supplier<R> supplier, BiConsumer<R, ? super T> accumulator, BiConsumer<R, R> combiner)
    //    //    {
    //    //        return @delegate.collect(supplier, accumulator, combiner);
    //    //    }


    //    //public <R, A> R collect(Collector<? super T, A, R> collector)
    //    //    {
    //    //        return @delegate.collect(collector);
    //    //    }


    //    //public Optional<T> Min(Comparator<? super T> comparator)
    //    //    {
    //    //        return @delegate.min(comparator);
    //    //    }


    //    //public Optional<T> Max(Comparator<? super T> comparator)
    //    //    {
    //    //        return @delegate.max(comparator);
    //    //    }


    //    //public long Count()
    //    //    {
    //    //        return @delegate.count();
    //    //    }


    //    //public bool AnyMatch(Predicate<? super T> predicate)
    //    //    {
    //    //        return @delegate.anyMatch(predicate);
    //    //    }


    //    //public bool AllMatch(Predicate<? super T> predicate)
    //    //    {
    //    //        return @delegate.allMatch(predicate);
    //    //    }


    //    //public bool NoneMatch(Predicate<? super T> predicate)
    //    //    {
    //    //        return @delegate.noneMatch(predicate);
    //    //    }


    //    //public Optional<T> FindFirst()
    //    //    {
    //    //        return @delegate.findFirst();
    //    //    }


    //    //public Optional<T> FindAny()
    //    //    {
    //    //        return @delegate.findAny();
    //    //    }

    //    //public static void main(String[] args)
    //    //{
    //    //    Stream<String> stream = Stream.of(
    //    //        "timestamp,consumption",
    //    //        "datetime,float",
    //    //        "T,",
    //    //        "7/2/10 0:00,21.2",
    //    //        "7/2/10 1:00,16.4",
    //    //        "7/2/10 2:00,4.7",
    //    //        "7/2/10 3:00,4.7",
    //    //        "7/2/10 4:00,4.6",
    //    //        "7/2/10 5:00,23.5",
    //    //        "7/2/10 6:00,47.5",
    //    //        "7/2/10 7:00,45.4",
    //    //        "7/2/10 8:00,46.1",
    //    //        "7/2/10 9:00,41.5",
    //    //        "7/2/10 10:00,43.4",
    //    //        "7/2/10 11:00,43.8",
    //    //        "7/2/10 12:00,37.8",
    //    //        "7/2/10 13:00,36.6",
    //    //        "7/2/10 14:00,35.7",
    //    //        "7/2/10 15:00,38.9",
    //    //        "7/2/10 16:00,36.2",
    //    //        "7/2/10 17:00,36.6",
    //    //        "7/2/10 18:00,37.2",
    //    //        "7/2/10 19:00,38.2",
    //    //        "7/2/10 20:00,14.1");

    //    //    BatchedCsvStream<String> csv = new BatchedCsvStream<>(stream, 3);
    //    //    System.out.println("Header: " + csv.getHeader());
    //    //    csv.continuation(false).forEach(l->System.out.println("line: " + Arrays.toString(l)));
    //    //}
    //}

    /// <summary>
    /// Defines a CSV stream that reads CSV files
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class BatchedCsvStream<T> : IMetaStream
    {
        [NonSerialized]
        private static readonly ILog LOGGER = LogManager.GetLogger(typeof(BatchedCsvStream<T>));

        private readonly int _headerLength;
        private BatchedCsvHeader<string[]> _header;
        private bool _isArrayType;
        internal IStream<string[]> _contentStream;

        public BatchedCsvStream(IStream<string> stream, int headerSize, Func<string, string[]> mappingFunc)
        {
            _headerLength = headerSize;
            MakeHeader(stream);
            // return line.split("[\\s]*,[\\s]*", -1); 
            //string tempValue;
            //while ((tempValue = stream.Read()) != null)
            //{
            //    _contents.Add(tempValue.Split(','));
            //}

            //Regex regex = new Regex("[\\s]*,[\\s]*");
            if (mappingFunc != null)
            {
                _contentStream = stream.Map(mappingFunc);
            }
            else
            {
                _contentStream = stream.Map(s =>
                {
                    //Debug.WriteLine(">> Splitting CSV string");
                    if (s.StartsWith("[") || _isArrayType)
                    {
                        // todo: review this
                        //return Regex.Split(s, "[\\s]*,[\\s]*");
                        return new[] { s };
                    }
                    return s.Split(',');
                });
            }
            LOGGER.Debug("Created BatchedCsvStream");
        }

        /// <summary>
        /// Extracts the header from the CSV file
        /// </summary>
        private void MakeHeader(IStream<string> stream)
        {
            List<string[]> contents = new List<string[]>();

            for (int i = 0; i < _headerLength; i++)
            {
                contents.Add(stream.Read().Split(','));
            }
            stream.SetOffset(_headerLength);
            BatchedCsvHeader<string[]> header = new BatchedCsvHeader<string[]>(contents, _headerLength);
            _header = header;
            _isArrayType = IsArrayType();

            if (LOGGER.IsDebugEnabled)
            {
                LOGGER.Debug("Created Header:");
                foreach (string[] h in contents)
                {
                    LOGGER.Debug("\t" + Arrays.ToString(h));
                }
                LOGGER.Debug("Successfully created BatchedCsvHeader.");
            }
        }

        /// <summary>
        /// Factory method to create a <see cref="BatchedCsvStream{T}"/>. If isParallel is false,
        /// this stream will behave like a typical stream. See also <see cref="BatchedCsvStream{T}.Batch"/>
        /// for more fine grained setting of characteristics.
        /// </summary>
        /// <param name="stream">Incomming stream</param>
        /// <param name="batchSize">the "chunk" length to be processed by each Threaded task</param>
        /// <param name="isParallel">if true, batching will take place, otherwise not</param>
        /// <param name="headerLength">number of header lines</param>
        /// <returns></returns>
        public static BatchedCsvStream<string[]> Batch(IStream<string> stream, int batchSize, bool isParallel, int headerLength, Func<string, string[]> mappingFunc = null)
        {
            //Notice the Type of the Stream becomes String[] - This is an important optimization for 
            //parsing the sequence number later. (to avoid calling String.split() on each entry)
            //Initializes and creates the CsvHeader here:

            // Create a new string that returns arrays of strings
            BatchedCsvStream<string[]> csv = new BatchedCsvStream<string[]>(stream, headerLength, mappingFunc);
            stream.SetOffset(headerLength);
            if (isParallel)
            {
                throw new NotImplementedException("Check the spliterator stuff");
            }
            IStream<string[]> s = !isParallel ? csv.Continuation(isParallel) : null;
            csv._contentStream = s;
            return csv;
        }

        /// <summary>
        /// Returns the portion of the <see cref="Stream"/> <em>not containing</em>
        /// the header. To obtain the header, refer to: <see cref="GetHeader()"/>
        /// </summary>
        /// <param name="parallel">flag indicating whether the underlying stream should be parallelized.</param>
        /// <returns>the stream continuation</returns>
        internal IStream<string[]> Continuation(bool parallel)
        {
            //_isTerminal = true;

            if (_contentStream == null)
            {
                throw new InvalidOperationException("You must first create a BatchCsvStream by calling batch(Stream, int, bool, int)");
            }

            int i = 0;
            IStream<string[]> stream = _contentStream.Map(value =>
            {
                string[] retVal = new string[value.Length + 1];
                Array.Copy(value, 0, retVal, 1, value.Length);
                retVal[0] = i++.ToString();
                return retVal;
            });

            //_contentStream = stream;

            //for (int i = 0; i < _contents.Count; i++)
            //{
            //    string[] value = _contents[i];
            //    string[] retVal = new string[value.Length + 1];
            //    Array.Copy(value, 0, retVal, 1, value.Length);
            //    retVal[0] = i.ToString();
            //}


            //var splitIterator = GetSequenceIterator(it);

            //StreamWriter writer = new StreamWriter(stream);
            //while (splitIterator.MoveNext())
            //{
            //    var current = splitIterator.Current;
            //    writer.WriteLine(current);
            //}
            //return StreamSupport.stream(
            //    Spliterators.spliteratorUnknownSize(
            //        parallel ? it : isArrayType ? getArraySequenceIterator(it) : getSequenceIterator(it), // Return a sequencing iterator if not parallel
            //                                                                                              // otherwise the Spliterator handles the sequencing
            //                                                                                              // through the special SequencingConsumer
            //        Spliterator.ORDERED | Spliterator.NONNULL | Spliterator.IMMUTABLE),
            //        parallel);
            return stream;
        }

        /// <summary>
        /// Returns a flag indicating whether the input field is an array
        /// </summary>
        private bool IsArrayType()
        {
            Tuple[] headerValues = GetHeader().GetHeaderValues();
            if (headerValues.Length < 3)
            {
                return false;
            }
            if (headerValues[1].All().Any(o => o.ToString().ToLower().Equals("sarr") || o.ToString().ToLower().Equals("darr")))
            {
                return _isArrayType = true;
            }
            return false;
        }

        /// <summary>
        /// Returns the <see cref="BatchedCsvHeader{T}"/>
        /// </summary>
        /// <returns></returns>
        public BatchedCsvHeader<string[]> GetHeader()
        {
            return _header;
        }

        #region Implementation of IMetaStream

        public IBaseStream Map(Func<string[], int[]> mapFunc)
        {
            return (IBaseStream)_contentStream.Map(mapFunc);
            //return new Stream<int[]>(_contents.Select(mapFunc).ToArray());
        }

        public long Count()
        {
            return _contentStream.Count();
        }

        /// <summary>
        /// Returns a <see cref="IValueList"/> containing meta information (i.e. header information)
        /// which can be used to infer the structure of the underlying stream.
        /// </summary>
        /// <returns> a <see cref="IValueList"/> describing meta features of this stream.</returns>
        public IValueList GetMeta()
        {
            return GetHeader();
        }

        /// <summary>
        /// <p>
        /// Returns a flag indicating whether the underlying stream has had
        /// a terminal operation called on it, indicating that it can no longer
        /// have operations built up on it.
        /// </p>
        /// <p>
        /// The "terminal" flag if true does not indicate that the stream has reached
        /// the end of its data, it just means that a terminating operation has been
        /// invoked and that it can no longer support intermediate operation creation.
        /// </p>
        /// </summary>
        /// <returns>true if terminal, false if not.</returns>
        public bool IsTerminal()
        {
            return _contentStream.IsTerminal();
        }

        public bool IsParallel()
        {
            return false;
        }
        /// <summary>
        /// Returns true when a string[] to int[] conversion is needed (when the raw input is string)
        /// </summary>
        /// <returns></returns>
        public bool NeedsStringMapping()
        {
            return true;
        }

        public IBaseStream DoStreamMapping()
        {
            throw new NotSupportedException("Not needed here, is for images");
        }

        #endregion

        [Serializable]
        public class BatchedCsvHeader<THeaderLine> : IValueList
        {
            /// <summary>
            /// Container for the field values
            /// </summary>
            private readonly Tuple[] _headerValues;

            /// <summary>
            /// Constructs a new <see cref="BatchedCsvHeader{T}"/>
            /// </summary>
            /// <param name="lines">List of csv strings</param>
            /// <param name="configuredHeaderLength">Number of header rows</param>
            public BatchedCsvHeader(List<THeaderLine> lines, int configuredHeaderLength)
            {

                if ((configuredHeaderLength < 1 || lines == null || lines.Count < 1) ||
                    (configuredHeaderLength > 1 && lines.Count != configuredHeaderLength))
                {

                    throw new InvalidOperationException("Actual Header was not the expected size: " +
                        (configuredHeaderLength < 1 ? "> 1" : configuredHeaderLength.ToString()) +
                            ", but was: " + (lines == null ? "null" : lines.Count.ToString()));
                }

                _headerValues = new Tuple[configuredHeaderLength];
                for (int i = 0; i < _headerValues.Length; i++)
                {
                    if (lines[i] is Array)
                    {
                        _headerValues[i] = new Tuple((Array)((object)lines[i]));
                    }
                    else
                    {
                        _headerValues[i] = new Tuple(lines[i]);
                    }
                }
            }

            public Tuple[] GetHeaderValues()
            {
                return _headerValues;
            }

            /// <summary>
            /// Returns the array of values  (<see cref="Tuple"/>) at the specified index.
            /// </summary>
            /// <param name="index">the index of the Tuple to be retrieved.</param>
            /// <returns></returns>
            public Tuple GetRow(int index)
            {
                if (index >= _headerValues.Length)
                {
                    return null;
                }
                return _headerValues[index];
            }

            /// <summary>
            /// Returns the current number of lines in the header.
            /// </summary>
            /// <returns>the current number of lines in the header.</returns>
            public int Size()
            {
                return _headerValues?.Length ?? 0;
            }

            public bool IsLearn()
            {
                throw new NotImplementedException();
            }

            public bool IsReset()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Returns the fieldnames of the header
            /// </summary>
            /// <returns></returns>
            public IEnumerable<string> GetFieldNames()
            {
                return GetRow(0).All().Cast<string>().ToList();
            }

            public List<FieldMetaType> GetFieldTypes()
            {
                return GetRow(1).All().Cast<string>().Select(s => FieldMetaTypeHelper.FromString(s)).ToList();
            }

            public List<SensorFlags> GetSpecialTypes()
            {
                return GetRow(2).All().Cast<string>().Select(s => SensorFlagsHelper.FromString(s)).ToList();
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                _headerValues.ToList().ForEach(l => sb.Append(l).Append("\n"));
                return sb.ToString();
            }


        }

        public void ForEach(Action<object> action)
        {
            // TODO: ensure the foreach goes further on the mapped stream
            _contentStream.ForEach(action);
        }

        /// <summary>
        /// Convienience method for inserting data in the base stream
        /// </summary>
        /// <param name="input"></param>
        public void Write(string[] input)
        {
            _contentStream.Write(input);
        }

        private List<object[]> _cachedValues;
        private int _cachePosition;
        private ModelRecordEncoder _modelRecordEncoder;

        private void BuildCacheList()
        {
            if (_cachedValues == null)
            {
                var copy = _contentStream.Copy();
                _cachedValues = new List<object[]>();
                var fieldTypes = GetHeader().GetFieldTypes();
                do
                {
                    string[] values = copy.Read();
                    if (values == null) break;
                    _cachedValues.Add(TranslateToObjects(fieldTypes, values));
                } while (!copy.EndOfStream);

                _cachePosition = 0;
            }
        }

        private object[] TranslateToObjects(List<FieldMetaType> fieldTypes, string[] values)
        {
            object[] result = new object[values.Length];
            result[0] = int.Parse(values[0]);
            for (int i = 1, j = 0; i < values.Length; i++, j++)
            {
                FieldMetaType fType = fieldTypes[j];
                object value = values[i];

                switch (fType)
                {
                    case FieldMetaType.String:
                        result[i] = value as string;
                        break;
                    case FieldMetaType.DateTime:
                        result[i] = DateTime.Parse(value as string, DateTimeFormatInfo.InvariantInfo);
                        break;
                    case FieldMetaType.Integer:
                        result[i] = int.Parse((string)value);
                        break;
                    case FieldMetaType.Float:
                        result[i] = double.Parse((string)value, NumberFormatInfo.InvariantInfo);
                        break;
                    case FieldMetaType.Boolean:
                        {
                            result[i] = bool.Parse((string)value);
                            break;
                        }
                    case FieldMetaType.List:
                    case FieldMetaType.Coord:
                    case FieldMetaType.Geo:
                    case FieldMetaType.SparseArray:
                    case FieldMetaType.DenseArray:
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the min value of the given field present in this stream.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public object GetFieldMin(string field)
        {
            if (_cachedValues == null)
            {
                BuildCacheList();
            }

            int fieldIndex = GetHeader().GetFieldNames().ToList().IndexOf(field);

            return _cachedValues.Select(v => v[fieldIndex + 1]).Min();
        }

        /// <summary>
        /// Returns the min value of the given field present in this stream.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public object GetFieldMax(string field)
        {
            if (_cachedValues == null)
            {
                BuildCacheList();
            }

            int fieldIndex = GetHeader().GetFieldNames().ToList().IndexOf(field);

            return _cachedValues.Select(v => v[fieldIndex + 1]).Max();
        }

        private List<FieldMetaInfo> GetFields()
        {
            var header = GetHeader();

            return ArrayUtils.Zip(header.GetFieldNames(), header.GetFieldTypes(), header.GetSpecialTypes()).Select(t => new FieldMetaInfo(t.Get(0) as string, (FieldMetaType)t.Get(1), (SensorFlags)t.Get(2))).ToList();
        }

        private AggregationSettings GetAggregationMonthsAndSeconds()
        {
            return new AggregationSettings();
        }

        private object[] GetNextRecord()
        {
            if (_cachedValues == null)
            {
                BuildCacheList();
            }

            if (_cachePosition < _cachedValues.Count)
            {
                return _cachedValues[_cachePosition++];
            }
            return null;
        }

        /// <summary>
        /// Returns next available data record from the storage as a dict, with the
        /// keys being the field names.This also adds in some meta fields:
        /// '_category': The value from the category field (if any)
        /// '_reset': True if the reset field was True (if any)
        /// '_sequenceId': the value from the sequenceId field (if any)
        /// </summary>
        /// <returns></returns>
        public Map<string, object> GetNextRecordDict()
        {
            var values = GetNextRecord();
            if (values == null)
            {
                return null;
            }
            if (!values.Any()) return new Map<string, object>();

            if (_modelRecordEncoder == null)
            {
                _modelRecordEncoder = new ModelRecordEncoder(GetFields(), GetAggregationMonthsAndSeconds());
            }
            return _modelRecordEncoder.Encode(values.Skip(1).ToList()); // skip record number in input
        }
    }

    /// <summary>
    /// Encodes metric data input rows for consumption by OPF models. 
    /// See the `ModelRecordEncoder.encode` method for more details.
    /// </summary>
    public class ModelRecordEncoder
    {
        private List<FieldMetaInfo> _fields;
        private AggregationSettings _aggregationPeriod;
        private int? _sequenceId;
        private List<string> _fieldNames;
        private int? _categoryFieldIndex, _resetFieldIndex, _sequenceFieldIndex, _timestampFieldIndex, _learningFieldIndex;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fields">non-empty sequence of nupic.data.fieldmeta.FieldMetaInfo objects corresponding to fields in input rows.</param>
        /// <param name="aggregationPeriod">aggregation period of the record stream as a
        /// dict containing 'months' and 'seconds'. The months is always an integer
        /// and seconds is a floating point.Only one is allowed to be non-zero at a
        /// time.If there is no aggregation associated with the stream, pass None.
        /// Typically, a raw file or hbase stream will NOT have any aggregation info,
        /// but subclasses of RecordStreamIface, like StreamReader, will and will
        /// provide the aggregation period.This is used by the encode method to
        /// assign a record number to a record given its timestamp and the aggregation
        /// interval.</param>
        public ModelRecordEncoder(List<FieldMetaInfo> fields, AggregationSettings aggregationPeriod = null)
        {
            if (fields == null || !fields.Any())
                throw new ArgumentException("fields arg must be non-empty", nameof(fields));

            _fields = fields;
            _aggregationPeriod = aggregationPeriod;
            _sequenceId = -1;
            _fieldNames = fields.Select(f => f.name).ToList();
            _categoryFieldIndex = _getFieldIndexBySpecial(fields, SensorFlags.Category);
            _resetFieldIndex = _getFieldIndexBySpecial(fields, SensorFlags.Reset);
            _sequenceFieldIndex = _getFieldIndexBySpecial(fields, SensorFlags.Sequence);
            _timestampFieldIndex = _getFieldIndexBySpecial(fields, SensorFlags.Timestamp);
            _learningFieldIndex = _getFieldIndexBySpecial(fields, SensorFlags.Learn);
        }

        public void Rewind()
        {
            _sequenceId = -1;
        }

        public Map<string, object> Encode(IList<object> inputRow)
        {
            // Create the return dict
            Map<string, object> result = new Map<string, object>(ArrayUtils.Zip(_fieldNames, inputRow).ToDictionary(t => (string)t.Get(0), t => t.Get(1)));

            // Add in the special fields
            if (_categoryFieldIndex.HasValue)
            {
                // category value can be an int or a list
                if (inputRow[_categoryFieldIndex.Value] is int)
                {
                }
            }

            if (_resetFieldIndex.HasValue)
            {
                result["_reset"] = inputRow[_resetFieldIndex.Value] == "1" ? 1 : 0;
            }
            else
            {
                result["_reset"] = 0;
            }

            if (_learningFieldIndex.HasValue)
            {
                result["_learning"] = inputRow[_learningFieldIndex.Value] == "1" ? 1 : 0;
            }

            result["_timestampRecordIdx"] = null;
            if (_timestampFieldIndex.HasValue)
            {
                result["_timestamp"] = inputRow[_timestampFieldIndex.Value];
                // Compute the record index based on timestamp
                result["_timestampRecordIdx"] = _computeTimestampRecordIdx((DateTime)inputRow[_timestampFieldIndex.Value]);
            }
            else
            {
                result["_timestamp"] = null;
            }

            // -----------------------------------------------------------------------
            // Figure out the sequence ID
            bool hasReset = _resetFieldIndex.HasValue;
            bool hasSequenceId = _sequenceFieldIndex.HasValue;
            object sequenceId = null;
            if (hasReset && !hasSequenceId)
            {
                // reset only
                if ((int)result["_reset"] > 0)
                {
                    _sequenceId += 1;
                }
                sequenceId = _sequenceId;
            }
            else if (!hasReset && hasSequenceId)
            {
                sequenceId = inputRow[_sequenceFieldIndex.Value];
                result["_reset"] = sequenceId.GetHashCode() != _sequenceId ? 1 : 0;
                _sequenceId = sequenceId.GetHashCode();
            }
            else if (hasReset && hasSequenceId)
            {
                sequenceId = inputRow[_sequenceFieldIndex.Value];
            }
            else
            {
                sequenceId = 0;
            }

            if (sequenceId != null)
            {
                result["_sequenceId"] = sequenceId.GetHashCode();
            }
            else
            {
                result["_sequenceId"] = null;
            }

            return result;
        }

        /// <summary>
        /// Give the timestamp of a record (a datetime object), compute the record's
        /// timestamp index - this is the timestamp divided by the aggregation period.
        /// </summary>
        /// <param name="recordTs"></param>
        /// <returns></returns>
        private int? _computeTimestampRecordIdx(DateTime recordTs)
        {
            if (_aggregationPeriod == null)
                return null;

            int? result = null;
            // Base record index on number of elapsed months if aggregation is in months
            if (_aggregationPeriod.months > 0)
            {
                Debug.Assert(_aggregationPeriod.seconds == 0);
                result = (int)((recordTs.Year * 12 + (recordTs.Month - 1)) / _aggregationPeriod.months);
            }
            // Base record index on elapsed seconds
            else if (_aggregationPeriod.seconds > 0)
            {
                var delta = recordTs - new DateTime(1, 1, 1);
                var deltaSecs = delta.Days * 24 * 60 * 60 + delta.Seconds + delta.Milliseconds / 1000.0;
                result = (int?)(deltaSecs / _aggregationPeriod.seconds);
            }
            else
            {
                result = null;
            }
            return result;
        }

        /// <summary>
        /// Return index of the field matching the field meta special value.
        /// </summary>
        /// <param name="fields">equence of nupic.data.fieldmeta.FieldMetaInfo objects representing the fields of a stream</param>
        /// <param name="special">one of the special field attribute values from <see cref="SensorFlags"/></param>
        /// <returns>first zero-based index of the field tagged with the target field meta special attribute; None if no such field</returns>
        public static int? _getFieldIndexBySpecial(List<FieldMetaInfo> fields, SensorFlags special)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].special == special)
                    return i;
            }
            return null;
        }
    }
}