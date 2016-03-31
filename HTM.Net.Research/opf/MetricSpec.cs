using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.Swarming;

namespace HTM.Net.Research.opf
{
    public class MetricSpec
    {
        private string metric;
        private InferenceElement inferenceElement;
        private string field;
        private IDictionary @params;

        public MetricSpec(string metric, InferenceElement inferenceElement, string field = null, IDictionary @params = null)
        {
            this.metric = metric;
            this.inferenceElement = inferenceElement;
            this.field = field;
            this.@params = @params;
        }

        /// <summary>
        /// Helper method that generates a unique label for a MetricSpec / InferenceType pair.The label is formatted as follows:
        /// <predictionKind>:<metric type>:(paramName= value)*:field=<fieldname>
        /// For example:
        ///     classification:aae:paramA=10.2:paramB=20:window=100:field=pounds
        /// </summary>
        /// <param name="inferenceType"></param>
        /// <returns></returns>
        public string getLabel(InferenceType? inferenceType = null)
        {

            var result = new List<string>();
            if (inferenceType.HasValue)
            {
                result.Add(inferenceType.ToString());
            }
            result.Add(this.inferenceElement.ToString());
            result.Add(this.metric);

            var @params = this.@params;
            if (@params != null)
            {
                var sortedParams = @params.Keys;
                //sortedParams.Sort();
                foreach (var param in sortedParams)
                {
                    // Don't include the customFuncSource - it is too long an unwieldy
                    if (new[] {"customFuncSource", "customFuncDef", "customExpr"}.Contains(param))
                    {
                        continue;
                    }

                    var value = @params[param];
                    if (value is string)
                    {
                        result.Add(string.Format("{0}={1}", param, value));
                    }
                    else
                    {
                        result.Add(string.Format("{0}={1}", param, value));
                    }
                }
            }

            if (this.field != null)
                result.Add("field="+ this.field);

            return string.Join("", result);
        }


    }

    public enum InferenceElement
    {
        Prediction,
        Classification,
        ClassConfidences,
        Encodings,
        AnomalyLabel,
        AnomalyScore,
        MultiStepPredictions,
        MultiStepBestPredictions,
        MultiStepBucketLikelihoods
    }
}