using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using HTM.Net.Network.Sensor;
using HTM.Net.Util;
using Newtonsoft.Json;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Swarming.Descriptions
{
    public interface IDescription
    {
        string Type { get; set; }

        DescriptionConfigModel modelConfig { get; set; }
        DescriptionControlModel control { get; set; }
        Map<string, object> inferenceArgs { get; set; }
        Map<string, Tuple<FieldMetaType, SensorFlags>> inputRecordSchema { get; set; }
        IDescription Clone();

        Network.Network BuildNetwork();

        Parameters GetParameters();
    }

    [JsonConverter(typeof(TypedDescriptionBaseJsonConverter))]
    public abstract class DescriptionBase : IDescription
    {
        protected DescriptionBase()
        {
            Type = GetType().AssemblyQualifiedName;
        }

        public void applyValueGettersToContainer(object container)
        {
            _applyValueGettersImpl(container: container, currentObj: container,
                recursionStack: new Stack<object>());
        }

        private void _applyValueGettersImpl(object container, object currentObj, Stack<object> recursionStack)
        {
            // Detect cycles
            if (recursionStack.Contains(currentObj)) return;

            // Sanity-check of our cycle-detection logic
            Debug.Assert(recursionStack.Count < 1000);

            // Push the current object on our cycle-detection stack
            recursionStack.Push(currentObj);

            // Resolve value-getters within dictionaries, tuples and lists
            if (currentObj is DescriptionConfigModel)
            {
                foreach (var pair in ((DescriptionConfigModel)currentObj).GetDictionary())
                {
                    //if (isinstance(value, ValueGetterBase))
                    if (HasValueGetter(pair.Value))
                    {
                        ((DescriptionConfigModel)currentObj).SetDictValue(pair.Key, ExecValueGetter(container));
                        //currentObj[pair.Key] = value(container);
                    }

                    _applyValueGettersImpl(container, ((DescriptionConfigModel)currentObj)[pair.Key], recursionStack);
                }
            }
            else if (currentObj is IDictionary)
            {
                foreach (DictionaryEntry pair in (IDictionary)currentObj)
                {
                    if (HasValueGetter(pair.Value))
                    {
                        ((IDictionary)currentObj)[pair.Key] = container;
                        //currentObj[key] = value(container)
                    }

                    _applyValueGettersImpl(container, ((IDictionary)currentObj)[pair.Key], recursionStack);
                }
            }
            else if (currentObj is Tuple)
            {
                //Tuple tuple = currentObj as Tuple;
                //int i = 0;
                //foreach (object item in tuple)
                //{
                //    if (HasValueGetter(item))
                //    {
                //        tuple[i] = ExecValueGetter(container);
                //    }
                //    i++;
                //    _applyValueGettersImpl(container, currentObj[i], recursionStack);

                //}
                throw new NotImplementedException();
            }
            else if (currentObj is IList)
            {
                IList list = currentObj as IList;
                int i = 0;
                foreach (object item in list)
                {
                    if (HasValueGetter(item))
                    {
                        list[i] = ExecValueGetter(container);
                    }
                    i++;
                    _applyValueGettersImpl(container, list[i], recursionStack);

                }
            }
            //else if (isinstance(currentObj, tuple) || isinstance(currentObj, list))
            //{
            //    foreach (var item in enumerate(currentObj)) // (i, value) 
            //    {
            //        // NOTE: values within a tuple should never be value-getters, since
            //        //       the top-level elements within a tuple are immutable. However,
            //        //       if any nested sub-elements might be mutable
            //        if (HasValueGetter(value))
            //        {
            //            currentObj[i] = value(container)
            //        }

            //        _applyValueGettersImpl(container, currentObj[i], recursionStack);
            //    }
            //}

            recursionStack.Pop();
        }

        private bool HasValueGetter(object obj)
        {
            return obj.GetType().GetProperty("Item") != null;
        }

        private object ExecValueGetter(object obj)
        {
            return obj.GetType().GetProperty("Item").GetValue(obj);
        }

        public abstract IDescription Clone();

        public abstract Network.Network BuildNetwork();

        public abstract Parameters GetParameters();

        public string Type { get; set; }

        public DescriptionConfigModel modelConfig { get; set; }
        public DescriptionControlModel control { get; set; }

        public Map<string, object> inferenceArgs { get; set; }
        public Map<string, Tuple<FieldMetaType, SensorFlags>> inputRecordSchema { get; set; }
    }

    

    ///// <summary>
    ///// This is the base interface class for description API classes which provide
    ///// OPF configuration parameters.
    ///// This mechanism abstracts description API from the specific description objects
    ///// created by the individiual users.
    ///// </summary>
    //public abstract class DescriptionBaseInterface
    //{
    //    protected DescriptionBaseInterface(DescriptionConfigModel modelConfig, DescriptionControlModel control)
    //    {
            
    //    }

    //    public abstract object getModelDescription();
    //    public abstract object getModelControl();
    //    public abstract object normalizeStreamSources();
    //    public abstract object convertNupicEnvToOPF();
    //}

    //public class ExperimentDescriptionAPI : DescriptionBaseInterface
    //{
    //    private DescriptionConfigModel _modelConfig;
    //    private DescriptionControlModel _control;
    //    private const string OpfEnvironmentExperiment = "experiment";
    //    private const string OpfEnvironmentNuPic = "nupic";

    //    public ExperimentDescriptionAPI(DescriptionConfigModel modelConfig, DescriptionControlModel control) 
    //        : base(modelConfig, control)
    //    {
    //        string environment = control.environment;
    //        if (environment == OpfEnvironmentExperiment)
    //        {
    //            __validateExperimentControl(control);
    //        }
    //        else if(environment == OpfEnvironmentNuPic)
    //        {
    //            __validateNupicControl(control);
    //        }
    //        _modelConfig = modelConfig;
    //        _control = control;
    //    }

    //    public override object getModelDescription()
    //    {
    //        if (_modelConfig.model == "CLA" && !_modelConfig.version.HasValue)
    //        {
    //            // The modelConfig is in the old CLA format, update it.
    //            //return __getCLAModelDescription();
    //        }
    //        return _modelConfig;
    //    }

    //    public override object getModelControl()
    //    {
    //        return _control;
    //    }

    //    public override object normalizeStreamSources()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public override object convertNupicEnvToOPF()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}