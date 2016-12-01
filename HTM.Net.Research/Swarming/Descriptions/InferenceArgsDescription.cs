using System;

namespace HTM.Net.Research.Swarming.Descriptions
{
    [Serializable]
    public class InferenceArgsDescription
    {
        public bool? useReconstruction;

        /// <summary>
        /// A list of integers that specifies which steps size(s) to learn/infer on
        /// </summary>
        public int[] predictionSteps { get; set; } = new[] { 1 };
        /// <summary>
        /// Name of the field being optimized for during prediction
        /// </summary>
        public string predictedField { get; set; }
        /// <summary>
        /// Whether or not to use the predicted field as an input. When set to 'auto', 
        /// swarming will use it only if it provides better performance. 
        /// When the inferenceType is NontemporalClassification, this value is forced to 'no'
        /// </summary>
        public InputPredictedField? inputPredictedField { get; set; }

        public InferenceArgsDescription Clone()
        {
            return new InferenceArgsDescription
            {
                predictionSteps = (int[])predictionSteps.Clone(),
                predictedField = predictedField,
                inputPredictedField = inputPredictedField
            };
        }
    }
}