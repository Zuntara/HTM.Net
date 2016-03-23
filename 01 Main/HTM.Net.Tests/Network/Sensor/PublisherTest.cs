using System;
using System.Collections.Generic;
using System.Reactive;
using HTM.Net.Network.Sensor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network.Sensor
{
    /**
 * Tests the structured process for building an {@link Observable}
 * emitter ({@code Publisher}) which can validate a manually constructed
 * input header and allow manual entry.
 * 
 * @author David Ray
 * @see Publisher
 * @see Header
 * @see ObservableSensor
 * @see ObservableSensorTest
 */
    [TestClass]
    public class PublisherTest
    {

        [TestMethod]
        public void TestHeaderConstructionAndManualEntry()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("timestamp,consumption")
                .AddHeader("datetime,float")
                .AddHeader("B")
                .Build();

            List<String> collected = new List<String>();
            //manual.subscribe(new Observer<String>() {
            //        @Override public void onCompleted() { }
            //    @Override public void onError(Throwable e) { e.printStackTrace(); }
            //    @Override public void onNext(String output)
            //    {
            //        collected.add(output);
            //    }
            //});
            manual.Subscribe(Observer.Create<string>(output =>
            {
                collected.Add(output);
            }, e => Console.WriteLine(e)));

            Assert.AreEqual(3, collected.Count);

            string[] entries =
            {
                "7/2/10 0:00,21.2",
                "7/2/10 1:00,34.0",
                "7/2/10 2:00,40.4",
                "7/2/10 3:00,123.4",
            };

            foreach (string s in entries)
            {
                manual.OnNext(s);
            }


            Assert.AreEqual(7, collected.Count);
        }

        [TestMethod]
        public void TestHeader()
        {
            try
            {
                Publisher.GetBuilder().Build();
                Assert.Fail();
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Header not properly formed (must contain 3 lines) see Header.cs", e.Message);
            }

        }
    }
}