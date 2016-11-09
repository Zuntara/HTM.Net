using System;
using System.Collections;
using System.Collections.Generic;
using HTM.Net.Research.opf;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Data
{
    /// <summary>
    /// TimeShifter class for shifting ModelResults.
    /// Shifts time for ModelResult objects.
    /// </summary>
    public class InferenceShifter
    {
        private Deque<Dictionary<InferenceElement, object>> _inferenceBuffer;

        public InferenceShifter()
        {
            _inferenceBuffer = null;
        }

        /// <summary>
        /// Shift the model result and return the new instance.
        /// 
        /// Queues up the T(i+1) prediction value and emits a T(i)
        /// input/prediction pair, if possible. E.g., if the previous T(i-1)
        /// iteration was learn-only, then we would not have a T(i) prediction in our
        /// FIFO and would not be able to emit a meaningful input/prediction pair.
        /// </summary>
        /// <param name="modelResult"></param>
        public ModelResult Shift(ModelResult modelResult)
        {
            Map<InferenceElement, object> inferencesToWrite = new Map<InferenceElement, object>();
            if (_inferenceBuffer == null)
            {
                var maxDelay = InferenceElementHelper.GetMaxDelay(modelResult.inferences);
                _inferenceBuffer = new Deque<Dictionary<InferenceElement, object>>(maxDelay + 1);
            }
            _inferenceBuffer.Insert(new Dictionary<InferenceElement, object>(modelResult.inferences));

            foreach (var iPair in modelResult.inferences)
            {
                InferenceElement inferenceElement = iPair.Key;
                object inference = iPair.Value;
                if (inference is IDictionary)
                {
                    inferencesToWrite[inferenceElement] = null;
                    foreach (DictionaryEntry dictItem in (IDictionary) inference)
                    {
                        var key = dictItem.Key;
                        var delay = InferenceElementHelper.GetTemporalDelay(inferenceElement, key);
                        if (_inferenceBuffer.Length > delay)
                        {
                            var prevInference = ((IDictionary)_inferenceBuffer.ToArray()[delay][inferenceElement])[key];
                            if (inferencesToWrite[inferenceElement] == null)
                            {
                                // ensure there is an instance in place to put the key value in
                                inferencesToWrite[inferenceElement] = Activator.CreateInstance(inference.GetType());
                            }
                            ((IDictionary)inferencesToWrite[inferenceElement])[key] = prevInference;
                        }
                        else
                        {
                            if (inferencesToWrite[inferenceElement] == null)
                            {
                                // ensure there is an instance in place to put the key value in
                                inferencesToWrite[inferenceElement] = Activator.CreateInstance(inference.GetType());
                            }
                            ((IDictionary)inferencesToWrite[inferenceElement])[key] = null;
                        }
                    }
                }
                else
                {
                    var delay = InferenceElementHelper.GetTemporalDelay(inferenceElement);
                    if (_inferenceBuffer.Length > delay)
                    {
                        inferencesToWrite[inferenceElement] = _inferenceBuffer.ToArray()[delay][inferenceElement];
                    }
                    else
                    {
                        if (inference is Tuple || inference is IList)
                        {
                            if (inference is Tuple)
                                inferencesToWrite[inferenceElement] = new object[((Tuple) inference).Count];
                            if (inference is IList)
                                inferencesToWrite[inferenceElement] = new object[((IList) inference).Count];
                        }
                        else
                        {
                            inferencesToWrite[inferenceElement] = null;
                        }
                    }
                }
            }

            var shiftedResult = new ModelResult(rawInput: modelResult.rawInput,
                sensorInput: modelResult.sensorInput,
                inferences: inferencesToWrite,
                metrics: modelResult.metrics,
                predictedFieldIdx: modelResult.predictedFieldIdx,
                predictedFieldName: modelResult.predictedFieldName);
            return shiftedResult;
        }
    }
}