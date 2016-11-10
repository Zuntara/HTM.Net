using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Data
{
    /// <summary>
    /// Encodes metric data input rows for consumption by OPF models. 
    /// See the `ModelRecordEncoder.encode` method for more details.
    /// </summary>
    public class ModelRecordEncoder
    {
        private List<FieldMetaInfo> _fields;
        private AggregationDict _aggregationPeriod;
        private int _sequenceId;
        private Tuple _fieldNames;
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
        public ModelRecordEncoder(List<FieldMetaInfo> fields, AggregationDict aggregationPeriod = null)
        {
            if (fields == null || !fields.Any())
                throw new ArgumentException("fields arg must be non-empty", nameof(fields));

            _fields = fields;
            _aggregationPeriod = aggregationPeriod;
            _sequenceId = -1;
            _fieldNames = new Util.Tuple(fields.Select(f => f.name));
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

        public Tuple Encode(Map<string,object> inputRow)
        {
            Tuple result = null;// ArrayUtils.Zip(_fieldNames, inputRow);

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

    /// <summary>
    /// This is the interface for the record input/output storage classes.
    /// </summary>
    public abstract class RecordStreamIface
    {
        private ModelRecordEncoder _modelRecordEncoder;

        protected RecordStreamIface()
        {
            // Will be initialized on-demand in getNextRecordDict with a
            // ModelRecordEncoder instance, once encoding metadata is available
            _modelRecordEncoder = null;
        }

        /// <summary>
        /// Close the stream
        /// </summary>
        public abstract void Close();

        /// <summary>
        /// Put us back at the beginning of the file again
        /// </summary>
        public virtual void Rewind()
        {
            if (_modelRecordEncoder != null)
            {
                _modelRecordEncoder.Rewind();
            }
        }

        /// <summary>
        /// Returns next available data record from the storage. If useCache is
        /// False, then don't read ahead and don't cache any records.
        /// </summary>
        /// <param name="useCache"></param>
        /// <returns>
        /// a data row (a list or tuple) if available; None, if no more records
        /// in the table(End of Stream - EOS); empty sequence(list or tuple)
        /// when timing out while waiting for the next record.
        /// </returns>
        public abstract object GetNextRecord(bool useCache = true);


    }
}