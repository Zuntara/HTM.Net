using System;
using System.Collections.Generic;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;

namespace HTM.Net.Network
{
    /**
 * Container for output from a given {@link Layer}. Represents the
 * result accumulated by the computation of a sequence of algorithms
 * contained in a given Layer and contains information needed at 
 * various stages in the sequence of calculations a Layer may contain.
 * 
 */
    public interface IInference : IPersistable
    {
        /**
         * Returns the input record sequence number associated with 
         * the state of a {@link Layer} which this {@code Inference}
         * represents.
         * 
         * @return
         */
         int GetRecordNum();
        /**
         * Returns the {@link ComputeCycle}
         * @return
         */
         ComputeCycle GetComputeCycle();
        /**
         * Returns a custom Object during sequence processing where one or more 
         * {@link Func1}(s) were added to a {@link Layer} in between algorithmic
         * components.
         *  
         * @return  the custom object set during processing
         */
         object GetCustomObject();
        /**
         * Returns the {@link Map} used as input into a given {@link CLAClassifier}
         * if it exists.
         * 
         * @return
         */
        Map<string, NamedTuple> GetClassifierInput();
        /**
         * Returns a tuple containing key/value pairings of input field
         * names to the {@link CLAClassifier} used in conjunction with it.
         * 
         * @return
         */
         NamedTuple GetClassifiers();
        /**
         * Returns the object used as input into a given Layer
         * which is associated with this computation result.
         * @return
         */
         object GetLayerInput();

        /**
         * Returns the <em>Sparse Distributed Representation</em> vector
         * which is the result of all algorithms in a series of algorithms
         * passed up the hierarchy.
         * 
         * @return
         */
         int[] GetSdr();
        /**
         * Returns the initial encoding produced by an {@link Encoder} or one
         * of its subtypes.
         * 
         * @return
         */
         int[] GetEncoding();

        /// <summary>
        /// Returns the most recent <see cref="Classification{T}"/>
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        IClassification<object> GetClassification(string fieldName);
        /// <summary>
        /// Returns the most recent anomaly calculation.
        /// </summary>
        /// <returns></returns>
        double GetAnomalyScore();
        /// <summary>
        /// Returns the column activation from a <see cref="SpatialPooler"/>
        /// </summary>
        /// <returns></returns>
        int[] GetFeedForwardActiveColumns();
        /// <summary>
        /// Returns the column activations in sparse form
        /// </summary>
        int[] GetFeedForwardSparseActives();
        /// <summary>
        /// Returns the column activation from a <see cref="TemporalMemory"/>
        /// </summary>
        /// <returns></returns>
        HashSet<Cell> GetActiveCells();
        /// <summary>
        /// Returns the predicted output from the last inference cycle.
        /// </summary>
        /// <returns></returns>
        HashSet<Cell> GetPreviousPredictiveCells();
        /// <summary>
        /// Returns the currently predicted columns.
        /// </summary>
        /// <returns></returns>
        HashSet<Cell> GetPredictiveCells();
    }
}