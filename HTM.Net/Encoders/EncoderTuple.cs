using System;

namespace HTM.Net.Encoders
{
    /**
     * Subclass of {@link Tuple} specialized to hold the 3-value contents 
     * of an "encoder tuple". Each {@code EncoderTuple} holds a name, encoder and offset
     * in that order. Also, every EncoderTuple's size == 3.
     * 
     * @see Tuple
     */
 [Serializable]
    public class EncoderTuple : Util.Tuple
    {
        /**
         * Constructs a new {@code EncoderTuple}
         * 
         * @param encoderName		the {@link Encoder}'s name
         * @param e			the {@link Encoder}
         * @param offset	the offset within the input (first on bit) that this 
         * 					encoder encodes/decodes. (see  {@link ScalarEncoder#getFirstOnBit(
         * 						org.numenta.nupic.research.Connections, double)})
         */
        public EncoderTuple(string fieldName, string encoderName, IEncoder e, int offset)
            : base(fieldName, encoderName, e, offset)
        {
            if (encoderName == null) throw new ArgumentException("Can't instantiate an EncoderTuple " +
                 " with a null encoderName");
            if (fieldName == null) throw new ArgumentException("Can't instantiate an EncoderTuple " +
                 " with a null fieldName");
            if (e == null) throw new ArgumentException("Can't instantiate an EncoderTuple " +
                 " with a null Encoder");
        }

        public string GetFieldName()
        {
            return (string) Get(0);
        }

        /**
         * Returns the {@link Encoder}'s name
         * @return
         */
        public string GetName()
        {
            return (string)Get(1);
        }

        /**
         * Returns this {@link Encoder}
         * @return
         */
        public IEncoder GetEncoder()
        {
            return (IEncoder)Get(2);
        }

        public T GetEncoder<T>()
        {
            return (T)Get(2);
        }

        /**
         * Returns the index of the first on bit (offset)
         * the {@link Encoder} encodes.
         * @return
         */
        public int GetOffset()
        {
            return (int)Get(3);
        }
    }
}