using System;
using System.IO;

namespace HTM.Net.Datagen
{
    public class ResourceLocator
    {
        public interface IResource
        {
            FileInfo Get();
        }

        public static Uri Uri(string s)
        {
            try
            {
                Uri url = new Uri(s);
                return url;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("failed to build uri " + s, e);
            }
        }

        //public static string Path(string s)
        //{
        //    return null;
        //    //Uri url =  typeof(ResourceLocator).GetResource(s);
        //    //if (url == null)
        //    //{
        //    //    url = typeof(ResourceLocator).getClassLoader().getResource(s);
        //    //}
        //    //return new FileInfo(url.getPath()).getPath();
        //}

        public static string Path(Type resourceType, string name)
        {
            return name;
            //Uri url =  typeof(ResourceLocator).GetResource(s);
            //if (url == null)
            //{
            //    url = typeof(ResourceLocator).getClassLoader().getResource(s);
            //}
            //return new FileInfo(url.getPath()).getPath();
        }

        public static string Locate(string s)
        {
            return null;
            //return typeof(ResourceLocator).getPackage().getName().replace('.', '/') + File.separator + s;
        }
    }
}