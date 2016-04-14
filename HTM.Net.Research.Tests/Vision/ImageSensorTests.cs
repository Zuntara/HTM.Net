using System;
using System.Drawing;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Vision.Sensor;
using HTM.Net.Util;
using Kaliko.ImageLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Vision
{
    [TestClass]
    public class ImageSensorTests
    {
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void Create_ImageSensor_NoArgs()
        {
            Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
                SensorParams.Keys.Image, null));
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void Create_ImageSensor_EmptyArgs()
        {
            Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
                SensorParams.Keys.Image, new ImageSensorConfig()));
        }

        [TestMethod]
        public void Create_ImageSensor_LoadSingleImage()
        {
            HTMSensor<ImageDefinition> htmSensor = (HTMSensor<ImageDefinition>)Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
                SensorParams.Keys.Image, new ImageSensorConfig
                {
                    ExplorerConfig = new ExplorerConfig
                    {
                        ExplorerName = "ImageSweep"
                    }
                }));
            ImageSensor sensor = (ImageSensor)htmSensor.GetDelegateSensor();

            Assert.IsNotNull(sensor);

            Bitmap b1s = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b1s))
            {
                g.FillRectangle(Brushes.White, 0, 0, 32, 32);
                g.DrawRectangle(new Pen(Color.Black, 1), 10, 10, 20, 20);
            }
            KalikoImage b1 = new KalikoImage(b1s);

            var loadedResultTuple = sensor.LoadSingleImage(b1, categoryName: "1");
            Assert.IsNotNull(loadedResultTuple);
            Assert.AreEqual(1, loadedResultTuple.Get(0));
            Assert.AreEqual(0, loadedResultTuple.Get(1));
        }

        [TestMethod]
        public void Create_ImageSensor_LoadSingleImage_Compute()
        {
            HTMSensor<ImageDefinition> htmSensor = (HTMSensor<ImageDefinition>)Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
                SensorParams.Keys.Image, new ImageSensorConfig
                {
                    Width = 32,
                    Height = 32,
                    ExplorerConfig = new ExplorerConfig
                    {
                        ExplorerName = "ImageSweep"
                    }
                }));
            ImageSensor sensor = (ImageSensor)htmSensor.GetDelegateSensor();

            Assert.IsNotNull(sensor);

            Bitmap b1s = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b1s))
            {
                g.FillRectangle(Brushes.White, 0, 0, 32, 32);
                g.DrawRectangle(new Pen(Color.Black, 1), 10, 10, 20, 20);
            }
            KalikoImage b1 = new KalikoImage(b1s);

            var loadedResultTuple = sensor.LoadSingleImage(b1, categoryName: "1");
            Assert.IsNotNull(loadedResultTuple);
            Assert.AreEqual(1, loadedResultTuple.Get(0));
            Assert.AreEqual(0, loadedResultTuple.Get(1));

            Assert.IsFalse(htmSensor.EndOfStream());

            sensor.Compute(); // do the internal magic :)

            Assert.IsTrue(htmSensor.EndOfStream(), "Expected the stream to be at it's end");
        }

        [TestMethod]
        public void Create_ImageSensor_LoadSpecificImages_Compute()
        {
            HTMSensor<ImageDefinition> htmSensor = (HTMSensor<ImageDefinition>)Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
                SensorParams.Keys.Image, new ImageSensorConfig
                {
                    Width = 32,
                    Height = 32,
                    ExplorerConfig = new ExplorerConfig
                    {
                        ExplorerName = "ImageSweep"
                    }
                }));
            ImageSensor sensor = (ImageSensor)htmSensor.GetDelegateSensor();

            Assert.IsNotNull(sensor);

            Bitmap b1s = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b1s))
            {
                g.FillRectangle(Brushes.White, 0, 0, 32, 32);
                g.DrawRectangle(new Pen(Color.Black, 1), 10, 10, 20, 20);
            }
            KalikoImage b1 = new KalikoImage(b1s);

            Bitmap b2s = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b2s))
            {
                g.FillRectangle(Brushes.White, 0, 0, 32, 32);
                g.DrawRectangle(new Pen(Color.Black, 1), 15, 15, 25, 25);
            }
            KalikoImage b2 = new KalikoImage(b2s);

            var loadedResultTuple = sensor.LoadSpecificImages(new[] { b1, b2 }, new[] { "1", "2" });
            Assert.IsNotNull(loadedResultTuple);
            Assert.AreEqual(2, loadedResultTuple.Get(0));
            Assert.AreEqual(0, loadedResultTuple.Get(1));

            Assert.IsFalse(htmSensor.EndOfStream());

            sensor.Compute(); // do the internal magic :)
            sensor.Compute();

            Assert.IsTrue(htmSensor.EndOfStream(), "Expected the stream to be at it's end");
        }

        [TestMethod]
        public void Create_ImageSensor_LoadSingleImage_Compute_Filter()
        {
            HTMSensor<ImageDefinition> htmSensor =
                (HTMSensor<ImageDefinition>) Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
                    SensorParams.Keys.Image, new ImageSensorConfig
                    {
                        Width = 32,
                        Height = 32,
                        ExplorerConfig = new ExplorerConfig
                        {
                            ExplorerName = "ImageSweep"
                        },
                        FilterConfigs = new[]
                        {
                            new FilterConfig
                            {
                                FilterName = "AddNoise",
                                FilterArgs = new Map<string, object> { { "noiseLevel", 0.2 } }
                            }
                        }
                    }));
            ImageSensor sensor = (ImageSensor)htmSensor.GetDelegateSensor();

            Assert.IsNotNull(sensor);

            Bitmap b1s = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b1s))
            {
                g.FillRectangle(Brushes.White, 0, 0, 32, 32);
                g.DrawRectangle(new Pen(Color.Black, 1), 10, 10, 20, 20);
            }
            KalikoImage b1 = new KalikoImage(b1s);

            var loadedResultTuple = sensor.LoadSingleImage(b1, categoryName: "1");
            Assert.IsNotNull(loadedResultTuple);
            Assert.AreEqual(1, loadedResultTuple.Get(0));
            Assert.AreEqual(0, loadedResultTuple.Get(1));

            Assert.IsFalse(htmSensor.EndOfStream());

            sensor.Compute(); // do the internal magic :)

            Assert.IsTrue(htmSensor.EndOfStream(), "Expected the stream to be at it's end");
        }
    }
}