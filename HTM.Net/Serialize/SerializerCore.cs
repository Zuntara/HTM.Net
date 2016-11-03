using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using HTM.Net.Model;
using log4net;

namespace HTM.Net.Serialize
{
    [Serializable]
    public class SerializerCore : Persistable<SerializerCore>
    {
        protected static readonly ILog LOGGER = LogManager.GetLogger(typeof(SerializerCore));

        private Type[] classes;

        public byte[] Serialize<T>(T instance)
            where T : IPersistable<T>
        {
            byte[] bytes;
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                MemoryStream ms = new MemoryStream();
                formatter.Serialize(ms, instance);
                bytes = ms.ToArray();
            }
            catch (Exception e)
            {
                throw new SerializationException("failure in serialisation", e);
            }
            return bytes;
        }

        public T Deserialize<T>(byte[] bytes)
            where T : IPersistable<T>
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(bytes);
            T obj = (T) formatter.Deserialize(ms);
            return obj.PostDeSerialize();
        }
    }
}