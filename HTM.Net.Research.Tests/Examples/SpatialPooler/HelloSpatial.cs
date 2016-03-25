using System;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Examples.SpatialPooler
{
    /// <summary>
    /// A simple program that demonstrates the working of the spatial pooler
    /// </summary>
    public class HelloSpatial
    {
        private Algorithms.SpatialPooler sp;
        private Parameters parameters;
        private Connections mem;
        private int[] inputArray;
        private int[] activeArray;
        private int inputSize;
        private int columnNumber;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputDimensions">The size of the input.  {m, n} will give a size of m x n</param>
        /// <param name="columnDimensions">The size of the 2 dimensional array of columns</param>
        public HelloSpatial(int[] inputDimensions, int[] columnDimensions)
        {
            inputSize = 1;
            columnNumber = 1;
            foreach (int x in inputDimensions)
            {
                inputSize *= x;
            }
            foreach (int x in columnDimensions)
            {
                columnNumber *= x;
            }
            activeArray = new int[columnNumber];

            parameters = Parameters.GetSpatialDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, inputDimensions);
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, columnDimensions);
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, inputSize);
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 0.02 * columnNumber);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.01);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.005);

            sp = new Algorithms.SpatialPooler();
            mem = new Connections();
            parameters.Apply(mem);
            sp.Init(mem);
        }

        /// <summary>
        /// Create a random input vector
        /// </summary>
        public void CreateInput()
        {
            for (int i = 0; i < 70; i++) Console.Write("-");
            Console.WriteLine("\nCreating a random input vector");
            for (int i = 0; i < 70; i++) Console.Write("-");
            Console.WriteLine();

            inputArray = new int[inputSize];

            Random rand = new Random();
            for (int i = 0; i < inputSize; i++)
            {
                // nextInt(2) returns 0 or 1
                inputArray[i] = rand.Next(2);
            }
        }

        /// <summary>
        /// Run the spatial pooler with the input vector
        /// </summary>
        public void Run()
        {
            for (int i = 0; i < 80; i++) Console.Write("-");
            Console.WriteLine("\nComputing the SDR");
            for (int i = 0; i < 70; i++) Console.Write("-");
            Console.WriteLine();

            sp.Compute(mem, inputArray, activeArray, true, true);

            int[] res = ArrayUtils.Where(activeArray, ArrayUtils.INT_GREATER_THAN_0);
            Console.WriteLine(Arrays.ToString(res));
        }

        /// <summary>
        /// Flip the value of a fraction of input bits (add noise)
        /// </summary>
        /// <param name="noiseLevel">The percentage of total input bits that should be flipped</param>
        public void AddNoise(double noiseLevel)
        {
            IRandom rand = new MersenneTwister(42);
            for (int i = 0; i < noiseLevel * inputSize; i++)
            {
                int randomPosition = rand.NextInt(inputSize);
                // Flipping the bit at the randomly picked position
                inputArray[randomPosition] = 1 - inputArray[randomPosition];
            }
        }

    }

    [TestClass]
    public class HelloSpatialTests
    {
        [TestMethod]
        public void TestHelloSpatialPooler()
        {
            HelloSpatial example = new HelloSpatial(new int[] { 32, 32 }, new int[] { 64, 64 });

            // Lesson 1
            Console.WriteLine("\n\nFollowing columns represent the SDR");
            Console.WriteLine("Different set of columns each time since we randomize the input");
            Console.WriteLine("Lesson - different input vectors give different SDRs\n\n");

            //Trying random vectors
            for (int i = 0; i < 3; i++)
            {
                example.CreateInput();
                example.Run();
            }

            //Lesson 2
            Console.WriteLine("\n\nIdentical SDRs because we give identical inputs");
            Console.WriteLine("Lesson - identical inputs give identical SDRs\n\n");

            for (int i = 0; i < 75; i++) Console.Write("-");
            Console.WriteLine("\nUsing identical input vectors");
            for (int i = 0; i < 75; i++) Console.Write("-");
            Console.WriteLine();

            //Trying identical vectors
            for (int i = 0; i < 2; i++)
            {
                example.Run();
            }

            // Lesson 3
            Console.WriteLine("\n\nNow we are changing the input vector slightly.");
            Console.WriteLine("We change a small percentage of 1s to 0s and 0s to 1s.");
            Console.WriteLine("The resulting SDRs are similar, but not identical to the original SDR");
            Console.WriteLine("Lesson - Similar input vectors give similar SDRs\n\n");

            // Adding 10% noise to the input vector
            // Notice how the output SDR hardly changes at all
            for (int i = 0; i < 75; i++) Console.Write("-");
            Console.WriteLine("\nAfter adding 10% noise to the input vector");
            for (int i = 0; i < 75; i++) Console.Write("-");
            example.AddNoise(0.1);
            example.Run();

            // Adding another 20% noise to the already modified input vector
            // The output SDR should differ considerably from that of the previous output
            for (int i = 0; i < 75; i++) Console.Write("-");
            Console.WriteLine("\nAfter adding another 20% noise to the input vector");
            for (int i = 0; i < 75; i++) Console.Write("-");
            example.AddNoise(0.2);
            example.Run();
        }
    }
}