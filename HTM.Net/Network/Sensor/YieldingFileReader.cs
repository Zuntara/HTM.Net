using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HTM.Net.Network.Sensor
{
    public class YieldingFileReader
    {
        public static IEnumerable<string> ReadAllLines(string path, Encoding encoding)
        {
            using (StreamReader sr = new StreamReader(path, encoding))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        } 
    }
}