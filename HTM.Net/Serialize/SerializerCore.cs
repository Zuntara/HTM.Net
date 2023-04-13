using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using HTM.Net.Model;

using Newtonsoft.Json;

namespace HTM.Net.Serialize
{
    [Serializable]
    public class SerializerCore : Persistable
    {
        [NonSerialized]
        private Type[] _classes;

        public SerializerCore(params Type[] classes)
        {
            _classes = classes;
        }

        public string SerializeJson<T>(T instance)
            where T : IPersistable
        {
            string serialized;
            try
            {
                serialized = JsonConvert.SerializeObject(instance);
            }
            catch (Exception e)
            {
                throw new SerializationException("failure in serialization", e);
            }
            return serialized;
        }

        public T DeserializeJson<T>(string content)
            where T : IPersistable
        {
            return JsonConvert.DeserializeObject<T>(content);
        }

        [Obsolete("Use SerializeJson instead", false)]
        public byte[] Serialize<T>(T instance)
            where T : IPersistable
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

        [Obsolete("Use DeserializeJson instead", false)]
        public T Deserialize<T>(byte[] bytes)
            where T : IPersistable
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(bytes);
            T obj = (T) formatter.Deserialize(ms);
            return (T) obj.PostDeSerialize();
        }
    }
}