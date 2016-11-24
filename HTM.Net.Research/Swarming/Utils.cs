using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HTM.Net.Research.Swarming
{
    // Class helper for model params definitions (less casting needed)
    public class ModelParams
    {
        //public Dictionary<string, Dictionary<string, object>> structuredParams { get; set; }
        public PermutationModelParameters structuredParams { get; set; }
        public ParticleStateModel particleState { get; set; }

        /*
        modelParams is a dictionary containing the following elements:

            structuredParams: dictionary containing all variables for
                this model, with encoders represented as a dict within
                this dict (or None if they are not included.

            particleState: dictionary containing the state of this
                particle. This includes the position and velocity of
                each of it's variables, the particleId, and the particle
                generation index. It contains the following keys:

                id: The particle Id of the particle we are using to
                    generate/track this model. This is a string of the
                    form <hypesearchWorkerId>.<particleIdx>
                genIdx: the particle's generation index. This starts at 0
                    and increments every time we move the particle to a
                    new position.
                swarmId: The swarmId, which is a string of the form <encoder>.<encoder>... that describes this swarm
                varStates: dict of the variable states. The key is the
                    variable name, the value is a dict of the variable's
                    position, velocity, bestPosition, bestResult, etc.
        */
    }

    //public class StructuredParams
    //{
    //    public ModelDescriptionParams modelParams { get; set; }
    //}

    public static class Utils
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(Utils));

        /// <summary>
        /// This creates an experiment directory with a base.py description file
        /// created from 'baseDescription' and a description.py generated from the
        /// given params dict and then runs the experiment.
        /// </summary>
        /// <param name="modelID">ID for this model in the models table</param>
        /// <param name="jobID">ID for this hypersearch job in the jobs table</param>
        /// <param name="baseDescription">Contents of a description.py with the base experiment description</param>
        /// <param name="params">Dictionary of specific parameters to override within the baseDescriptionFile.</param>
        /// <param name="predictedField">Name of the input field for which this model is being optimized</param>
        /// <param name="reportKeys">Which metrics of the experiment to store into the results dict of the model's database entry</param>
        /// <param name="optimizeKey">Which metric we are optimizing for</param>
        /// <param name="jobsDAO">Jobs data access object - the interface to the jobs database which has the model's table.</param>
        /// <param name="modelCheckpointGUID">A persistent, globally-unique identifier for constructing the model checkpoint key</param>
        /// <param name="predictionCacheMaxRecords"></param>
        /// <returns>completion reason and msg</returns>
        public static ModelCompletionStatus runModelGivenBaseAndParams(ulong? modelID, uint? jobID,
            IDescription baseDescription, PermutationModelParameters @params,
            string predictedField, string[] reportKeys, string optimizeKey, BaseClientJobDao jobsDAO,
            string modelCheckpointGUID, int? predictionCacheMaxRecords = null)
        {
            //// --------------------------------------------------------------------------
            //// Create a temp directory for the experiment and the description files
            //string experimentDir = tempfile.mkdtemp();


            try
            {
                //logger.Info("Using experiment directory: %s" % (experimentDir));

                //    // Create the decription.py from the overrides in params
                //    string paramsFilePath = Path.Combine(experimentDir, "description.py");
                //    StreamWriter paramsFile = open(paramsFilePath, 'wb');
                //    //paramsFile.write(_paramsFileHead());

                string expDescription = Json.Serialize(@params);

                var cloneDescr = baseDescription.Clone();

                // Override parameter values
                if (@params?.modelParams?.clParams?.alpha != null)
                {
                    cloneDescr.modelConfig.modelParams.clParams.alpha = (double)@params.modelParams.clParams.alpha;
                }
                if (@params?.modelParams?.spParams?.synPermInactiveDec != null)
                {
                    cloneDescr.modelConfig.modelParams.spParams.synPermInactiveDec =
                        (double)@params.modelParams.spParams.synPermInactiveDec;
                }
                cloneDescr.modelConfig.modelParams.tpParams.activationThreshold =
                    TypeConverter.Convert<int>(@params.modelParams.tpParams.activationThreshold);
                cloneDescr.modelConfig.modelParams.tpParams.minThreshold =
                    TypeConverter.Convert<int>(@params.modelParams.tpParams.minThreshold);
                cloneDescr.modelConfig.modelParams.tpParams.pamLength =
                    TypeConverter.Convert<int>(@params.modelParams.tpParams.pamLength);

                Debug.WriteLine($">> clParams.alpha = {@params?.modelParams?.clParams?.alpha}");
                Debug.WriteLine($">> tpParams.activationThreshold = {@params.modelParams.tpParams.activationThreshold}");
                Debug.WriteLine($">> tpParams.minThreshold = {@params.modelParams.tpParams.minThreshold}");
                Debug.WriteLine($">> tpParams.pamLength = {@params.modelParams.tpParams.pamLength}");

                foreach (var encoderDict in cloneDescr.modelConfig.modelParams.sensorParams.encoders)
                {
                    string encoderName = encoderDict.Key;
                    Map<string, object> encoderValues = encoderDict.Value;

                    var permutedEncoder = (Map<string, object>)@params.modelParams.sensorParams.encoders[encoderName];
                    if (permutedEncoder == null) continue;
                    foreach (var pair in permutedEncoder)
                    {
                        // overload permuted encoder values when they are there
                        if (pair.Value != null)
                        {
                            Debug.WriteLine($">> sensorParams.{pair.Key} = {pair.Value}");
                            encoderValues[pair.Key] = pair.Value;
                        }
                    }
                }

                //    //items.sort();
                //    //for (key, value) in items
                //    foreach (var keyValue in @params.OrderBy(k => k.Key))
                //    {
                //        string quotedKey = _quoteAndEscape(key);

                //        if (keyValue.Value is string)
                //        {
                //            paramsFile.WriteLine("  {0} : '{1}',\n", quotedKey, keyValue.Value);
                //        }
                //        else
                //        {
                //            paramsFile.WriteLine("  {0} : {1},\n", quotedKey, keyValue.Value);
                //        }
                //    }

                //    //paramsFile.WriteLine(_paramsFileTail());
                //    paramsFile.Close();


                //    // Write out the base description
                //    StreamWriter baseParamsFile = open(Path.Combine(experimentDir, "base.py"), 'wb');
                //    baseParamsFile.WriteLine(baseDescription);
                //    baseParamsFile.Close();


                //    // Store the experiment's sub-description file into the model table
                //    //  for reference
                //    fd = open(paramsFilePath);
                //    expDescription = fd.read();
                //    fd.close();
                jobsDAO.modelSetFields(modelID, new Map<string, object> { { "genDescription", expDescription } });

                // Run the experiment now
                ModelCompletionStatus completionStatus;
                try
                {
                    var runner = new OpfModelRunner(
                        modelID: modelID,
                        jobID: jobID,
                        predictedField: predictedField,
                        experimentDir: cloneDescr,
                        reportKeyPatterns: reportKeys,
                        optimizeKeyPattern: optimizeKey,
                        jobsDAO: jobsDAO,
                        modelCheckpointGUID: modelCheckpointGUID,
                        predictionCacheMaxRecords: predictionCacheMaxRecords);

                    completionStatus = runner.run();
                    return completionStatus;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    if (Debugger.IsAttached) Debugger.Break();
                    throw;
                }

                //    try
                //    {
                //        runner = OPFModelRunner(
                //          modelID = modelID,
                //          jobID = jobID,
                //          predictedField = predictedField,
                //          experimentDir = experimentDir,
                //          reportKeyPatterns = reportKeys,
                //          optimizeKeyPattern = optimizeKey,
                //          jobsDAO = jobsDAO,
                //          modelCheckpointGUID = modelCheckpointGUID,
                //          logLevel = logLevel,
                //          predictionCacheMaxRecords = predictionCacheMaxRecords);

                //        signal.signal(signal.SIGINT, runner.handleWarningSignal);

                //        completionStatus = runner.run();
                //    }

                //    catch (InvalidConnectionException)
                //    {
                //        raise;
                //    }
                //    catch (Exception e)
                //    {

                //        completionStatus = _handleModelRunnerException(jobID,
                //                                       modelID, jobsDAO, experimentDir, logger, e);
                //    }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: " + ex);
                if (Debugger.IsAttached) Debugger.Break();
            }
            finally
            {
                //    // delete our temporary directory tree
                //    Directory.Delete(experimentDir);
                //    //signal.signal(signal.SIGINT, signal.default_int_handler);
            }

            //// Return completion reason and msg
            //return completionStatus;
            throw new NotImplementedException();
        }

        public static ModelCompletionStatus runDummyModel(int modelID, int? jobID, ModelDescription @params
            , string predictedField, object reportKeys, string optimizeKey, BaseClientJobDao jobsDAO,
           string modelCheckpointGUID
            , int? predictionCacheMaxRecords)
        {
            throw new NotImplementedException();
            //// The logger for this method
            ////logger = logging.getLogger('com.numenta.nupic.hypersearch.utils');


            //// Run the experiment now
            //try
            //{
            //    if (type(@params) is bool)
            //    {
            //        @params = { };
            //    }

            //    runner = OPFDummyModelRunner(modelID = modelID,
            //                                 jobID = jobID,
            //                                 params=params,
            //                                 predictedField = predictedField,
            //                                 reportKeyPatterns = reportKeys,
            //                                 optimizeKeyPattern = optimizeKey,
            //                                 jobsDAO = jobsDAO,
            //                                 modelCheckpointGUID = modelCheckpointGUID,
            //                                 logLevel = logLevel,
            //                                 predictionCacheMaxRecords = predictionCacheMaxRecords);

            //    (completionReason, completionMsg) = runner.run();
            //}

            //// The dummy model runner will call sys.exit(1) if
            ////  NTA_TEST_sysExitFirstNModels is set and the number of models in the
            ////  models table is <= NTA_TEST_sysExitFirstNModels
            //catch (SystemExit)
            //{
            //    sys.exit(1);
            //}
            //catch (InvalidConnectionException)
            //{
            //    raise;
            //}
            //catch (Exception e)
            //{
            //    (completionReason, completionMsg) = _handleModelRunnerException(jobID,
            //                                   modelID, jobsDAO, "NA",
            //                                   logger, e);
            //}

            //// Return completion reason and msg
            //return (completionReason, completionMsg);
        }

        /// <summary>
        /// Recursively applies f to the values in dict d.
        /// </summary>
        /// <param name="d">The dict to recurse over.</param>
        /// <param name="f">A function to apply to values in d that takes the value and 
        /// a list of keys from the root of the dict to the value.</param>
        public static void rApply(object d, Action<object, string[]> f, string[] currentKeys = null)
        {
            if (d is IDictionary)
            {
                foreach (DictionaryEntry entry in (IDictionary)d)
                {
                    List<string> keys = new List<string>(currentKeys);
                    keys.Add(entry.Key as string);
                    var value = entry.Value;
                    f(value, keys.ToArray());
                    rApply(value, f, keys.ToArray());
                }
                return;
            }

            if (d == null || (d.GetType().Namespace == null || !d.GetType().Namespace.StartsWith("HTM")))
            {
                return;
            }

            // We have an object
            // Get the properties of this object and loop over them
            var properties = d.GetType().GetProperties();
            if (currentKeys == null) currentKeys = new string[0];

            foreach (var property in properties)
            {
                if (property.Name == "Item" && property.GetGetMethod().GetParameters().Any())
                    continue;
                List<string> keys = new List<string>(currentKeys);
                keys.Add(property.Name);
                var value = property.GetValue(d);
                f(value, keys.ToArray());

                rApply(value, f, keys.ToArray());
            }

            //remainingDicts = [(d, ())];
            //while (len(remainingDicts) > 0)
            //{
            //    current, prevKeys = remainingDicts.pop();
            //    for (k, v in current.iteritems())
            //    {
            //          keys = prevKeys + (k,);
            //          if (isinstance(v, dict))
            //          {
            //              remainingDicts.insert(0, (v, keys));
            //          }
            //          else
            //          {
            //              f(v, keys);
            //          }
            //}            
        }

        private static Func<object, string[], object> identityConversion = (value, _keys) => value;

        /// <summary>
        /// Recursively copies a dict and returns the result.
        /// </summary>
        /// <param name="d">The dict to copy.</param>
        /// <param name="f">A function to apply to values when copying that takes the value and the
        /// list of keys from the root of the dict to the value and returns a value for the new dict.</param>
        /// <param name="discardNoneKeys">If True, discard key - value pairs when f returns None for the value.</param>
        /// <param name="deepCopy">If True, all values in returned dict are true copies(not the same object)</param>
        /// <param name="currentKeys">the current progress of keys in the recursion</param>
        /// <returns>A new dict with keys and values from d replaced with the result of f.</returns>
        public static object rCopy(object d, Func<object, string[], object> f = null
            , bool discardNoneKeys = true, bool deepCopy = true, string[] currentKeys = null)
        {
            if (d == null || (d.GetType().Namespace == null || !d.GetType().Namespace.StartsWith("HTM")))
            {
                return d;
            }
            if (f == null) f = identityConversion;
            // Optionally deep copy the dict.
            if (deepCopy)
            {
                MemoryStream ms = new MemoryStream();
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, d);
                ms.Position = 0;
                d = bf.Deserialize(ms);
            }

            if (d is IDictionary)
            {
                IDictionary dict = d as IDictionary;
                string[] dictKeys = new string[dict.Keys.Count];
                dict.Keys.CopyTo(dictKeys, 0);

                for (int i = 0; i < dict.Count; i++)
                {
                    List<string> keys = new List<string>(currentKeys);
                    string dictKey = dictKeys[i];
                    keys.Add(dictKey);
                    var value = dict[dictKey];
                    var converted = f(value, keys.ToArray());
                    ((IDictionary)d)[dictKey] = converted;
                    rCopy(converted, f, discardNoneKeys, deepCopy, keys.ToArray());
                }
                //foreach (DictionaryEntry entry in (IDictionary)d)
                //{
                //    List<string> keys = new List<string>(currentKeys);
                //    keys.Add(entry.Key as string);
                //    var value = entry.Value;
                //    var converted = f(value, keys.ToArray());
                //    ((IDictionary)d)[entry.Key] = converted;
                //    rCopy(converted, f, discardNoneKeys, deepCopy, keys.ToArray());
                //}
                return d;
            }



            // We have an object
            // Get the properties of this object and loop over them
            var properties = d.GetType().GetProperties();
            if (currentKeys == null) currentKeys = new string[0];

            foreach (var property in properties)
            {
                if (property.Name == "Item" && property.GetGetMethod().GetParameters().Any())
                    continue;
                List<string> keys = new List<string>(currentKeys);
                keys.Add(property.Name);
                var value = property.GetValue(d);
                var converted = f(value, keys.ToArray());
                property.SetValue(d, converted);
                var rcObj = rCopy(converted, f, discardNoneKeys, deepCopy, keys.ToArray());
                property.SetValue(d, rcObj);
            }

            //newDict = { };
            //toCopy = [(k, v, newDict, ()) for k, v in d.iteritems()];
            //while (len(toCopy) > 0)
            //{
            //  k, v, d, prevKeys = toCopy.pop();
            //  prevKeys = prevKeys + (k,);
            //  if (isinstance(v, dict))
            //  {
            //      d[k] = dict();
            //      toCopy[0:0] = [(innerK, innerV, d[k], prevKeys)
            //      for innerK, innerV in v.iteritems()];
            //  }
            //  else
            //  {
            //      # print k, v, prevKeys
            //      newV = f(v, prevKeys);
            //      if (not discardNoneKeys or newV is not None)
            //      {
            //          d[k] = newV;
            //      }
            //  }
            //}
            //return newDict;
            //}
            return d;
        }

        /// <summary>
        /// Return a clipped version of obj suitable for printing, This
        /// is useful when generating log messages by printing data structures, but
        /// don't want the message to be too long.
        /// 
        /// If passed in a dict, list, or namedtuple, each element of the structure's
        /// string representation will be limited to 'maxElementSize' characters.This
        /// will return a new object where the string representation of each element
        /// has been truncated to fit within maxElementSize.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="maxElementSize"></param>
        /// <returns></returns>
        public static object ClippedObj(object obj, int maxElementSize = 64)
        {
            // Is it a named tuple?
            if (obj is Util.Tuple)
            {
                obj = ((Util.Tuple)obj).All();
            }

            // Printing a dict?
            //if (isinstance(obj, dict))
            if (obj is IDictionary)
            {
                IDictionary objOut = (IDictionary)Activator.CreateInstance(obj.GetType());
                //for (key, val in obj.iteritems())
                foreach (DictionaryEntry kvp in (IDictionary)obj)
                {
                    objOut.Add(kvp.Key, ClippedObj(kvp.Value));
                    //objOut[key] = clippedObj(val);
                }
                return objOut;
            }

            // Printing a list?
            else if (obj is IEnumerable && !(obj is string))
            {
                var objOut = new ArrayList();
                foreach (var val in (IEnumerable)obj)
                {
                    objOut.Add(ClippedObj(val));
                }
                return objOut;
            }

            // Some other object
            else
            {
                string objOut = obj?.ToString();
                if (objOut?.Length > maxElementSize)
                {
                    //objOut = objOut[0:maxElementSize] + '...';
                    objOut = objOut.Substring(0, maxElementSize) + "...";
                }
                return objOut;
            }

            //return objOut;
        }

        /// <summary>
        /// Validate a python value against json schema:
        /// validate(value, schemaPath)
        /// validate(value, schemaDict)
        /// 
        /// value: python object to validate against the schema
        /// 
        /// The json schema may be specified either as a path of the file containing
        /// the json schema or as a python dictionary using one of the
        /// following keywords as arguments:
        /// schemaPath: Path of file containing the json schema object.
        /// schemaDict:     Python dictionary containing the json schema object
        /// 
        /// Returns: nothing
        /// 
        /// Raises:
        ///     ValidationError when value fails json validation
        /// </summary>
        /// <param name="value"></param>
        /// <param name="kwds"></param>
        public static void validate(object value, Dictionary<string, object> kwds)
        {
            //          Debug.Assert(kwds.Keys.Count >= 1);
            //          Debug.Assert(kwds.ContainsKey("schemaPath") || kwds.ContainsKey("schemaDict"));

            //          var schemaDict = null;
            //          if (kwds.ContainsKey("schemaPath"))
            //          {
            //              string schemaPath = (string)kwds["schemaPath"];
            //              kwds.Remove("schemaPath");
            //              schemaDict = loadJsonValueFromFile(schemaPath);
            //          }
            //          else if (kwds.ContainsKey("schemaDict"))
            //          {
            //              schemaDict = kwds["schemaDict"];
            //              kwds.Remove("schemaDict");
            //          }

            //          try
            //          {
            //              validictory.validate(value, schemaDict, **kwds);
            //          }
            //          catch (validictory.ValidationError as e)
            //{
            //              raise ValidationError(e);
            //          }
            //          
        }

        public static string generatePersistentJobGUID()
        {
            return "JOB_UUID1-" + Guid.NewGuid().ToString();
        }
        /// <summary>
        /// Return the result from dividing two dicts that represent date and time.
        /// 
        /// Both dividend and divisor are dicts that contain one or more of the following
        /// keys: 'years', 'months', 'weeks', 'days', 'hours', 'minutes', seconds',
        /// 'milliseconds', 'microseconds'.
        /// </summary>
        /// <param name="dividend">The numerator, as a dict representing a date and time</param>
        /// <param name="divisor">the denominator, as a dict representing a date and time</param>
        /// <returns>number of times divisor goes into dividend, as a floating point number.</returns>
        /// <remarks>
        /// For example: 
        /// aggregationDivide({ 'hours': 4}, {'minutes': 15}) == 16
        /// </remarks>
        public static double aggregationDivide(AggregationSettings dividend, AggregationSettings divisor)
        {
            // Convert each into microseconds
            Map<string, double> dividendMonthSec = aggregationToMonthsSeconds(dividend);
            Map<string, double> divisorMonthSec = aggregationToMonthsSeconds(divisor);

            // It is a usage error to mix both months and seconds in the same operation
            if ((dividendMonthSec["months"] != 0 && divisorMonthSec["seconds"] != 0)
                || (dividendMonthSec["seconds"] != 0 && divisorMonthSec["months"] != 0))
            {
                throw new InvalidOperationException("Aggregation dicts with months/years can only be inter-operated with other aggregation dicts " +
                                                    "that contain months/years");
            }

            if (dividendMonthSec["months"] > 0)
            { return (dividendMonthSec["months"]) / divisor.months; }
            else
            {
                return (dividendMonthSec["seconds"]) / divisorMonthSec["seconds"];
            }

        }
        /// <summary>
        /// Return the number of months and seconds from an aggregation dict that
        /// represents a date and time.
        /// Interval is a dict that contain one or more of the following keys: 'years',
        /// 'months', 'weeks', 'days', 'hours', 'minutes', seconds', 'milliseconds', 
        /// 'microseconds'.
        /// </summary>
        /// <param name="interval">The aggregation interval, as a dict representing a date and time</param>
        /// <returns>number of months and seconds in the interval, as a dict:  {months': XX, 'seconds': XX}. 
        /// The seconds is a floating point that can represent resolutions down to a
        /// microsecond.</returns>
        /// <remarks>
        /// For example:
        /// aggregationMicroseconds({ 'years': 1, 'hours': 4, 'microseconds':42}) ==  {'months':12, 'seconds':14400.000042}
        /// </remarks>
        public static Map<string, double> aggregationToMonthsSeconds(AggregationSettings interval)
        {
            double seconds = (double)interval.microseconds * 0.000001;
            seconds += (double)interval.milliseconds * 0.001;
            seconds += (double)interval.seconds;
            seconds += (double)interval.minutes * 60;
            seconds += (double)interval.hours * 60 * 60;
            seconds += (double)interval.days * 24 * 60 * 60;
            seconds += (double)interval.weeks * 7 * 24 * 60 * 60;

            double months = (double)interval.months;
            months += 12 * (double)interval.years;

            return new Map<string, double> { { "months", months }, { "seconds", seconds } };
        }
    }


    public struct ModelCompletionStatus
    {
        public ModelCompletionStatus(string reason, string msg)
        {
            completionReason = reason;
            completionMsg = msg;
        }

        public string completionReason;
        public string completionMsg;
    }

    public class OneFieldPermuationFilter : IPermutionFilter
    {
        public OneFieldPermuationFilter()
        {
            predictedField = "consumption";
            permutations = new PermutationModelParameters
            {
                modelParams = new PermutationModelDescriptionParams()
                {
                    sensorParams = new PermutationSensorParams()
                    {
                        encoders =
                        {
                            {
                                "", new PermuteEncoder(
                                    fieldName: "consumption",
                                    encoderClass: "ScalarEncoder",
                                    kwArgs: new KWArgsModel
                                    {
                                        {"maxval", new PermuteInt(100, 300, 1)},
                                        {"n", new PermuteInt(13, 500, 1)},
                                        {"w", 7},
                                        {"minval", 0},
                                    }
                                    )
                            }
                        }
                    },
                    tpParams = new PermutationTemporalPoolerParams
                    {
                        minThreshold = new PermuteInt(9, 12),
                        activationThreshold = new PermuteInt(12, 16),
                    }
                }
            };
            report = new[] { ".*consumption.*" };
            minimize = "prediction:rmse:field=consumption";
        }

        #region Implementation of IPermutionFilter

        public string predictedField { get; set; }
        public PermutationModelParameters permutations { get; set; }
        public string[] report { get; set; }
        public string minimize { get; set; }
        public PermutationModelParameters fastSwarmModelParams { get; set; }
        public List<string> fixedFields { get; set; }
        public int? minParticlesPerSwarm { get; set; }
        public bool? killUselessSwarms { get; set; }
        public InputPredictedField? inputPredictedField { get; set; }
        public bool? tryAll3FieldCombinations { get; set; }
        public bool? tryAll3FieldCombinationsWTimestamps { get; set; }
        public int? minFieldContribution { get; set; }
        public int? maxFieldBranching { get; set; }
        public string maximize { get; set; }
        public int? maxModels { get; set; }

        public IDictionary<string, object> dummyModelParams(PermutationModelParameters perm)
        {
            throw new NotImplementedException();
        }

        public bool permutationFilter(PermutationModelParameters perm)
        {
            throw new NotImplementedException();
        }

        #endregion
    }



    [JsonConverter(typeof(TypedPermutionFilterJsonConverter))]
    public abstract class PermutionFilterBase2// : IPermutionFilter
    {
        public string Type { get; set; }
        public string predictedField { get; set; }
        public ModelDescription permutations { get; set; }
        public string[] report { get; set; }
        public string minimize { get; set; }
        public ModelDescription fastSwarmModelParams { get; set; }
        public List<string> fixedFields { get; set; }
        public int? minParticlesPerSwarm { get; set; }
        public bool? killUselessSwarms { get; set; }
        public string inputPredictedField { get; set; }
        public bool? tryAll3FieldCombinations { get; set; }
        public bool? tryAll3FieldCombinationsWTimestamps { get; set; }
        public int? minFieldContribution { get; set; }
        public int? maxFieldBranching { get; set; }
        public string maximize { get; set; }
        public int? maxModels { get; set; }

        protected PermutionFilterBase2()
        {
            Type = GetType().AssemblyQualifiedName;
        }

        public virtual IDictionary<string, object> dummyModelParams(ModelDescription perm)
        {
            throw new NotImplementedException();
        }

        public virtual bool permutationFilter(ModelDescription perm)
        {
            throw new NotImplementedException();
        }
    }

    public class TypedPermutionFilterJsonConverter : JsonConverter
    {
        #region Overrides of JsonConverter

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Debug.WriteLine("Writing json");
            serializer.Serialize(writer, value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Debug.WriteLine("Reading json (PermutionFilterBase)");

            var jObj = JObject.Load(reader);

            object retVal = null;
            string type = jObj.Value<string>("Type");
            retVal = Activator.CreateInstance(Type.GetType(type));

            JsonReader reader2 = new JTokenReader(jObj);
            serializer.Populate(reader2, retVal);

            return retVal;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(BasePermutations).IsAssignableFrom(objectType);
        }
        public override bool CanWrite { get { return false; } }

        #endregion
    }

    public class TypedDescriptionBaseJsonConverter : JsonConverter
    {

        public override bool CanWrite { get { return false; } }

        #region Overrides of JsonConverter

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Debug.WriteLine("Writing json");
            serializer.Serialize(writer, value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Debug.WriteLine("Reading json (DescriptionBase)");

            var jObj = JObject.Load(reader);

            object retVal = null;
            string type = jObj.Value<string>("Type");
            retVal = Activator.CreateInstance(Type.GetType(type));

            JsonReader reader2 = new JTokenReader(jObj);
            serializer.Populate(reader2, retVal);

            return retVal;
        }

        public override bool CanConvert(Type objectType)
        {
            Debug.WriteLine("CanConvert json");
            return typeof(BaseDescription).IsAssignableFrom(objectType);
        }

        #endregion
    }

    [Serializable]
    public class ModelDescription
    {
        public ModelDescriptionParams modelParams { get; set; }

        //public StructuredParams structuredParams { get; set; }
    }

    [Serializable]
    public class ModelDescriptionParams
    {
        public string inferenceType { get; set; }
        public SensorParamsModel sensorParams { get; set; }
        public TemporalParams tpParams { get; set; }
    }

    public class ModelDescriptionParamsDescrModel
    {
        public bool clEnable;
        public InferenceType inferenceType { get; set; }
        public SensorParamsDescrModel sensorParams { get; set; }
        public TemporalParamsDescr tpParams { get; set; }
        public bool spEnable { get; set; }
        public SpatialParamsDescr spParams { get; set; }
        public bool tpEnable { get; set; }
        public bool trainSPNetOnlyIfRequested { get; set; }
        public ClassifierParamsDescr clParams { get; set; }
        public AnomalyParamsDescription anomalyParams { get; set; }

        public Parameters GetParameters()
        {
            Parameters pars = Parameters.Empty();
            if (spEnable)
            {
                pars.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, spParams.globalInhibition);
            }

            return pars;
        }
    }


    [Serializable]
    public class SensorParamsModel
    {
        public Map<string, object> encoders { get; set; }
        public TemporalParams tpParams { get; set; }
    }

    public class SensorParamsDescrModel
    {
        public Map<string, Map<string, object>> encoders { get; set; }
        public int verbosity { get; set; }
        public IDictionary<string, object> sensorAutoReset { get; set; }
    }

    [Serializable]
    public class TemporalParams
    {
        /// <summary>
        /// int or PermuteInt
        /// </summary>
        public object minThreshold { get; set; }
        /// <summary>
        /// int or PermuteInt
        /// </summary>
        public object activationThreshold { get; set; }
    }

    public class TemporalParamsDescr
    {
        [ParameterMapping]
        public int minThreshold { get; set; }
        [ParameterMapping]
        public int activationThreshold { get; set; }
        [ParameterMapping("tmVerbosity")]
        public int verbosity { get; set; }
        [ParameterMapping("columnDimensions")]
        public int[] columnCount { get; set; }
        [ParameterMapping]
        public int cellsPerColumn { get; set; }
        [ParameterMapping("inputDimensions")]
        public int[] inputWidth { get; set; }
        [ParameterMapping]
        public int seed { get; set; }
        //[ParameterMapping]
        public string temporalImp { get; set; }
        [ParameterMapping("maxNewSynapseCount")]
        public int newSynapseCount { get; set; }
        //[ParameterMapping]
        public int maxSynapsesPerSegment { get; set; }
        //[ParameterMapping]
        public int maxSegmentsPerCell { get; set; }
        [ParameterMapping("initialPermanence")]
        public double initialPerm { get; set; }
        [ParameterMapping("permanenceIncrement")]
        public double permanenceInc { get; set; }
        [ParameterMapping("permanenceDecrement")]
        public double permanenceDec { get; set; }
        //[ParameterMapping]
        public double globalDecay { get; set; }
        //[ParameterMapping]
        public int maxAge { get; set; }
        //[ParameterMapping]
        public string outputType { get; set; }
        //[ParameterMapping]
        public int pamLength { get; set; }
    }

    public class SpatialParamsDescr
    {
        [ParameterMapping]
        public int spVerbosity { get; set; }
        [ParameterMapping]
        public bool globalInhibition { get; set; }
        [ParameterMapping("columnDimensions")]
        public int[] columnCount { get; set; }
        [ParameterMapping("inputDimensions")]
        public int[] inputWidth { get; set; }
        [ParameterMapping]
        public double numActiveColumnsPerInhArea { get; set; }
        [ParameterMapping]
        public int seed { get; set; }
        [ParameterMapping]
        public double potentialPct { get; set; }
        [ParameterMapping]
        public double synPermConnected { get; set; }
        [ParameterMapping]
        public double synPermActiveInc { get; set; }
        [ParameterMapping]
        public double synPermInactiveDec { get; set; }
        [ParameterMapping]
        public double maxBoost { get; set; }
    }

    public class ClassifierParamsDescr
    {
        public string regionName { get; set; }
        public int clVerbosity { get; set; }
        public double alpha { get; set; }
        public int steps { get; set; }
    }

    public class AnomalyParamsDescription
    {
        public int? slidingWindowSize { get; set; }
        public Anomaly.Mode? mode { get; set; }

        /// <summary>
        /// Number of records to store in internal anomaly classifier record cache
        /// </summary>
        public int? anomalyCacheRecords { get; set; }
        /// <summary>
        /// Threshold for anomaly score to  auto detect anomalies
        /// </summary>
        public double? autoDetectThreshold { get; set; }
        /// <summary>
        /// Number of records to wait until auto detection begins
        /// </summary>
        public int? autoDetectWaitRecords { get; set; }
    }

    public class DescriptionConfigModel
    {
        public string model { get; set; }
        public int? version { get; set; }
        public AggregationSettings aggregationInfo { get; set; }
        public AggregationSettings predictAheadTime { get; set; }
        public ModelDescriptionParamsDescrModel modelParams { get; set; }

        public Map<string, object> GetDictionary()
        {
            return new Map<string, object>
            {
                {"model",model },
                {"version",version },
                {"aggregationInfo",aggregationInfo },
                {"predictAheadTime",predictAheadTime },
                {"modelParams",modelParams },
            };
        }

        public Parameters GetParameters()
        {
            var parameters = Parameters.Empty();
            parameters.Union(modelParams.GetParameters());
            return parameters;
        }

        public object this[string key]
        {
            get { return GetDictionary()[key]; }
        }

        public void SetDictValue(string key, object value)
        {
            switch (key)
            {
                case "model":
                    model = (string)value;
                    break;
                case "version":
                    version = (int)value;
                    break;
                case "aggregationInfo":
                    aggregationInfo = (AggregationSettings)value;
                    break;
                case "predictAheadTime":
                    predictAheadTime = (AggregationSettings)value;
                    break;
                case "modelParams":
                    modelParams = (ModelDescriptionParamsDescrModel)value;
                    break;
            }
        }
    }

    public class DescriptionControlModel
    {
        public string environment { get; set; }
        public Map<string, object> dataset { get; set; }
        public string[] loggedMetrics { get; set; }
        public MetricSpec[] metrics { get; set; }
        public Map<string, object> inferenceArgs { get; set; }
        public int? iterationCount { get; set; }
        public int? iterationCountInferOnly { get; set; }
    }
}

/*

import copy;
import json;
import os;
import sys;
import tempfile;
import logging;
import re;
import traceback;
import StringIO;
from collections import namedtuple;
import pprint;
import shutil;
import types;
import signal;
import uuid;
import validictory;

from nupic.database.ClientJobsDAO import (
    ClientJobsDAO, InvalidConnectionException);

// TODO: Note the function 'rUpdate' is also duplicated in the
// nupic.data.dictutils module -- we will eventually want to change this
// TODO: 'ValidationError', 'validate', 'loadJSONValueFromFile' duplicated in
// nupic.data.jsonhelpers -- will want to remove later

class JobFailException(Exception)
{
  """ If a model raises this exception, then the runModelXXX code will
  mark the job as canceled so that all other workers exit immediately, and mark
  the job as failed.
  """;
  pass;
}


def _paramsFileHead()
{
  """
  This is the first portion of every sub-experiment params file we generate. Between
  the head and the tail are the experiment specific options.
  """;

  str = getCopyrightHead() + \
"""

#// This file defines parameters for a prediction experiment.

//##############################################################################
//                                IMPORTANT!!!
// This params file is dynamically generated by the RunExperimentPermutations
// script. Any changes made manually will be over-written the next time
// RunExperimentPermutations is run!!!
//##############################################################################


from nupic.frameworks.opf.expdescriptionhelpers import importBaseDescription

// the sub-experiment configuration
config ={
""";

  return str;
}


def _paramsFileTail()
{
  """
  This is the tail of every params file we generate. Between the head and the tail
  are the experiment specific options.
  """;

  str = \
"""
}

mod = importBaseDescription('base.py', config)
locals().update(mod.__dict__)
""";
  return str;
}



def _appendReportKeys(keys, prefix, results)
{
  """
  Generate a set of possible report keys for an experiment's results.
  A report key is a string of key names separated by colons, each key being one
  level deeper into the experiment results dict. For example, 'key1:key2'.

  This routine is called recursively to build keys that are multiple levels
  deep from the results dict.

  Parameters:
  -----------------------------------------------------------
  keys:         Set of report keys accumulated so far
  prefix:       prefix formed so far, this is the colon separated list of key
                  names that led up to the dict passed in results
  results:      dictionary of results at this level.
  """;

  allKeys = results.keys();
  allKeys.sort();
  for( key in allKeys)
  {
    if( hasattr(results[key], 'keys'))
    {
      _appendReportKeys(keys, "%s%s:" % (prefix, key), results[key]);
    }
    else
    {
      keys.add("%s%s" % (prefix, key));
    }
  }
}



class _BadKeyError(Exception)
{
  """ If a model raises this exception, then the runModelXXX code will
  mark the job as canceled so that all other workers exit immediately, and mark
  the job as failed.
  """;
  pass;
}



def _matchReportKeys(reportKeyREs=[], allReportKeys=[])
{
  """
  Extract all items from the 'allKeys' list whose key matches one of the regular
  expressions passed in 'reportKeys'.

  Parameters:
  ----------------------------------------------------------------------------
  reportKeyREs:     List of regular expressions
  allReportKeys:    List of all keys

  retval:         list of keys from allReportKeys that match the regular expressions
                    in 'reportKeyREs'
                  If an invalid regular expression was included in 'reportKeys',
                    then BadKeyError() is raised
  """;

  matchingReportKeys = [];

  // Extract the report items of interest
  for( keyRE in reportKeyREs)
  {
    // Find all keys that match this regular expression
    matchObj = re.compile(keyRE);
    found = False;
    for( keyName in allReportKeys)
    {
      match = matchObj.match(keyName);
      if( match and match.end() == len(keyName))
      {
        matchingReportKeys.append(keyName);
        found = True;
      }
    }
    if( not found)
    {
      raise _BadKeyError(keyRE);
    }
  }

  return matchingReportKeys;
}



def _getReportItem(itemName, results)
{
  """
  Get a specific item by name out of the results dict.

  The format of itemName is a string of dictionary keys separated by colons,
  each key being one level deeper into the results dict. For example,
  'key1:key2' would fetch results['key1']['key2'].

  If itemName is not found in results, then None is returned

  """;

  subKeys = itemName.split(':');
  subResults = results;
  for( subKey in subKeys)
  {
    subResults = subResults[subKey];
  }

  return subResults;
}



def filterResults(allResults, reportKeys, optimizeKey=None)
{
  """ Given the complete set of results generated by an experiment (passed in
  'results'), filter out and return only the ones the caller wants, as
  specified through 'reportKeys' and 'optimizeKey'.

  A report key is a string of key names separated by colons, each key being one
  level deeper into the experiment results dict. For example, 'key1:key2'.


  Parameters:
  -------------------------------------------------------------------------
  results:             dict of all results generated by an experiment
  reportKeys:          list of items from the results dict to include in
                       the report. These can be regular expressions.
  optimizeKey:         Which report item, if any, we will be optimizing for. This can
                       also be a regular expression, but is an error if it matches
                       more than one key from the experiment's results.
  retval:  (reportDict, optimizeDict)
              reportDict: a dictionary of the metrics named by desiredReportKeys
              optimizeDict: A dictionary containing 1 item: the full name and
                    value of the metric identified by the optimizeKey

  """;

  // Init return values
  optimizeDict = dict();

  // Get all available report key names for this experiment
  allReportKeys = set();
  _appendReportKeys(keys=allReportKeys, prefix='', results=allResults);

  #----------------------------------------------------------------------------
  // Extract the report items that match the regular expressions passed in reportKeys
  matchingKeys = _matchReportKeys(reportKeys, allReportKeys);

  // Extract the values of the desired items
  reportDict = dict();
  for( keyName in matchingKeys)
  {
    value = _getReportItem(keyName, allResults);
    reportDict[keyName] = value;
  }


  // -------------------------------------------------------------------------
  // Extract the report item that matches the regular expression passed in
  //   optimizeKey
  if( optimizeKey is not None)
  {
    matchingKeys = _matchReportKeys([optimizeKey], allReportKeys);
    if( len(matchingKeys) == 0)
    {
      raise _BadKeyError(optimizeKey);
    }
    else if( len(matchingKeys) > 1)
    {
      raise _BadOptimizeKeyError(optimizeKey, matchingKeys);
    }
    optimizeKeyFullName = matchingKeys[0];

    // Get the value of the optimize metric
    value = _getReportItem(optimizeKeyFullName, allResults);
    optimizeDict[optimizeKeyFullName] = value;
    reportDict[optimizeKeyFullName] = value;
  }

  // Return info
  return(reportDict, optimizeDict);
}



def _quoteAndEscape(string)
{
  """
  string:   input string (ascii or unicode)

  Returns:  a quoted string with characters that are represented in python via
            escape sequences converted to those escape sequences
  """;
  assert type(string) in types.StringTypes;
  return pprint.pformat(string);
}



def _handleModelRunnerException(jobID, modelID, jobsDAO, experimentDir, logger,
                                e)
{
  """ Perform standard handling of an exception that occurs while running
  a model.

  Parameters:
  -------------------------------------------------------------------------
  jobID:                ID for this hypersearch job in the jobs table
  modelID:              model ID
  jobsDAO:              ClientJobsDAO instance
  experimentDir:        directory containing the experiment
  logger:               the logger to use
  e:                    the exception that occurred
  retval:               (completionReason, completionMsg)
  """;

  msg = StringIO.StringIO();
  print >>msg, "Exception occurred while running model %s: %r (%s)" % (
    modelID, e, type(e));
  traceback.print_exc(None, msg);

  completionReason = jobsDAO.CMPL_REASON_ERROR;
  completionMsg = msg.getvalue();
  logger.error(completionMsg);

  // Write results to the model database for the error case. Ignore
  // InvalidConnectionException, as this is usually caused by orphaned models
  #
  // TODO: do we really want to set numRecords to 0? Last updated value might
  //       be useful for debugging
  if( type(e) is not InvalidConnectionException)
  {
    jobsDAO.modelUpdateResults(modelID,  results=None, numRecords=0);
  }

  // TODO: Make sure this wasn't the best model in job. If so, set the best
  // appropriately

  // If this was an exception that should mark the job as failed, do that
  // now.
  if( type(e) == JobFailException)
  {
    workerCmpReason = jobsDAO.jobGetFields(jobID,
        ['workerCompletionReason'])[0];
    if( workerCmpReason == ClientJobsDAO.CMPL_REASON_SUCCESS)
    {
      jobsDAO.jobSetFields(jobID, fields=dict(
          cancel=True,
          workerCompletionReason = ClientJobsDAO.CMPL_REASON_ERROR,
          workerCompletionMsg = ": ".join(str(i) for i in e.args)),
          useConnectionID=False,
          ignoreUnchanged=True);
    }
  }

  return (completionReason, completionMsg);
}



def runModelGivenBaseAndParams(modelID, jobID, baseDescription, params,
            predictedField, reportKeys, optimizeKey, jobsDAO,
            modelCheckpointGUID, logLevel=None, predictionCacheMaxRecords=None)
{
  """ This creates an experiment directory with a base.py description file
  created from 'baseDescription' and a description.py generated from the
  given params dict and then runs the experiment.

  Parameters:
  -------------------------------------------------------------------------
  modelID:              ID for this model in the models table
  jobID:                ID for this hypersearch job in the jobs table
  baseDescription:      Contents of a description.py with the base experiment
                                          description
  params:               Dictionary of specific parameters to override within
                                  the baseDescriptionFile.
  predictedField:       Name of the input field for which this model is being
                                    optimized
  reportKeys:           Which metrics of the experiment to store into the
                                    results dict of the model's database entry
  optimizeKey:          Which metric we are optimizing for
  jobsDAO               Jobs data access object - the interface to the
                                  jobs database which has the model's table.
  modelCheckpointGUID:  A persistent, globally-unique identifier for
                                  constructing the model checkpoint key
  logLevel:             override logging level to this value, if not None

  retval:               (completionReason, completionMsg)
  """;
  from nupic.swarming.ModelRunner import OPFModelRunner;

  // The logger for this method
  logger = logging.getLogger('com.numenta.nupic.hypersearch.utils');


  // --------------------------------------------------------------------------
  // Create a temp directory for the experiment and the description files
  experimentDir = tempfile.mkdtemp();
  try
  {
    logger.info("Using experiment directory: %s" % (experimentDir));

    // Create the decription.py from the overrides in params
    paramsFilePath = os.path.join(experimentDir, 'description.py');
    paramsFile = open(paramsFilePath, 'wb');
    paramsFile.write(_paramsFileHead());

    items = params.items();
    items.sort();
    for (key,value) in items
    {
      quotedKey = _quoteAndEscape(key);
      if( isinstance(value, basestring))
      {

        paramsFile.write("  %s : '%s',\n" % (quotedKey , value));
      }
      else
      {
        paramsFile.write("  %s : %s,\n" % (quotedKey , value));
      }
    }

    paramsFile.write(_paramsFileTail());
    paramsFile.close();


    // Write out the base description
    baseParamsFile = open(os.path.join(experimentDir, 'base.py'), 'wb');
    baseParamsFile.write(baseDescription);
    baseParamsFile.close();


    // Store the experiment's sub-description file into the model table
    //  for reference
    fd = open(paramsFilePath);
    expDescription = fd.read();
    fd.close();
    jobsDAO.modelSetFields(modelID, {'genDescription': expDescription});


    // Run the experiment now
    try
    {
      runner = OPFModelRunner(
        modelID=modelID,
        jobID=jobID,
        predictedField=predictedField,
        experimentDir=experimentDir,
        reportKeyPatterns=reportKeys,
        optimizeKeyPattern=optimizeKey,
        jobsDAO=jobsDAO,
        modelCheckpointGUID=modelCheckpointGUID,
        logLevel=logLevel,
        predictionCacheMaxRecords=predictionCacheMaxRecords);

      signal.signal(signal.SIGINT, runner.handleWarningSignal);

      (completionReason, completionMsg) = runner.run();
    }

    catch( InvalidConnectionException)
    {
      raise;
    }
    catch( Exception, e)
    {

      (completionReason, completionMsg) = _handleModelRunnerException(jobID,
                                     modelID, jobsDAO, experimentDir, logger, e);
    }
  }

  finally
  {
    // delete our temporary directory tree
    shutil.rmtree(experimentDir);
    signal.signal(signal.SIGINT, signal.default_int_handler);
  }

  // Return completion reason and msg
  return (completionReason, completionMsg);
}



def runDummyModel(modelID, jobID, params, predictedField, reportKeys,
                  optimizeKey, jobsDAO, modelCheckpointGUID, logLevel=None, predictionCacheMaxRecords=None)
{
  from nupic.swarming.DummyModelRunner import OPFDummyModelRunner;

  // The logger for this method
  logger = logging.getLogger('com.numenta.nupic.hypersearch.utils');


  // Run the experiment now
  try
  {
    if( type(params) is bool)
    {
      params = {};
    }

    runner = OPFDummyModelRunner(modelID=modelID,
                                 jobID=jobID,
                                 params=params,
                                 predictedField=predictedField,
                                 reportKeyPatterns=reportKeys,
                                 optimizeKeyPattern=optimizeKey,
                                 jobsDAO=jobsDAO,
                                 modelCheckpointGUID=modelCheckpointGUID,
                                 logLevel=logLevel,
                                 predictionCacheMaxRecords=predictionCacheMaxRecords);

    (completionReason, completionMsg) = runner.run();
  }

  // The dummy model runner will call sys.exit(1) if
  //  NTA_TEST_sysExitFirstNModels is set and the number of models in the
  //  models table is <= NTA_TEST_sysExitFirstNModels
  catch( SystemExit)
  {
    sys.exit(1);
  }
  catch( InvalidConnectionException)
  {
    raise;
  }
  catch( Exception, e)
  {
    (completionReason, completionMsg) = _handleModelRunnerException(jobID,
                                   modelID, jobsDAO, "NA",
                                   logger, e);
  }

  // Return completion reason and msg
  return (completionReason, completionMsg);
}



// Passed as parameter to ActivityMgr
#
// repeating: True if the activity is a repeating activite, False if one-shot
// period: period of activity's execution (number of "ticks")
// cb: a callable to call upon expiration of period; will be called
//     as cb()
PeriodicActivityRequest = namedtuple("PeriodicActivityRequest",
                                     ("repeating", "period", "cb"));



class PeriodicActivityMgr(object)
{
  """
  TODO: move to shared script so that we can share it with run_opf_experiment
  """;

  // iteratorHolder: a list holding one iterator; we use a list so that we can
  //           replace the iterator for repeating activities (a tuple would not
  //           allow it if the field was an imutable value)
  Activity = namedtuple("Activity", ("repeating",
                                     "period",
                                     "cb",
                                     "iteratorHolder"));

  def __init__(self, requestedActivities)
  {
    """
    requestedActivities: a sequence of PeriodicActivityRequest elements
    """;

    self.__activities = [];
    for( req in requestedActivities)
    {
      act =   self.Activity(repeating=req.repeating,
                            period=req.period,
                            cb=req.cb,
                            iteratorHolder=[iter(xrange(req.period))]);
      self.__activities.append(act);
    }
    return;
  }

  def tick(self)
  {
    """ Activity tick handler; services all activities

    Returns:      True if controlling iterator says it's okay to keep going;
                  False to stop
    """;

    // Run activities whose time has come
    for( act in self.__activities)
    {
      if( not act.iteratorHolder[0])
      {
        continue;
      }

      try
      {
        next(act.iteratorHolder[0]);
      }
      catch( StopIteration)
      {
        act.cb();
        if( act.repeating)
        {
          act.iteratorHolder[0] = iter(xrange(act.period));
        }
        else
        {
          act.iteratorHolder[0] = None;
        }
      }
    }

    return True;
  }
}



def generatePersistentJobGUID()
{
  """Generates a "persistentJobGUID" value.

  Parameters:
  ----------------------------------------------------------------------
  retval:          A persistentJobGUID value

  """;
  return "JOB_UUID1-" + str(uuid.uuid1());
}



def identityConversion(value, _keys)
{
  return value;
}



def rCopy(d, f=identityConversion, discardNoneKeys=True, deepCopy=True)
{
  """Recursively copies a dict and returns the result.

  Args:
    d: The dict to copy.
    f: A function to apply to values when copying that takes the value and the
        list of keys from the root of the dict to the value and returns a value
        for the new dict.
    discardNoneKeys: If True, discard key-value pairs when f returns None for
        the value.
    deepCopy: If True, all values in returned dict are true copies (not the
        same object).
  Returns:
    A new dict with keys and values from d replaced with the result of f.
  """;
  // Optionally deep copy the dict.
  if( deepCopy)
  {
    d = copy.deepcopy(d);
  }

  newDict = {};
  toCopy = [(k, v, newDict, ()) for k, v in d.iteritems()];
  while( len(toCopy) > 0)
  {
    k, v, d, prevKeys = toCopy.pop();
    prevKeys = prevKeys + (k,);
    if( isinstance(v, dict))
    {
      d[k] = dict();
      toCopy[0:0] = [(innerK, innerV, d[k], prevKeys)
                     for innerK, innerV in v.iteritems()];
    }
    else
    {
      #print k, v, prevKeys
      newV = f(v, prevKeys);
      if( not discardNoneKeys or newV is not None)
      {
        d[k] = newV;
      }
    }
  }
  return newDict;
}



def rApply(d, f)
{
  """Recursively applies f to the values in dict d.

  Args:
    d: The dict to recurse over.
    f: A function to apply to values in d that takes the value and a list of
        keys from the root of the dict to the value.
  """;
  remainingDicts = [(d, ())];
  while( len(remainingDicts) > 0)
  {
    current, prevKeys = remainingDicts.pop();
    for( k, v in current.iteritems())
    {
      keys = prevKeys + (k,);
      if( isinstance(v, dict))
      {
        remainingDicts.insert(0, (v, keys));
      }
      else
      {
        f(v, keys);
      }
    }
  }
}



def clippedObj(obj, maxElementSize=64)
{
  """
  Return a clipped version of obj suitable for printing, This
  is useful when generating log messages by printing data structures, but
  don't want the message to be too long.

  If passed in a dict, list, or namedtuple, each element of the structure's
  string representation will be limited to 'maxElementSize' characters. This
  will return a new object where the string representation of each element
  has been truncated to fit within maxElementSize.
  """;

  // Is it a named tuple?
  if( hasattr(obj, '_asdict'))
  {
    obj = obj._asdict();
  }


  // Printing a dict?
  if( isinstance(obj, dict))
  {
    objOut = dict();
    for( key,val in obj.iteritems())
    {
      objOut[key] = clippedObj(val);
    }
  }

  // Printing a list?
  else if( hasattr(obj, '__iter__'))
  {
    objOut = [];
    for( val in obj)
    {
      objOut.append(clippedObj(val));
    }
  }

  // Some other object
  else
  {
    objOut = str(obj);
    if( len(objOut) > maxElementSize)
    {
      objOut = objOut[0:maxElementSize] + '...';
    }
  }

  return objOut;
}



class ValidationError(validictory.ValidationError)
{
  pass;
}



def validate(value, **kwds)
{
  """ Validate a python value against json schema:
  validate(value, schemaPath)
  validate(value, schemaDict)

  value:          python object to validate against the schema

  The json schema may be specified either as a path of the file containing
  the json schema or as a python dictionary using one of the
  following keywords as arguments:
    schemaPath:     Path of file containing the json schema object.
    schemaDict:     Python dictionary containing the json schema object

  Returns: nothing

  Raises:
          ValidationError when value fails json validation
  """;

  assert len(kwds.keys()) >= 1;
  assert 'schemaPath' in kwds or 'schemaDict' in kwds;

  schemaDict = None;
  if( 'schemaPath' in kwds)
  {
    schemaPath = kwds.pop('schemaPath');
    schemaDict = loadJsonValueFromFile(schemaPath);
  }
  else if( 'schemaDict' in kwds)
  {
    schemaDict = kwds.pop('schemaDict');
  }

  try
  {
    validictory.validate(value, schemaDict, **kwds);
  }
  catch( validictory.ValidationError as e)
  {
    raise ValidationError(e);
  }
}



// def loadJsonValueFromFile(inputFilePath):
//  """ Loads a json value from a file and converts it to the corresponding python
//  object.
#
//  inputFilePath:
//                  Path of the json file;
#
//  Returns:
//                  python value that represents the loaded json value
#
//  """
//  with open(inputFilePath) as fileObj:
//    value = json.load(fileObj)
#
//  return value



def sortedJSONDumpS(obj)
{
  """
  Return a JSON representation of obj with sorted keys on any embedded dicts.
  This insures that the same object will always be represented by the same
  string even if it contains dicts (where the sort order of the keys is
  normally undefined).
  """;

  itemStrs = [];

  if( isinstance(obj, dict))
  {
    items = obj.items();
    items.sort();
    for( key, value in items)
    {
      itemStrs.append('%s: %s' % (json.dumps(key), sortedJSONDumpS(value)));
    }
    return '{%s}' % (', '.join(itemStrs));
  }

  else if( hasattr(obj, '__iter__'))
  {
    for( val in obj)
    {
      itemStrs.append(sortedJSONDumpS(val));
    }
    return '[%s]' % (', '.join(itemStrs));
  }

  else
  {
    return json.dumps(obj);
  }
}



    */
