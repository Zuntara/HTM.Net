using HTM.Net.Model;

namespace HTM.Net.Monitor
{
    /// <summary>
    /// Decorator interface for main algorithms 
    /// </summary>
    public interface IComputeDecorator
    {
        /// <summary>
        /// Feeds input record through TM, performing inferencing and learning
        /// </summary>
        /// <param name="connections">the connection memory</param>
        /// <param name="activeColumns">direct activated column input</param>
        /// <param name="learn">learning mode flag</param>
        /// <returns>{@link ComputeCycle} container for one cycle of inference values.</returns>
        ComputeCycle Compute(Connections connections, int[] activeColumns, bool learn);

        /// <summary>
        /// Called to start the input of a new sequence, and reset the sequence state of the TM.
        /// </summary>
        /// <param name="connections">the Connections state of the temporal memory</param>
        void Reset(Connections connections);
    }
}