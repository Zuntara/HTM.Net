using System.IO;
using HTM.Net.Datagen;
using HTM.Net.Network.Sensor;
using HTM.Net.Tests.Properties;
using HTM.Net.Util;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network.Sensor
{
    [TestClass]
    public class FileSensorTest
    {

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.csv")]
        public void TestFileSensorCreation()
        {
            object[] n = { "some name", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.csv") };
            SensorParams parms = SensorParams.Create(SensorParams.Keys.Path, n);
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(FileSensor.Create, parms);

            Assert.IsNotNull(sensor);
            Assert.IsNotNull(sensor.GetSensorParams());
            SensorParams sp = sensor.GetSensorParams();
            Assert.AreEqual("some name", sp.Get("FILE"));
            Assert.AreEqual(null, sp.Get("NAME"));
            Assert.AreEqual(ResourceLocator.Path(typeof(Resources), "rec-center-hourly.csv"), sp.Get("PATH"));

            Sensor<FileInfo> sensor2 = Sensor< FileInfo>.Create(FileSensor.Create,SensorParams.Create(SensorParams.Keys.Path, n));

            Assert.IsNotNull(sensor2);
            Assert.IsNotNull(sensor2.GetSensorParams());
            sp = sensor2.GetSensorParams();
            Assert.AreEqual("some name", sp.Get("FILE"));
            Assert.AreEqual(null, sp.Get("NAME"));
            Assert.AreEqual(ResourceLocator.Path(typeof(Resources), "rec-center-hourly.csv"), sp.Get("PATH"));
        }


    }
}