using System;
using System.Collections.Generic;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Vision.Sensor
{
    public class ImageHeader : IValueList
    {
        public Tuple GetRow(int row)
        {
            if (row == 0)
            {
                return new Tuple("category", "imageIn");
            }
            if (row == 1)
            {
                return new Tuple("int", "darr");
            }
            if (row == 2)
            {
                return new Tuple(",");
            }
            throw new NotImplementedException();
        }

        public int Size()
        {
            return 3;
        }

        public bool IsLearn()
        {
            throw new NotImplementedException();
        }

        public bool IsReset()
        {
            throw new NotImplementedException();
        }

        public List<FieldMetaType> GetFieldTypes()
        {
            throw new NotImplementedException();
        }
    }
}