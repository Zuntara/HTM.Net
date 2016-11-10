using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net.Research.opf
{
    public class ModelResult
    {
        public int? predictionNumber;
        public Map<string, object> rawInput;
        public SensorInput sensorInput;
        public Map<InferenceElement, object> inferences;
        public object metrics;
        public int? predictedFieldIdx;
        public string predictedFieldName;
        public object classifierInput;

        public ModelResult(int? predictionNumber = null,
            Map<string, object> rawInput = null,
            SensorInput sensorInput = null,
            Map<InferenceElement, object> inferences = null,
            object metrics = null,
            int? predictedFieldIdx = null,
            string predictedFieldName = null,
            object classifierInput = null)
        {
            this.predictionNumber = predictionNumber;
            this.rawInput = rawInput;
            this.sensorInput = sensorInput;
            this.inferences = inferences;
            this.metrics = metrics;
            this.predictedFieldIdx = predictedFieldIdx;
            this.predictedFieldName = predictedFieldName;
            this.classifierInput = classifierInput;
        }


        public ModelResult Clone()
        {
            return new ModelResult(predictionNumber, rawInput, sensorInput, inferences, metrics, predictedFieldIdx, predictedFieldName, classifierInput);
        }
    }
}