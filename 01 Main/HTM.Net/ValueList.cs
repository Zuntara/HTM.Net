using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net
{
    public interface IValueList
    {
        /// <summary>
        /// Returns a collection of values in the form of a {@link Tuple}
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        Tuple GetRow(int row);

        /// <summary>
        /// Returns the number of rows.
        /// </summary>
        /// <returns></returns>
        int Size();
        /// <summary>
        /// Returns a flag indicating whether the current input state is set to learn or not.
        /// </summary>
        bool IsLearn();
        /// <summary>
        /// Returns a flag indicating whether any watched column has changed data.
        /// </summary>
        bool IsReset();
        /// <summary>
        /// Returns the header line containing the field types.
        /// </summary>
        List<FieldMetaType> GetFieldTypes();
    }
}