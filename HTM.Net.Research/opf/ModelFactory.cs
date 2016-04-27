using System;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;

namespace HTM.Net.Research.opf
{
    public class ModelFactory
    {
        public static Model Create(IDescription modelConfig)
        {
            Type modelClass;
            if (modelConfig.modelConfig.model == "CLA")
            {
                modelClass = typeof(CLAModel);
            }
            else if (modelConfig.modelConfig.model == "TwoGram")
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.modelConfig.model);
            }
            else if (modelConfig.modelConfig.model == "PreviousValue")
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.modelConfig.model);
            }
            else
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.modelConfig.model);
            }
            return (Model)Activator.CreateInstance(modelClass, modelConfig);
        }
    }
}