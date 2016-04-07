using System;
using HTM.Net.Util;

namespace HTM.Net.Network.Sensor
{
    /// <summary>
    /// Adds meta information retrieval to a <see cref="IStream{T}"/>
    /// </summary>
    public interface IMetaStream
    {
        /// <summary>
        /// Returns a <see cref="IValueList"/> containing meta information (i.e. header information)
        /// which can be used to infer the structure of the underlying stream.
        /// </summary>
        /// <returns> a <see cref="IValueList"/> describing meta features of this stream.</returns>
        IValueList GetMeta();

        /// <summary>
        /// <p>
        /// Returns a flag indicating whether the underlying stream has had
        /// a terminal operation called on it, indicating that it can no longer
        /// have operations built up on it.
        /// </p>
        /// <p>
        /// The "terminal" flag if true does not indicate that the stream has reached
        /// the end of its data, it just means that a terminating operation has been
        /// invoked and that it can no longer support intermediate operation creation.
        /// </p>
        /// </summary>
        /// <returns>true if terminal, false if not.</returns>
        bool IsTerminal();

        bool IsParallel();

        IBaseStream Map(Func<string[], int[]> mapFunc);
        void ForEach(Action<object> action);
        long Count();
        /// <summary>
        /// Returns true when a string[] to int[] conversion is needed (when the raw input is string)
        /// </summary>
        /// <returns></returns>
        bool NeedsStringMapping();

        IBaseStream DoStreamMapping();
    }
}