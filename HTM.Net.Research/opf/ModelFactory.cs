using System;
using HTM.Net.Research.Swarming;

namespace HTM.Net.Research.opf
{
    public class ModelFactory
    {
        public static Model create(DescriptionConfigModel modelConfig)
        {
            Type modelClass = null;
            if (modelConfig.model == "CLA")
            {
                modelClass = typeof(CLAModel);
            }
            else if (modelConfig.model == "TwoGram")
            {

            }
            else if (modelConfig.model == "PreviousValue")
            {

            }
            else
            {
                throw new NotSupportedException("ModelFactory received unsupported Model type: " + modelConfig.model);
            }
            return (Model)Activator.CreateInstance(modelClass, modelConfig.modelParams);
        }
    }
}