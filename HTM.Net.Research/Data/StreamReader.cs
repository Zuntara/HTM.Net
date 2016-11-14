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