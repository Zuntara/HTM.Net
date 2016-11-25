using System;

namespace HTM.Net.Network
{
    public class RegionAlreadyClosedException : Exception
    {
        public RegionAlreadyClosedException(string msg)
            : base(msg)
        {

        }
    }
}