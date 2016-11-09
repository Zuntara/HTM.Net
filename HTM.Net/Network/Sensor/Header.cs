using System;
using System.Collections.Generic;
using System.Linq;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Network.Sensor
{
    /**
     * Meta data describing the fields, types and reset
     * rules usually specified by a file header, but may 
     * be manually or programmatically set.
     */
    [Serializable]
    public class Header : IValueList
    {
        private readonly IValueList _rawTupleList;
        /** Name of each field */
        private readonly List<string> _fieldNames;
        /** Field data types */
        private readonly List<FieldMetaType> _fieldMeta;
        /** Processing flags and hints */
        private readonly List<SensorFlags> _sensorFlags;

        private bool _isChanged;
        private bool _isLearn = true;

        private int[] _resetIndexes;
        private int[] _seqIndexes;
        //@SuppressWarnings("unused")
        private int[] _tsIndexes;
        private int[] _learnIndexes;
        //@SuppressWarnings("unused")
        private int[] _categoryIndexes;

        private List<string> _sequenceCache;

        /**
         * Constructs a new {@code Header} using the specified
         * {@link ValueList}
         * 
         * @param input     3 rows of data describing the input
         */
        public Header(IValueList input)
        {
            if (input.Size() != 3)
            {
                throw new ArgumentException("Input did not have 3 rows");
            }
            _rawTupleList = input;
            _fieldNames = input.GetRow(0).All().Select(o => o.ToString().Trim()).ToList();
            _fieldMeta = input.GetRow(1).All().Select(FieldMetaTypeHelper.FromString).ToList();
            _sensorFlags = input.GetRow(2).All().Select(SensorFlagsHelper.FromString).ToList();

            InitIndexes();
        }

        /**
         * Retrieves the header line specified.
         */
        public Tuple GetRow(int index)
        {
            return _rawTupleList.GetRow(index);
        }

        /**
         * Returns the number of configuration lines contained.
         * WARNING: Must be size == 3
         */
        public int Size()
        {
            return _rawTupleList.Size();
        }

        /**
         * Returns the header line containing the field names.
         * @return
         */
        public List<string> GetFieldNames()
        {
            return _fieldNames;
        }

        /// <summary>
        /// Returns the header line containing the field types.
        /// </summary>
        public List<FieldMetaType> GetFieldTypes()
        {
            return _fieldMeta;
        }

        /**
         * Returns the header line ({@link List}) containing the
         * control flags (in the 3rd line) which designate control
         * operations such as turning learning on/off and resetting
         * the state of an algorithm.
         * 
         * @return
         */
        public List<SensorFlags> GetFlags()
        {
            return _sensorFlags;
        }

        /// <summary>
        /// Returns a flag indicating whether any watched column has changed data.
        /// </summary>
        public bool IsReset()
        {
            return _isChanged;
        }

        /// <summary>
        /// Returns a flag indicating whether the current input state is set to learn or not.
        /// </summary>
        public bool IsLearn()
        {
            return _isLearn;
        }

        /**
         * Processes the current line of input and sets flags based on 
         * sensor flags formed by the 3rd line of a given header.
         * 
         * @param input
         */
        public void Process(string[] input)
        {
            _isChanged = false;

            if (_resetIndexes.Length > 0)
            {
                foreach (int i in _resetIndexes)
                {
                    if (int.Parse(input[i].Trim()) == 1)
                    {
                        _isChanged = true; break;
                    }
                    else {
                        _isChanged = false;
                    }
                }
            }

            if (_learnIndexes.Length > 0)
            {
                foreach (int i in _learnIndexes)
                {
                    if (int.Parse(input[i].Trim()) == 1)
                    {
                        _isLearn = true; break;
                    }
                    else {
                        _isLearn = false;
                    }
                }
            }

            // Store lines in cache to detect when current input is a change.
            if (_seqIndexes.Length > 0)
            {
                bool sequenceChanged = false;
                if (!_sequenceCache.Any())
                {
                    foreach (int i in _seqIndexes)
                    {
                        _sequenceCache.Add(input[i]);
                    }
                }
                else {
                    int idx = 0;
                    foreach (int i in _seqIndexes)
                    {
                        if (!_sequenceCache[idx].Equals(input[i]))
                        {
                            _sequenceCache[idx] = input[i];
                            sequenceChanged = true;
                        }
                    }
                }
                _isChanged |= sequenceChanged;
            }
        }

        /**
         * Initializes the indexes of {@link SensorFlags} types to aid
         * in line processing.
         */
        private void InitIndexes()
        {
            int idx = 0;
            List<int> tList = new List<int>();
            List<int> rList = new List<int>();
            List<int> cList = new List<int>();
            List<int> sList = new List<int>();
            List<int> lList = new List<int>();
            foreach (SensorFlags sf in _sensorFlags)
            {
                switch (sf)
                {
                    case SensorFlags.T:
                    case SensorFlags.Timestamp:
                        tList.Add(idx); break;
                    case SensorFlags.R:
                    case SensorFlags.Reset:
                        rList.Add(idx); break;
                    case SensorFlags.C:
                    case SensorFlags.Category:
                        cList.Add(idx); break;
                    case SensorFlags.S:
                    case SensorFlags.Sequence:
                        sList.Add(idx); break;
                    case SensorFlags.L:
                    case SensorFlags.Learn:
                        lList.Add(idx); break;
                    default:
                        break;
                }
                idx++;
            }

            // Add + 1 to each to offset for Sensor insertion of sequence number in all row headers.
            _resetIndexes = rList.Select(i=> i +1).ToArray();
            _seqIndexes = sList.Select(i => i + 1).ToArray();
            _categoryIndexes = cList.Select(i => i + 1).ToArray();
            _tsIndexes = tList.Select(i => i + 1).ToArray();
            _learnIndexes = lList.Select(i => i + 1).ToArray();

            if (_seqIndexes.Length > 0)
            {
                _sequenceCache = new List<string>();
            }
        }
    }
}