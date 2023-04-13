using HTM.Net.Util;

namespace HTM.Net.Research.opf;

public class ModelResult
{
    public int? predictionNumber;
    public Map<string, object> rawInput;
    public SensorInput sensorInput;
    public Map<InferenceElement, object> inferences;
    public Map<string, double?> metrics;
    public int? predictedFieldIdx;
    public string predictedFieldName;
    public ClassifierInput classifierInput;

    public ModelResult(int? predictionNumber = null,
        Map<string, object> rawInput = null,
        SensorInput sensorInput = null,
        Map<InferenceElement, object> inferences = null,
        Map<string, double?> metrics = null,
        int? predictedFieldIdx = null,
        string predictedFieldName = null,
        ClassifierInput classifierInput = null)
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

    #region Overrides of Object

    public override string ToString()
    {
        return $"ModelResult(\tpredictionNumber={predictionNumber}\n\trawInput={rawInput}\n\tsensorInput={sensorInput}\n\tinferences={inferences}" +
               $"\n\tmetrics={metrics}\n\tpredictedFieldIdx={predictedFieldIdx}\n\tpredictedFieldName={predictedFieldName}\n\tclassifierInput={classifierInput}\n)";
    }

    #endregion

    public ModelResult Clone()
    {
        return new ModelResult(predictionNumber, rawInput, sensorInput, inferences, metrics, predictedFieldIdx, predictedFieldName, classifierInput);
    }
}