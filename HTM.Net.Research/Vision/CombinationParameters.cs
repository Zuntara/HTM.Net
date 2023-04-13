using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Swarming.HyperSearch.Variables;
using HTM.Net.Util;

namespace HTM.Net.Research.Vision
{
    public class CombinationParameters
    {
        private List<string> _names;
        private List<List<object>> _allowedValues;
        private List<List<object>> _combinations;
        private Map<int, List<object>> _results; // combination_index, values
        private int _currentCombinationProgress;

        public CombinationParameters()
        {
            _names = new List<string>();
            _allowedValues = new List<List<object>>();
            _currentCombinationProgress = 0;
            _results = new Map<int, List<object>>();
        }

        public void Define(string name, List<object> allowedValues)
        {
            if (!_names.Contains(name))
            {
                _names.Add(name);
                _allowedValues.Add(allowedValues);
                CalculateCombinations();
            }
            else
            {
                Console.WriteLine("Parameter: {0} is already defined!", name);
            }
        }
        public void Define(string name, params object[] allowedValues)
        {
            if (!_names.Contains(name))
            {
                _names.Add(name);
                _allowedValues.Add(new List<object>(allowedValues));
                CalculateCombinations();
            }
            else
            {
                Console.WriteLine("Parameter: {0} is already defined!", name);
            }
        }
        private void CalculateCombinations()
        {
            List<List<object>> result = _allowedValues.CartesianProduct();
            _combinations = result;
        }

        /// <summary>
        /// Returns the index of the combination and the combination itself.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public int NextCombination(out IDictionary<string, object> values)
        {
            _currentCombinationProgress = _currentCombinationProgress % (_combinations.Count);
            int index = _currentCombinationProgress;
            _currentCombinationProgress++;

            List<object> combinations = _combinations[index];

            values = combinations
                .Select((obj, idx) => new { Name = _names[idx], Value = obj })
                .ToDictionary(k => k.Name, v => v.Value);

            return index;
        }
        /// <summary>
        /// Returns a specific combination
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IDictionary<string, object> GetCombination(int index)
        {
            List<object> combinations = _combinations[index];

            return combinations
                .Select((obj, idx) => new { Name = _names[idx], Value = obj })
                .ToDictionary(k => k.Name, v => v.Value);
        }
        /// <summary>
        /// This method prints a summary of all the results.
        /// </summary>
        /// <param name="resultNames"></param>
        /// <param name="formatStrings"></param>
        public void PrintResults(IEnumerable<string> resultNames, IEnumerable<string> formatStrings)
        {
            Console.WriteLine();
            Console.WriteLine("Summary of results:");
            Console.WriteLine();

            // Write header list
            var headerList = _names.ToList();
            headerList.AddRange(resultNames);
            string headerString = string.Join("\t", headerList);
            Console.WriteLine(headerString);

            foreach (var resultPair in _results)
            {
                // Print combination values
                var values = _combinations[resultPair.Key];
                string valueString = string.Join("\t", values);

                int f = 0;
                foreach (string formatString in formatStrings)
                {
                    valueString += string.Format(formatString, resultPair.Value[f]);
                    f++;
                }
                Console.WriteLine(valueString);
            }
        }
        /// <summary>
        /// This method adds an item to the results list.
        /// </summary>
        public void AppendResults(int combinationNumber, List<object> results)
        {
            Console.WriteLine("Just completed parameter Combination: {0}", Arrays.ToString(_combinations[combinationNumber]));
            _results[combinationNumber] = results;
            Console.WriteLine();
            Console.WriteLine("Parameter combinations completed: {0}/{1}", _results.Count, GetNumCombinations());
            Console.WriteLine();
        }

        public int GetNumCombinations()
        {
            return _combinations.Count;
        }
        /// <summary>
        /// This method returns the number of items in the results list.
        /// </summary>
        /// <returns></returns>
        public int GetNumResults()
        {
            return _results.Count;
        }

        public List<List<object>> GetAllCombinations()
        {
            return _combinations;
        }
    }

    public class CombiParameters : Parameters
    {
        private List<string> _names;
        private List<List<object>> _allowedValues;
        private List<List<object>> _combinations;
        private Map<int, List<object>> _results; // combination_index, values
        private int _currentCombinationProgress;
        private IRandom _random;
        public CombiParameters()
        {
            _names = new List<string>();
            _allowedValues = new List<List<object>>();
            _currentCombinationProgress = 0;
            _results = new Map<int, List<object>>();
            _random = new XorshiftRandom(42);
        }

        #region Overrides of Parameters

        public override void SetParameterByKey(KEY key, object value)
        {
            if (value is PermuteVariable)
            {
                string name = key.GetFieldName();
                if (!_names.Contains(name))
                {
                    _names.Add(name);
                    _allowedValues.Add(GetAllowedValues(value as PermuteVariable));
                    CalculateCombinations();
                }
                else
                {
                    Console.WriteLine("Parameter: {0} is already defined!", name);
                }
            }
            else
            {
                string name = key.GetFieldName();
                if (!_names.Contains(name))
                {
                    _names.Add(name);
                    _allowedValues.Add(new List<object> { value });
                    CalculateCombinations();
                }
                else
                {
                    Console.WriteLine("Parameter: {0} is already defined!", name);
                }
            }


            // record in our collection
            base.SetParameterByKey(key, value);
        }

        private List<object> GetAllowedValues(PermuteVariable permuteVariable)
        {
            List<object> retVal = new List<object>();

            bool addedValue;
            int tries = 0;
            do
            {
                addedValue = false;
                object value = permuteVariable.GetPosition();
                tries++;
                if (permuteVariable is PermuteInt)
                {
                    if (!retVal.Contains(value))
                    {
                        retVal.Add(value);
                        addedValue = true;
                        tries = 0;
                        permuteVariable.Agitate();
                        permuteVariable.NewPosition(null, _random);
                    }
                }
                if (tries < 100 && !addedValue)
                {
                    addedValue = true;  // try again
                    permuteVariable.Agitate();
                    permuteVariable.NewPosition(null, _random);
                }
            } while (addedValue);

            return retVal;
        }

        #endregion

        private void CalculateCombinations()
        {
            List<List<object>> result = _allowedValues.CartesianProduct();
            _combinations = result;
        }

        public int GetNumCombinations()
        {
            return _combinations.Count;
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
            Console.WriteLine("Summary of results:");
            Console.WriteLine();

            // Write header list
            var headerList = _names.ToList();
            headerList.AddRange(resultNames);
            string headerString = string.Join("\t", headerList);
            Console.WriteLine(headerString);

            foreach (var resultPair in _results)
            {
                // Print combination values
                var values = _combinations[resultPair.Key];
                string valueString = string.Join("\t", values);

                int f = 0;
                foreach (string formatString in formatStrings)
                {
                    valueString += string.Format(formatString, resultPair.Value[f]);
                    f++;
                }
                Console.WriteLine(valueString);
            }
        }
        /// <summary>
        /// This method adds an item to the results list.
        /// </summary>
        public void AppendResults(int combinationNumber, List<object> results)
        {
            Console.WriteLine("Just completed parameter Combination: {0}", Arrays.ToString(_combinations[combinationNumber]));
            _results[combinationNumber] = results;
            Console.WriteLine();
            Console.WriteLine("Parameter combinations completed: {0}/{1}", _results.Count, GetNumCombinations());
            Console.WriteLine();
        }

        /// <summary>
        /// Returns the index of the combination and the combination itself.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public int NextCombination(out IDictionary<string, object> values)
        {
            _currentCombinationProgress = _currentCombinationProgress % (_combinations.Count);
            int index = _currentCombinationProgress;
            _currentCombinationProgress++;

            List<object> combinations = _combinations[index];

            values = combinations
                .Select((obj, idx) => new { Name = _names[idx], Value = obj })
                .ToDictionary(k => k.Name, v => v.Value);

            return index;
        }
        /// <summary>
        /// Returns a specific combination
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IDictionary<string, object> GetCombination(int index)
        {
            List<object> combinations = _combinations[index];

            return combinations
                .Select((obj, idx) => new { Name = _names[idx], Value = obj })
                .ToDictionary(k => k.Name, v => v.Value);
        }
    }

    public static class ListExtentions
    {
        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Concat(new[] { item })
                );
        }

        public static List<List<T>> CartesianProduct<T>(this List<List<T>> sequences)
        {
            IEnumerable<List<T>> emptyProduct = new[] { new List<T>() };
            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Concat(new[] { item }).ToList()
                ).ToList();
        }
    }
}