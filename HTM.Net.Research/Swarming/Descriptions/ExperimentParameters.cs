using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Util;
using Newtonsoft.Json;

namespace HTM.Net.Research.Swarming.Descriptions
{
    [Serializable]
    public class ExperimentParameters : Parameters
    {
        [JsonProperty("GroupedEncodersAlready")]
        private bool _groupedEncoders = false;

        private bool? _enableClassification;

        [JsonConstructor]
        protected ExperimentParameters()
        {
            Control = new ExperimentControl();
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            // Spatial defaults
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.GLOBAL_INHIBITION, true);
            SetParameterByKey(KEY.COLUMN_DIMENSIONS, new[] { 2048 });
            SetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 40.0);
            SetParameterByKey(KEY.SEED, 1956);
            SetParameterByKey(KEY.RANDOM, new XorshiftRandom((int)1956));
            SetParameterByKey(KEY.SYN_PERM_ACTIVE_INC, 0.05);
            SetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC, 0.0005);
            SetParameterByKey(KEY.MAX_BOOST, 1.0);
            // Temporal defaults
            SetParameterByKey(KEY.CELLS_PER_COLUMN, 32);
            SetParameterByKey(KEY.INPUT_DIMENSIONS, new[] { 2048 });
            SetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT, 20);
            SetParameterByKey(KEY.MAX_SYNAPSES_PER_SEGMENT, 32);
            SetParameterByKey(KEY.MAX_SEGMENTS_PER_CELL, 128);
            SetParameterByKey(KEY.INITIAL_PERMANENCE, 0.21);
            SetParameterByKey(KEY.PERMANENCE_INCREMENT, 0.1);
            SetParameterByKey(KEY.PERMANENCE_DECREMENT, 0.1);
            //SetParameterByKey(KEY.GLOBAL_DECAY, 0);
            //SetParameterByKey(KEY.MAX_AGE, 0);
            SetParameterByKey(KEY.MIN_THRESHOLD, 12);
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, 16);
            //SetParameterByKey(KEY.PAM_LENGTH, 1);

            // Classifier params
            SetParameterByKey(KEY.CLASSIFIER_ALPHA, 0.001);
            SetParameterByKey(KEY.AUTO_CLASSIFY, true);
            SetParameterByKey(KEY.CLASSIFIER_STEPS, new[] { 1 });
        }

        public static ExperimentParameters Default()
        {
            return new ExperimentParameters();
        }

        public ExperimentControl Control { get; set; }

        public string Environment { get; set; } = "Nupic";

        /// <summary>
        /// Type of model that the rest of these parameters apply to.
        /// </summary>
        public string Model { get; set; } = "CLA";
        /// <summary>
        /// The type of inference that this model will perform
        /// </summary>
        public InferenceType InferenceType { get; set; }
        public bool EnableSpatialPooler { get; set; }
        /// <summary>
        /// Controls whether TP is enabled or disabled;
        /// TP is necessary for making temporal predictions, such as predicting
        /// the next inputs.  Without TP, the model is only capable of
        /// reconstructing missing sensor inputs (via SP).
        /// </summary>
        public bool EnableTemporalMemory { get; set; }

        public bool EnableClassification
        {
            get
            {
                if (_enableClassification == null)
                {
                    _enableClassification = (bool)GetParameterByKey(KEY.AUTO_CLASSIFY);
                }

                return _enableClassification.GetValueOrDefault();
            }
            set
            {
                _enableClassification = value;
                SetParameterByKey(KEY.AUTO_CLASSIFY, value);
            }
        }

        /// <summary>
        /// A dictionary specifying the period for automatically-generated
        /// resets from a RecordSensor;
        ///
        /// None = disable automatically-generated resets (also disabled if
        /// all of the specified values evaluate to 0).
        /// Valid keys is the desired combination of the following:
        ///   days, hours, minutes, seconds, milliseconds, microseconds, weeks
        ///
        /// Example for 1.5 days: sensorAutoReset = dict(days=1,hours=12),
        /// </summary>
        public AggregationSettings SensorAutoReset { get; set; }
        /// <summary>
        /// Intermediate variables used to compute fields in modelParams and also
        /// referenced from the control section.
        /// </summary>
        public AggregationSettings AggregationInfo { get; set; }

        public bool TrainSPNetOnlyIfRequested { get; set; }
        public AggregationSettings PredictAheadTime { get; set; }


        public EncoderSettingsList GetEncoderSettings()
        {
            if (_groupedEncoders)
            {
                return (EncoderSettingsList)GetParameterByKey(KEY.FIELD_ENCODING_MAP);
            }
            _groupedEncoders = true;

            // Lookup DateEncoders and group them if needed
            EncoderSettingsList list = new EncoderSettingsList((Map<string, Map<string, object>>)GetParameterByKey(KEY.FIELD_ENCODING_MAP));

            var selection = list.Where(e => e.Key.Contains("_") && e.Value?.GetEncoderType() == EncoderTypes.DateEncoder).ToList();
            var grouped = selection.GroupBy(k => k.Value.fieldName, e => e.Key);
            if (selection.Count > 1)
            {
                foreach (var grouping in grouped)
                {
                    string fieldName = grouping.Key;
                    EncoderSetting setting = new EncoderSetting();
                    setting.encoderType = EncoderTypes.DateEncoder;
                    setting.fieldName = fieldName;

                    foreach (string name in grouping)
                    {
                        if (name.EndsWith("timeOfDay"))
                        {
                            setting.timeOfDay = selection.Single(s => s.Key == name).Value.timeOfDay;
                        }
                        else if (name.EndsWith("dayOfWeek"))
                        {
                            setting.dayOfWeek = selection.Single(s => s.Key == name).Value.dayOfWeek;
                        }
                        else if (name.EndsWith("weekend"))
                        {
                            setting.weekend = selection.Single(s => s.Key == name).Value.weekend;
                        }
                        else if (name.EndsWith("season"))
                        {
                            setting.season = selection.Single(s => s.Key == name).Value.season;
                        }
                        else if (name.EndsWith("holiday"))
                        {
                            setting.holiday = selection.Single(s => s.Key == name).Value.holiday;
                        }
                        list.Remove(name);
                    }
                    list.Add(fieldName, setting);
                }
            }

            return new EncoderSettingsList((Map<string, Map<string,object>>)GetParameterByKey(KEY.FIELD_ENCODING_MAP));
        }

        public ExperimentParameters Union(ExperimentParameters p)
        {
            foreach (KEY k in p.Keys())
            {
                SetParameterByKey(k, p.GetParameterByKey(k));
            }
            return this;
        }

        public new ExperimentParameters Copy()
        {
            var p = new ExperimentParameters().Union(this);
            p.InferenceType = InferenceType;
            p.EnableSpatialPooler = EnableSpatialPooler;
            p.EnableTemporalMemory = EnableTemporalMemory;
            p.EnableClassification = EnableClassification;
            p.SensorAutoReset = SensorAutoReset?.Clone();
            p.AggregationInfo = AggregationInfo?.Clone();
            p.TrainSPNetOnlyIfRequested = TrainSPNetOnlyIfRequested;
            p.Control = Control.Clone();

            return p;
        }

        public void OverrideWith(ExperimentPermutationParameters @params)
        {
            // Overload the normal values
            foreach (KEY key in @params.Keys())
            {
                var value = @params.GetParameterByKey(key);
                if (value != null)
                {
                    SetParameterByKey(key, value);
                }
            }

            // Loose field that can be permuted over
            if (@params.InferenceType != null)
            {
                InferenceType = (InferenceType)TypeConverter.Convert<int>(@params.InferenceType);
            }

            // Encoder overloading
            var encoders = GetEncoderSettings();
            foreach (KeyValuePair<string, object> pair in @params.Encoders)
            {
                string name = pair.Key;
                EncoderSetting setting = pair.Value as EncoderSetting;
                if (setting != null)
                {
                    var origSetting = encoders.Get(name);
                    if(Debugger.IsAttached && origSetting==null) Debugger.Break();
                    foreach (string subKey in setting.Keys)
                    {
                        var obj = setting[subKey];
                        if (!obj.Equals(origSetting[subKey]))
                        {
                            origSetting[subKey] = obj;
                        }
                    }
                    encoders[name] = origSetting;
                }
            }
        }
    }
}