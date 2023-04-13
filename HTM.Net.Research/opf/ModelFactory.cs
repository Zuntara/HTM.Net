using System;
using HTM.Net.Research.Swarming.Descriptions;

namespace HTM.Net.Research.opf
{
    public class ModelFactory
    {
        public static Model Create(ExperimentParameters modelConfig)
        {
            Type modelClass;
            if (modelConfig.Model == "CLA")
            {
                modelClass = typeof(CLAModel);
            }
            else if (modelConfig.Model == "TwoGram")
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.Model);
            }
            else if (modelConfig.Model == "PreviousValue")
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.Model);
            }
            else
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.Model);
            }
            return (Model)Activator.CreateInstance(modelClass, modelConfig);
        }
    }
}