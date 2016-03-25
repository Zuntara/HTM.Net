using System.Collections.Generic;
using System.Linq;
using HTM.Net.Model;

namespace HTM.Net
{
    /**
 * <p>
 * For now, a utility class for convenience operations
 * on integer arrays understood to be algorithmic inputs
 * and outputs; and conversions to and from canonical objects.
 * </p><p>
 * Later, this may become the encapsulation of the vectors
 * representing SDRs and previously treated as integer arrays.
 * </p><p>
 * <b>NOTE:</b> <em>Eclipse is not up to date with its leakable resource inspection.
 * Streams not derived from channels (i.e. from arrays or lists) do not 
 * need explicit closing.</em>
 * </p>
 * <p>
 * see here: http://stackoverflow.com/questions/25796118/java-8-streams-and-try-with-resources 
 * </p>
 * @author cogmission
 */
    public class SDR
    {

        /**
         * Converts a vector of {@link Cell} indexes to {@link Column} indexes.
         * 
         * @param cells             the indexes of the cells to convert
         * @param cellsPerColumn    the defined number of cells per column  
         *                          false if not.   
         * @return  the column indexes of the specified cells.
         */
        public static int[] AsColumnIndices(int[] cells, int cellsPerColumn)
        { 
            return cells.Select(cell=>cell / cellsPerColumn).Distinct().ToArray();
        }

        /**
         * Converts a vector of {@link Cell} indexes to {@link Column} indexes.
         * 
         * @param cells             the indexes of the cells to convert
         * @param cellsPerColumn    the defined number of cells per column  
         *                          false if not.   
         * @return  the column indexes of the specified cells.
         */
        public static int[] AsColumnIndices(List<int> cells, int cellsPerColumn)
        {
            var op = cells.Select(c => c);
            
            return op.Select(cellIdx=>cellIdx / cellsPerColumn).Distinct().ToArray();
        }

        /**
         * Converts a List of {@link Cell}s to {@link Column} indexes.
         * 
         * @param cells             the list of cells to convert
         * @param cellsPerColumn    the defined number of cells per column  
         *                          false if not.   
         * @return  the column indexes of the specified cells.
         */
        public static int[] CellsToColumns(IEnumerable<Cell> cells, int cellsPerColumn)
        {
            var op = cells.Select(c => c.GetIndex());

            return op.Select(cellIdx => cellIdx / cellsPerColumn).Distinct().ToArray();
        }

        /**
         * Converts a Set of {@link Cell}s to {@link Column} indexes.
         * 
         * @param cells             the list of cells to convert
         * @param cellsPerColumn    the defined number of cells per column  
         * 
         * @return  the column indexes of the specified cells.
         */
        public static int[] CellsAsColumnIndices(IEnumerable<Cell> cells, int cellsPerColumn)
        {
            return cells.Select(c => c.GetIndex()).OrderBy(i => i).Select(ci => ci / cellsPerColumn).Distinct().ToArray();
            //return cells.stream().mapToInt(c->c.getIndex()).sorted().map(cellIdx->cellIdx / cellsPerColumn).distinct().toArray();
        }

        /**
         * Converts a {@link Collection} of {@link Cell}s to a list
         * of cell indexes.
         * 
         * @param cells
         * @return
         */
        public static int[] AsCellIndices(IEnumerable<Cell> cells)
        {
            return cells.Select(c => c.GetIndex()).OrderBy(i => i).ToArray();
            //return cells.stream().mapToInt(cell=>cell.getIndex()).sorted().toArray();
        }
    }
}