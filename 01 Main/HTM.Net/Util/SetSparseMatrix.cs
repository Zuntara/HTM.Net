

using System.Collections.Generic;

namespace HTM.Net.Util
{
    public class SetSparseMatrix : AbstractSparseMatrix<int>
    {
         private SortedSet<int> indexes = new SortedSet<int>();

        public SetSparseMatrix(int[] dimensions)
            : this(dimensions, false)
        {
            
        }

        public SetSparseMatrix(int[] dimensions, bool useColumnMajorOrdering)
            : base(dimensions, useColumnMajorOrdering)
        {
            
        }

        public override AbstractSparseMatrix<int> Set(int[] coordinates, int value)
        {
            return (AbstractSparseMatrix<int>) Set(ComputeIndex(coordinates), value);

        }

         public override int Get(int index)
        {
            return this.indexes.Contains(index) ? 0 : 1;
        }

        public override IFlatMatrix<int> Set(int index, int value)
        {
            if (value > 0)
                this.indexes.Add(index);

            return this;
        }
    }
}