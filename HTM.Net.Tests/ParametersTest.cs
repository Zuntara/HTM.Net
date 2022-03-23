using System;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests
{
    [TestClass]
    public class ParametersTest
    {
        private Parameters _parameters;

        [TestMethod]
        public void TestApply()
        {
            DummyContainer dc = new DummyContainer();
            Parameters @params = Parameters.GetAllDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 2048 });
            @params.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 20.0);
            @params.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, null);
            @params.Apply(dc);
            Assert.AreEqual(20.0, dc.PotentialPct, 0, "Setter did not work");
            Assert.IsTrue(Arrays.AreEqual(new[] { 2048 }, dc.GetColumnDimensions()), "Setter did not work");
        }

        [TestMethod]
        public void TestDefaultsAndUpdates()
        {
            Parameters @params = Parameters.GetAllDefaultParameters();
            Assert.AreEqual(@params.GetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN), 32);
            Assert.AreEqual(@params.GetParameterByKey(Parameters.KEY.SEED), 42);
            Assert.AreEqual(true, ((Random)@params.GetParameterByKey(Parameters.KEY.RANDOM)).GetType().Equals(typeof(MersenneTwister)));
            Console.WriteLine("All Defaults:\n" + Parameters.GetAllDefaultParameters());
            Console.WriteLine("Spatial Defaults:\n" + Parameters.GetSpatialDefaultParameters());
            Console.WriteLine("Temporal Defaults:\n" + Parameters.GetTemporalDefaultParameters());
            _parameters = Parameters.GetSpatialDefaultParameters();
            _parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new[] { 64, 64 });
            _parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 32, 32 });
            _parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 0.02 * 64 * 64);
            Console.WriteLine("Updated/Combined:\n" + _parameters);

        }

        public class DummyContainerBase
        {
            private int[] _columnDimensions;

            public int[] GetColumnDimensions()
            {
                return _columnDimensions;
            }

            public void SetColumnDimensions(int[] columnDimensions)
            {
                _columnDimensions = columnDimensions;
            }
        }

        public class DummyContainer : DummyContainerBase
        {
            public double PotentialPct { get; set; } = 0;

            public bool SpParallelMode { get; set; }
        }

        [TestMethod]
        public void TestKNNEnumAndConstantFields()
        {
            Parameters @params = Parameters.GetKnnDefaultParameters();
            KNNClassifier knn = KNNClassifier.GetBuilder().Apply(@params);
            try
            {
                @params.Apply(knn);
                Assert.IsTrue(knn.GetNumSVDDims() == null);
                Assert.IsTrue(knn.GetDistanceMethod() == DistanceMethod.Norm); // the default
            }
            catch (Exception e)
            {
                Assert.Fail();
            }

            @params = Parameters.GetKnnDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.NUM_SVD_DIMS, (int)KnnMode.ADAPTIVE);
            @params.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.PctInputOverlap);
            knn = KNNClassifier.GetBuilder().Apply(@params);
            try
            {
                @params.Apply(knn);
                Assert.IsTrue(knn.GetNumSVDDims() == (int)KnnMode.ADAPTIVE);
                Assert.IsTrue(knn.GetDistanceMethod() == DistanceMethod.PctInputOverlap);
            }
            catch (Exception e)
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void TestUnion()
        {
            Parameters @params = Parameters.GetAllDefaultParameters();
            Parameters arg = Parameters.GetAllDefaultParameters();
            arg.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 5);

            Assert.IsTrue((int)@params.GetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN) != 5);
            @params.Union(arg);
            Assert.IsTrue((int)@params.GetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN) == 5);
        }

        [TestMethod]
        public void TestGetKeyByFieldName()
        {
            Parameters.KEY expected = Parameters.KEY.POTENTIAL_PCT;
            Assert.AreEqual(expected, Parameters.KEY.GetKeyByFieldName("potentialPct"));

            Assert.IsFalse(expected.Equals(Parameters.KEY.GetKeyByFieldName("random")));
        }

        [TestMethod]
        public void TestGetMinMax()
        {
            Parameters.KEY synPermActInc = Parameters.KEY.SYN_PERM_ACTIVE_INC;
            Assert.AreEqual(0.0, synPermActInc.GetMin());
            Assert.AreEqual(1.0, synPermActInc.GetMax());
        }

        [TestMethod]
        public void TestCheckRange()
        {
            Parameters @params = Parameters.GetAllDefaultParameters();

            try
            {
                @params.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 2.0);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(ArgumentException), e.GetType());
                Assert.AreEqual("Can not set Parameters Property 'synPermActiveInc' because of value '2.00' not in range. Range[0.00-1.00]", e.Message);
            }

            try
            {
                Parameters.KEY.SYN_PERM_ACTIVE_INC.CheckRange(null);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(ArgumentException), e.GetType());
                Assert.AreEqual("checkRange argument can not be null", e.Message);
            }

            // Test catch type mismatch
            try
            {
                @params.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, true);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(ArgumentException), e.GetType());
                Assert.AreEqual("Can not set Parameters Property 'synPermActiveInc' because of type mismatch. The required type is class System.Double", e.Message);
            }

            // Positive test
            try
            {
                @params.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.8);
                Assert.AreEqual(0.8, (double)@params.GetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC), 0.0);
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }

        }

        [TestMethod]
        public void TestSize()
        {
            Parameters @params = Parameters.GetAllDefaultParameters();
            Assert.AreEqual(66, @params.Size());
        }

        [TestMethod]
        public void TestKeys()
        {
            Parameters @params = Parameters.GetAllDefaultParameters();
            Assert.IsNotNull(@params.Keys());
            Assert.AreEqual(66, @params.Keys().Count);
        }

        [TestMethod]
        public void TestClearParameter()
        {
            Parameters @params = Parameters.GetAllDefaultParameters();

            Assert.IsNotNull(@params.GetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC));

            @params.ClearParameter(Parameters.KEY.SYN_PERM_ACTIVE_INC);

            Assert.IsNull(@params.GetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC));
        }

        [TestMethod]
        public void TestLogDiff()
        {
            Parameters @params = Parameters.GetAllDefaultParameters();

            Assert.IsNotNull(@params.GetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC));

            Connections connections = new Connections();
            @params.Apply(connections);

            Parameters all = Parameters.GetAllDefaultParameters();
            all.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.9);

            bool b = all.LogDiff(connections);
            Assert.IsTrue(b);
        }

        [TestMethod]
        public void TestSetterMethods()
        {
            Parameters @params = Parameters.GetAllDefaultParameters();

            @params.SetCellsPerColumn(42);
            Assert.AreEqual(42, @params.GetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN));

            @params.SetActivationThreshold(42);
            Assert.AreEqual(42, @params.GetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD));

            @params.SetLearningRadius(42);
            Assert.AreEqual(42, @params.GetParameterByKey(Parameters.KEY.LEARNING_RADIUS));

            @params.SetMinThreshold(42);
            Assert.AreEqual(42, @params.GetParameterByKey(Parameters.KEY.MIN_THRESHOLD));

            @params.SetMaxNewSynapseCount(42);
            Assert.AreEqual(42, @params.GetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT));

            @params.SetSeed(42);
            Assert.AreEqual(42, @params.GetParameterByKey(Parameters.KEY.SEED));

            @params.SetInitialPermanence(0.82);
            Assert.AreEqual(0.82, @params.GetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE));

            @params.SetConnectedPermanence(0.82);
            Assert.AreEqual(0.82, @params.GetParameterByKey(Parameters.KEY.CONNECTED_PERMANENCE));

            @params.SetPermanenceIncrement(0.11);
            Assert.AreEqual(0.11, @params.GetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT));

            @params.SetPermanenceDecrement(0.11);
            Assert.AreEqual(0.11, @params.GetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT));

        }
    }
}
