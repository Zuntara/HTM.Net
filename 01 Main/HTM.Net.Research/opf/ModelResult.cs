using System.Collections.Generic;

namespace HTM.Net.Research.opf
{
    public class ModelResult
    {
        public int? predictionNumber;
        public Dictionary<string, object> rawInput;
        public SensorInput sensorInput;
        public Dictionary<InferenceElement, object> inferences;
        public object metrics;
        public int? predictedFieldIdx;
        public string predictedFieldName;
        public object classifierInput;

        public ModelResult(int? predictionNumber = null,
            Dictionary<string, object> rawInput = null,
            SensorInput sensorInput = null,
            Dictionary<InferenceElement, object> inferences = null,
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


    }
}