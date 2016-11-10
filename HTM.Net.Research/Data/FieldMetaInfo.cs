using HTM.Net.Network.Sensor;
using HTM.Net.Util;

namespace HTM.Net.Research.Data
{
    public class FieldMetaInfoBase : NamedTuple
    {
        public FieldMetaInfoBase(string name, FieldMetaType type, SensorFlags special) 
            : base(new [] {"name", "type", "special"}, new object[] { name, type, special })
        {
        }
    }

    public class FieldMetaInfo : FieldMetaInfoBase
    {
        public FieldMetaInfo(string name, FieldMetaType type, SensorFlags special)
            : base(name, type, special)
        {
        }

        public string name
        {
            get {  return GetAsString("name");}
        }

        public FieldMetaType type { get { return (FieldMetaType) Get(1); } }
        public SensorFlags special { get { return (SensorFlags) Get(2); } }
    }
}