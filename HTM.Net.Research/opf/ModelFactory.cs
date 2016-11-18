using System;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;

namespace HTM.Net.Research.opf
{
    public class ModelFactory
    {
        public static Model Create(ConfigModelDescription modelConfig)
        {
            Type modelClass;
            if (modelConfig.model == "CLA")
            {
                modelClass = typeof(CLAModel);
            }
            else if (modelConfig.model == "TwoGram")
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.model);
            }
            else if (modelConfig.model == "PreviousValue")
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.model);
            }
            else
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.model);
            }
            return (Model)Activator.CreateInstance(modelClass, modelConfig);
        }
    }
}