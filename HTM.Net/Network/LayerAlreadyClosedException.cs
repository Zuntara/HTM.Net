using System;

namespace HTM.Net.Network
{
    public class LayerAlreadyClosedException : Exception
    {
        public LayerAlreadyClosedException()
            : base("Layer already \"closed\"")
        {

        }
    }
}