using System;
using System.Collections.Generic;
using System.Reactive;

using HTM.Net.Network;
using HTM.Net.Network.Sensor;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network;

[TestClass]
public class PublisherSupplierTest
{
    [TestMethod]
    public void TestPublisherCreation()
    {
        Parameters p = NetworkTestHarness.GetParameters().Copy();
        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());

        var supplier = PublisherSupplier.GetBuilder()
                                        .AddHeader("dayOfWeek")
                                        .AddHeader("number")
                                        .AddHeader("B")
                                        .Build();

        // This line invokes all the Publisher creation underneath
        Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
            ObservableSensor<string[]>.Create, 
            SensorParams.Create(SensorParams.Keys.Obs, new object[] { "name", supplier }));

        ///////////////////////////////////////
        //   Now Test Publisher was created  //
        ///////////////////////////////////////

        Publisher pub = supplier.Get();
        Assert.IsNotNull(pub);

        List<String> outputList = new List<String>();
        pub.Subscribe(new AnonymousObserver<string>(
            s => outputList.Add(s),
            e => Console.WriteLine(e),
            () => { }));

        pub.OnNext("" + 0);
        
        for(int i = 0;i<outputList.Count;i++)
        {
            switch (i)
            {
                case 0:
                    Assert.AreEqual("dayOfWeek", outputList[i]);
                    break;
                case 1:
                    Assert.AreEqual("number", outputList[i]);
                    break;
                case 2:
                    Assert.AreEqual("B", outputList[i]);
                    break;
                case 3:
                    Assert.AreEqual("0", outputList[i]);
                    break;
            }
        }

        // Next test pessimistic path
        Net.Network.Network network2 = Net.Network.Network.Create("testNetwork", p);
        Publisher nullPublisher = null;
        try
        {
            nullPublisher = network2.GetPublisher();
            Assert.Fail(); // Should not reach here
        }
        catch (Exception e)
        {
            Assert.AreEqual("A Supplier must be built first. " +
                            "please see Network.getPublisherSupplier()", e.Message);
        }

        Assert.IsNull(nullPublisher);
    }
}