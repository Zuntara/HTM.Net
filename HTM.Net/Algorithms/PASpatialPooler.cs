using HTM.Net.Util;

namespace HTM.Net.Algorithms
{
    /**
 * Subclasses {@link SpatialPooler} to perform Prediction-Assisted CLA
 *
 * @author David Ray
 * @author Fergal Byrne
 *
 */
    public class PASpatialPooler : SpatialPooler
    {
    /**
     * This function determines each column's overlap with the current input
     * vector. The overlap of a column is the number of synapses for that column
     * that are connected (permanence value is greater than '_synPermConnected')
     * to input bits which are turned on. Overlap values that are lower than
     * the 'stimulusThreshold' are ignored. The implementation takes advantage of
     * the SpraseBinaryMatrix class to perform this calculation efficiently.
     *
     * @param c				the {@link Connections} memory encapsulation
     * @param inputVector   an input array of 0's and 1's that comprises the input to
     *                      the spatial pooler.
     * @return
     */
    public override int[] CalculateOverlap(Connections c, int[] inputVector)
    {
        int[] overlaps = new int[c.GetNumColumns()];
        c.GetConnectedCounts().RightVecSumAtNZ(inputVector, overlaps);
        int[] paOverlaps = ArrayUtils.ToIntArray(c.GetPAOverlaps());
        overlaps = ArrayUtils.Add(paOverlaps, overlaps);
        ArrayUtils.LessThanXThanSetToY(overlaps, (int)c.GetStimulusThreshold(), 0);
        return overlaps;
    }

}
}