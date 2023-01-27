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
    public class CombinationParameters
    {
        private List<string> _names;
        private List<List<object>> _allowedValues;
        private List<List<object>> _values;
        private List<List<int>> _valueIndexes;
        private List<List<object>> _results;
        private int _numCombinations;
        private readonly IRandom _random = new XorshiftRandom(42);

        /// <summary>
        /// Have to keep track of the names and valid values of each parameter
        /// defined by the user.
        /// </summary>
        public CombinationParameters()
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
            _numCombinations = 1;

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
                _numCombinations = _numCombinations + allowedValues.Count;
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
            return _valueIndexes.Last().Select((i,j) => _allowedValues[j][i]);
        }
        /// <summary>
        /// This method adds an item to the results list.
        /// </summary>
        public void AppendResults(List<object> items)
        {
            Console.WriteLine("Just completed parameter Combination: {0}", Arrays.ToString(GetAllValues()));
            _results.Add(items);
            Console.WriteLine();
            Console.WriteLine("Parameter combinations completed: {0}/{1}", _results.Count, _numCombinations);
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
        public void PrintResults(ICollection<string> resultNames, ICollection<string> formatStrings)
        {
            Console.WriteLine();
            Console.WriteLine("Summary of results");
            Console.WriteLine();

            var headerList = GetNames();
            headerList.AddRange(resultNames);
            string headerString = string.Join("\t", headerList);
            Console.WriteLine(headerString);
            int i = 0;
            foreach (var result in _results)
            {
                if(_valueIndexes.Count <= i) throw new InvalidOperationException("Did you call NextCombination?");
                var values = _valueIndexes[i].Select((j,k) => _allowedValues[k][j]);
                string valueString = string.Join("\t", values);

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
                _valueIndexes.Add(_names.Select(_ => 0).ToList());
            }
            else
            {
                var newValueIndexes = _valueIndexes.Last().ToList();
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
            int index = _random.NextInt(list.Count);
            return list[index];
        }

        public int GetNumCombinations()
        {
            return _numCombinations;
        }
    }
}