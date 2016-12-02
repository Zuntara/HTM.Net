using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.Swarming;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Swarming
{
    [TestClass]
    public class PermutationHelperTest
    {
        private int verbosity;

        /// <summary>
        /// Run a bunch of iterations on a PermuteVar and collect which positions
        /// were visited.Verify that they were all valid.
        /// </summary>
        private void TestValidPositions<T>(double minValue, double maxValue, double? stepSize, int iterations = 100)
            where T : PermuteVariable
        {
            var positions = new HashSet<double>();
            double? cogRate = 2.0;
            double? socRate = 2.0;
            double? inertia = null;
            double gBestPosition = maxValue;
            double lBestPosition = minValue;
            double? foundBestPosition = null;
            double? foundBestResult = null;
            var rng = new MersenneTwister(42);

            //var var = varClass(min = minValue, max = maxValue, stepSize = stepSize,
            //                   inertia = inertia, cogRate = cogRate, socRate = socRate);
            var var = (T)Activator.CreateInstance(typeof(T), minValue, maxValue, stepSize, inertia, cogRate, socRate);
            foreach (var nothing in ArrayUtils.XRange(0, iterations, 1))
            {
                var pos = (double)var.GetPosition();
                if (this.verbosity >= 1)
                {
                    Console.WriteLine("pos: {0}", pos);
                }
                if (this.verbosity >= 2)
                {
                    Console.WriteLine(var);
                }
                positions.Add(pos);

                // Set the result so that the local best is at lBestPosition.
                double result = 1.0 - Math.Abs(pos - lBestPosition);

                if (foundBestResult == null || result > foundBestResult)
                {
                    foundBestResult = result;
                    foundBestPosition = pos;
                    var state = var.GetState();
                    state.bestPosition = foundBestPosition.GetValueOrDefault();
                    state.bestResult = foundBestResult;
                    var.SetState(state);
                }

                var.NewPosition(gBestPosition, rng);
            }

            //positions = sorted(positions);
            //positions.Sort();
            Console.WriteLine("Positions visited ({0}):", positions.Count);

            // Validate positions.
            Assert.IsTrue((positions.Max()) <= maxValue);
            Assert.IsTrue((positions.Min()) >= minValue);
            int visited = (int)(Math.Round((maxValue - minValue) / stepSize.GetValueOrDefault()) + 1);
            Assert.IsTrue(positions.Count <= visited);
        }

        private void TestValidPositions<T>(int minValue, int maxValue, int? stepSize, int iterations = 100)
           where T : PermuteVariable
        {
            var positions = new HashSet<double>();
            double? cogRate = 2.0;
            double? socRate = 2.0;
            double? inertia = null;
            double gBestPosition = maxValue;
            double lBestPosition = minValue;
            double? foundBestPosition = null;
            double? foundBestResult = null;
            var rng = new MersenneTwister(42);

            //var var = varClass(min = minValue, max = maxValue, stepSize = stepSize,
            //                   inertia = inertia, cogRate = cogRate, socRate = socRate);
            var var = (T)Activator.CreateInstance(typeof(T), minValue, maxValue, stepSize, inertia, cogRate, socRate);
            foreach (var nothing in ArrayUtils.XRange(0, iterations, 1))
            {
                var pos = (double)var.GetPosition();
                if (this.verbosity >= 1)
                {
                    Console.WriteLine("pos: {0}", pos);
                }
                if (this.verbosity >= 2)
                {
                    Console.WriteLine(var);
                }
                positions.Add(pos);

                // Set the result so that the local best is at lBestPosition.
                double result = 1.0 - Math.Abs(pos - lBestPosition);

                if (foundBestResult == null || result > foundBestResult)
                {
                    foundBestResult = result;
                    foundBestPosition = pos;
                    var state = var.GetState();
                    state.bestPosition = foundBestPosition.GetValueOrDefault();
                    state.bestResult = foundBestResult;
                    var.SetState(state);
                }

                var.NewPosition(gBestPosition, rng);
            }

            //positions = sorted(positions);
            //positions.Sort();
            Console.WriteLine("Positions visited ({0}):", positions.Count);

            // Validate positions.
            Assert.IsTrue((positions.Max()) <= maxValue);
            Assert.IsTrue((positions.Min()) >= minValue);
            int visited = (int)(Math.Round((double)(maxValue - minValue) / stepSize.GetValueOrDefault()) + 1);
            Assert.IsTrue(positions.Count <= visited);
        }

        /// <summary>
        /// Test that we can converge on the right answer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <param name="targetValue"></param>
        /// <param name="iterations"></param>
        private void TestConvergence<T>(double minValue, double maxValue, double targetValue,
                       int iterations = 100)
            where T : PermuteVariable
        {
            double gBestPosition = targetValue;
            double lBestPosition = targetValue;
            double? foundBestPosition = null;
            double? foundBestResult = null;
            var rng = new MersenneTwister(42);

            //var = varClass(min = minValue, max = maxValue);
            var var = (T)Activator.CreateInstance(typeof(T), minValue, maxValue, null, null, null, null);
            double pos = 0;
            foreach (var nothing in ArrayUtils.XRange(0, iterations, 1))
            {
                pos = (double)var.GetPosition();
                if (this.verbosity >= 1)
                {
                    Console.WriteLine("pos: {0}", pos);
                }
                if (this.verbosity >= 2)
                {
                    Console.WriteLine(var);
                }

                // Set the result so that the local best is at lBestPosition.
                double result = 1.0 - Math.Abs(pos - lBestPosition);

                if (foundBestResult == null || result > foundBestResult)
                {
                    foundBestResult = result;
                    foundBestPosition = pos;
                    var state = var.GetState();
                    state.bestPosition = foundBestPosition.GetValueOrDefault();
                    state.bestResult = foundBestResult;
                    var.SetState(state);
                }

                var.NewPosition(gBestPosition, rng);
            }

            // Test that we reached the target.

            Console.WriteLine("Target: {0}, Converged on: {1}", targetValue, pos);
            Assert.IsTrue(Math.Abs(pos - targetValue) < 0.001);
        }

        private void TestConvergence<T>(int minValue, int maxValue, int targetValue,
                       int iterations = 100)
            where T : PermuteVariable
        {
            double gBestPosition = targetValue;
            double lBestPosition = targetValue;
            double? foundBestPosition = null;
            double? foundBestResult = null;
            var rng = new MersenneTwister(42);

            //var = varClass(min = minValue, max = maxValue);
            var var = (T)Activator.CreateInstance(typeof(T), minValue, maxValue, null, null, null, null);
            double pos = 0;
            foreach (var nothing in ArrayUtils.XRange(0, iterations, 1))
            {
                pos = (double)var.GetPosition();
                if (this.verbosity >= 1)
                {
                    Console.WriteLine("pos: {0}", pos);
                }
                if (this.verbosity >= 2)
                {
                    Console.WriteLine(var);
                }

                // Set the result so that the local best is at lBestPosition.
                double result = 1.0 - Math.Abs(pos - lBestPosition);

                if (foundBestResult == null || result > foundBestResult)
                {
                    foundBestResult = result;
                    foundBestPosition = pos;
                    var state = var.GetState();
                    state.bestPosition = foundBestPosition.GetValueOrDefault();
                    state.bestResult = foundBestResult;
                    var.SetState(state);
                }

                var.NewPosition(gBestPosition, rng);
            }

            // Test that we reached the target.

            Console.WriteLine("Target: {0}, Converged on: {1}", targetValue, pos);
            Assert.IsTrue(Math.Abs(pos - targetValue) < 0.001);
        }

        [TestMethod]
        public void TestChoices()
        {
            var pc = new PermuteChoices(new object[] { 0, 1.0, 2, 3 });
            int[] counts = new int[4];
            var rng = new MersenneTwister(42);
            // Check the without results the choices are chosen uniformly.
            int pos = -1;
            foreach (var nothing in ArrayUtils.Range(0, 1000))
            {
                pos = (int)(pc.NewPosition(null, rng));
                counts[pos] += 1;
            }
            foreach (int count in counts)
            {
                Assert.IsTrue(count < 270 && count > 230);
            }
            Console.WriteLine("No results permuteChoice test passed");

            // Check that with some results the choices are chosen with the lower
            // errors being chosen more often.
            var choices = new object[] { 1, 11.0, 21, 31 };
            pc = new PermuteChoices(choices);
           List<Tuple<int, List<double>>> resultsPerChoice = new List<Tuple<int, List<double>>>();
            var counts2 = new Map<int, int>();
            foreach (var choice in choices)
            {
                resultsPerChoice.Add(new Tuple<int, List<double>>((int)choice, new List<double> { (double)choice }));
                counts2[(int)choice] = 0;
            }
            pc.SetResultsPerChoice(resultsPerChoice);


            // Check the without results the choices are chosen uniformly.
            foreach (var nothing in ArrayUtils.Range(0, 1000))
            {
                double choice = ((double?)pc.NewPosition(null, rng)).GetValueOrDefault();
                counts2[(int)choice] += 1;
            }
            // Make sure that as the error goes up, the number of times the choice is
            // seen goes down.
            int prevCount = 1001;
            foreach (var choice in choices)
            {
                Assert.IsTrue(prevCount > counts2[(int)choice]);
                prevCount = counts2[(int)choice];
            }
            Console.WriteLine("Results permuteChoice test passed");

            // Check that with fixEarly as you see more data points you begin heavily
            // biasing the probabilities to the one with the lowest error.
            choices = new object[] { 1, 11, 21.0, 31 };
            pc = new PermuteChoices(choices, fixEarly: true);
            var resultsPerChoiceDict = new Map<int, Tuple<int, List<double>>>();
            counts2 = new Map<int, int>();

            foreach (var choice in choices)
            {
                //resultsPerChoiceDict[choice] = (choice, []);
                resultsPerChoiceDict[(int)choice] = new Tuple<int, List<double>>((int)choice, new List<double>());
                //resultsPerChoiceDict[(int)choice] = new Dictionary<int, List<double>> { { (int)choice, new List<double>() } };

                counts2[(int)choice] = 0;
            }
            // The count of the highest probability entry, this should go up as more
            // results are seen.
            int prevLowestErrorCount = 0;
            foreach (var nothing in ArrayUtils.Range(0, 10))
            {
                foreach (var choice in choices)
                {
                    //resultsPerChoiceDict[(int)choice][1].Add((double) choice);
                    resultsPerChoiceDict[(int)choice].Item2.Add((double)choice);
                    counts2[(int)choice] = 0;
                }
                pc.SetResultsPerChoice(resultsPerChoiceDict.Values.ToList());

                // Check the without results the choices are chosen uniformly.
                foreach (var nothing2 in ArrayUtils.Range(0, 1000))
                {
                    double choice = ((double?)pc.NewPosition(null, rng)).GetValueOrDefault();
                    counts2[(int)choice] += 1;
                }
                // Make sure that as the error goes up, the number of times the choice is
                // seen goes down.
                Assert.IsTrue(prevLowestErrorCount < counts2[1]);
                prevLowestErrorCount = counts2[1];
            }
            Console.WriteLine("Fix early permuteChoice test passed");
        }

        [TestMethod]
        public void TestValidPositionsFloat()
        {
            verbosity = 2;
            // ------------------------------------------------------------------------
            // Test that step size is handled correctly for floats
            this.TestValidPositions<PermuteFloat>(minValue: 2.1, maxValue: 5.1, stepSize: 0.5);
        }

        [TestMethod]
        public void TestValidPositionsIntStep1()
        {
            verbosity = 2;
            // ------------------------------------------------------------------------
            // Test that step size is handled correctly for floats
            this.TestValidPositions<PermuteInt>(minValue: 2, maxValue: 11, stepSize: 1, iterations: 100);
        }

        [TestMethod]
        public void TestValidPositionsIntStep3()
        {
            verbosity = 2;
            // ------------------------------------------------------------------------
            // Test that step size is handled correctly for floats
            this.TestValidPositions<PermuteInt>(minValue: 2, maxValue: 11, stepSize: 3);
        }

        // ------------------------------------------------------------------------
        // Test that we can converge on a target value
        // Using Float
        [TestMethod]
        public void TestConvergenceFloat()
        {
            verbosity = 2;
            this.TestConvergence<PermuteFloat>(minValue: 2.1, maxValue: 5.1, targetValue: 5.0);
            this.TestConvergence<PermuteFloat>(minValue: 2.1, maxValue: 5.1, targetValue: 2.2);
            this.TestConvergence<PermuteFloat>(minValue: 2.1, maxValue: 5.1, targetValue: 3.5);
        }


        // Using int
        [TestMethod]
        public void TestConvergenceInt()
        {
            verbosity = 2;
            this.TestConvergence<PermuteInt>(minValue: 1, maxValue: 20, targetValue: 19);
            this.TestConvergence<PermuteInt>(minValue: 1, maxValue: 20, targetValue: 1);
        }

    }
}