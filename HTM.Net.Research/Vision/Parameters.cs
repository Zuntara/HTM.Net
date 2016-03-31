using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Util;

namespace HTM.Net.Research.Vision
{
    /// <summary>
    /// This class provides methods for searching ranges of parameters to see how
    /// they affect performance.
    /// </summary>
    public class Parameters
    {
        private List<string> _names;
        private List<List<object>> _allowedValues;
        private List<List<object>> _values;
        private List<List<int>> _valueIndexes;
        private List<List<object>> _results;
        private int numCombinations;
        private IRandom random = new XorshiftRandom(42);

        /// <summary>
        /// Have to keep track of the names and valid values of each parameter
        /// defined by the user.
        /// </summary>
        public Parameters()
        {
            // list of parameter names
            _names = new List<string>();
            // list of allowed parameter values
            _allowedValues = new List<List<object>>();
            // list of past and present parameter value indexes
            _valueIndexes = new List<List<int>>();
            // list of past and present results that correspond to each set of parameter values
            _results = new List<List<object>>();
            // the number of possible combinations of parameter values for all parameters
            numCombinations = 1;

            _values = new List<List<object>>();
        }

        /// <summary>
        /// This method allows users to define a parameter by providing its name and a list of values for the parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="allowedValues"></param>
        public void Define(string name, List<object> allowedValues)
        {
            if (!_names.Contains(name))
            {
                _names.Add(name);
                _allowedValues.Add(allowedValues);
                numCombinations = numCombinations + allowedValues.Count;
            }
            else
            {
                Console.WriteLine("Parameter: {0} is already defined!");
            }
        }

        /// <summary>
        /// This method returns the names of all defined parameters.
        /// </summary>
        /// <returns></returns>
        public List<string> GetNames()
        {
            return _names;
        }
        /// <summary>
        /// This method returns the current value of the parameter specified by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object GetValue(string name)
        {
            Debug.Assert(_names.Contains(name));
            int i = _names.IndexOf(name);
            Debug.Assert(_valueIndexes.Last().Count > i);
            return _allowedValues[i][_valueIndexes.Last()[i]];
        }
        /// <summary>
        /// This method returns the current values of all defined parameters.
        /// </summary>
        public IEnumerable<object> GetAllValues()
        {
            return _valueIndexes.Last().Select((i,j) => _allowedValues[i][j]);
        }
        /// <summary>
        /// This method adds an item to the results list.
        /// </summary>
        public void AppendResults(List<object> items)
        {
            Console.WriteLine("Just completed parameter Combination: {0}", Arrays.ToString(GetAllValues()));
            _results.Add(items);
            Console.WriteLine();
            Console.WriteLine("Parameter combinations completed: {0}/{1}", _results.Count, numCombinations);
            Console.WriteLine();
        }
        /// <summary>
        /// This method returns the number of items in the results list.
        /// </summary>
        /// <returns></returns>
        public int GetNumResults()
        {
            return _results.Count;
        }
        /// <summary>
        /// This method prints a summary of all the results.
        /// </summary>
        /// <param name="resultNames"></param>
        /// <param name="formatStrings"></param>
        public void PrintResults(IEnumerable<string> resultNames, IEnumerable<string> formatStrings)
        {
            Console.WriteLine();
            Console.WriteLine("Summary of results");
            Console.WriteLine();

            var headerList = GetNames();
            headerList.AddRange(resultNames);
            string headerString = string.Join(", ", headerList);
            Console.WriteLine(headerString);
            int i = 0;
            foreach (var result in _results)
            {
                var values = _valueIndexes[i].Skip(1).Select((j,k) => _allowedValues[j][k]);
                string valueString = Arrays.ToString(values);

                int f = 0;
                foreach (string formatString in formatStrings)
                {
                    valueString += string.Format(formatString, result[f]);
                    f++;
                }
                Console.WriteLine(valueString);
                i++;
            }
        }
        /// <summary>
        /// This method randomly selects a value for each parameter from its list of
        /// allowed parameter values.  If the resulting combination has already been
        /// used then it tries again.
        /// </summary>
        public void NextRandomCombination()
        {
            List<object> randomCombination = new List<object>();
            for (int i = 0; i < _names.Count; i++)
            {
                var combination = GetRandomChoice(_allowedValues[i]);
                randomCombination.Add(combination);
            }

            if (_values.Any(vl=> vl.SequenceEqual(randomCombination)))
            {
                NextRandomCombination();
            }
            else
            {
                _values.Add(randomCombination);
                Console.WriteLine("Parameter combination: {0}", Arrays.ToString(GetAllValues()));
            }
        }
        /// <summary>
        /// This method finds the next combination of parameter values using the
        /// allowed value lists for each parameter.
        /// </summary>
        public void NextCombination()
        {
            if (_valueIndexes.Count == 0)
            {
                // list of value indexes is empty so this is the first combination, 
                // each parameter gets the first value in its list of allowed values
                _valueIndexes.Add(_names.Select(n => 0).ToList());
            }
            else
            {
                var newValueIndexes = _valueIndexes.Last();
                int i = 0;
                while (i < _names.Count)
                {
                    // if current value is not the last in the list
                    if (_valueIndexes.Last()[i] != _allowedValues[i].Count - 1)
                    {
                        // change parameter to next value in allowed value list and return
                        newValueIndexes[i] += 1;
                        break;
                    }
                    else
                    {
                        // change parameter to first value in allowed value list
                        newValueIndexes[i] = 0;
                        // move next parameter to next value in its allowed value list
                        i++;
                    }
                }
                _valueIndexes.Add(newValueIndexes);
            }
            Console.WriteLine("Parameter combination: {0}", Arrays.ToString(GetAllValues()));
        }

        private object GetRandomChoice(IList<object> list)
        {
            int index = random.NextInt(list.Count);
            return list[index];
        }
    }
}