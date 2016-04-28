using System;
using System.Threading;
using HTM.Net.Research.Taurus.MetricCollectors;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace HTM.Net.Research.Tests.Taurus
{
    [TestClass]
    public class CollectorAgentTests
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(AnomalyLikelihoodHelperTests));

        [TestMethod]
        public void TestStartAndStopOfCollector()
        {
            TwitterCollectorAgent agent = new TwitterCollectorAgent();
            agent.StartCollector();

            Thread.Sleep(1000);

            agent.StopCollector(); // Waits on the tasks to stop

            Assert.IsTrue(agent.CollectionTask.IsCompleted);
            Assert.IsTrue(agent.GarbageCollectionTask.IsCompleted);
            Assert.IsTrue(agent.ForwarderTask.IsCompleted);
            Assert.IsFalse(agent.CollectionTask.IsFaulted);
            Assert.IsFalse(agent.GarbageCollectionTask.IsFaulted);
            Assert.IsFalse(agent.ForwarderTask.IsFaulted);
        }

        [TestMethod]
        public void TestCancellationErrorOfCollector()
        {
            FaultingCollector agent = new FaultingCollector();

            agent.StartCollector();

            Thread.Sleep(1000);

            agent.StopCollector();

            Assert.IsTrue(agent.CollectionTask.IsCompleted);
            Assert.IsTrue(agent.CollectionTask.IsFaulted);
            Assert.IsFalse(agent.CollectionTask.IsCanceled);

            Assert.IsTrue(agent.GarbageCollectionTask.IsCompleted);
            Assert.IsTrue(agent.GarbageCollectionTask.IsFaulted);
            Assert.IsFalse(agent.GarbageCollectionTask.IsCanceled); // just throwing cancel does not cancel!

            Assert.IsTrue(agent.ForwarderTask.IsCompleted);
            Assert.IsFalse(agent.ForwarderTask.IsFaulted);
            Assert.IsFalse(agent.ForwarderTask.IsCanceled);
        }
    }

    internal class FaultingCollector : MetricCollectorAgent
    {
        protected override void ExecuteCollectionTask()
        {
            throw new System.NotImplementedException(); // this should give a faulted result
        }

        protected override void ExecuteGarbageCollectionTask()
        {
            throw new OperationCanceledException(); // this should cancel the result
        }

        protected override void ExecuteForwarderTask()
        {
            // should run to completion
        }
    }
}