using System;
using System.Text;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    public class GeospatialCoordinateEncoder : CoordinateEncoder
    {

        private int _scale;
        private int _timestep;


        public GeospatialCoordinateEncoder()
        {
            Tuple desc = new Tuple("longitude", 0);
            Tuple desc2 = new Tuple("lattitude", 1);
            Tuple desc3 = new Tuple("speed", 2);
            description.Add(desc);
            description.Add(desc2);
            description.Add(desc3);
        }

        /**
         * Returns a builder for building ScalarEncoders. 
         * This builder may be reused to produce multiple builders
         * 
         * @return a {@code CoordinateEncoder.Builder}
         */
        public static IBuilder GetGeobuilder()
        {
            return new Builder();
        }


        /**
         * {@inheritDoc}
         */
        public override void EncodeIntoArray(Tuple inputData, int[] output)
        {
            double longitude = (double)inputData.Get(0);
            double lattitude = (double)inputData.Get(1);
            double speed = (double)inputData.Get(2);
            int[] coordinate = CoordinateForPosition(longitude, lattitude);
            double radius = RadiusForSpeed(speed);

            base.EncodeIntoArray(new Tuple(coordinate, radius), output);
        }

        #region Overrides of Encoder<Tuple>

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            EncodeIntoArray((Tuple)o, tempArray);
        }

        #endregion

        public int[] CoordinateForPosition(double longitude, double lattitude)
        {
            double[] coordinate = ToMercator(longitude, lattitude);
            coordinate[0] /= _scale;
            coordinate[1] /= _scale;
            return new[] { (int)coordinate[0], (int)coordinate[1] };
        }

        /**
         * Returns coordinates converted to Mercator Spherical projection
         * 
         * @param lon	the longitude
         * @param lat	the lattitude
         * @return
         */
        internal double[] ToMercator(double lon, double lat)
        {
            double x = lon * 20037508.34d / 180;
            double y = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
            y = y * 20037508.34d / 180;

            return new[] { x, y };
        }

        /**
         * Returns coordinates converted to Long/Lat from Mercator Spherical projection
         * 
         * @param lon	the longitude
         * @param lat	the lattitude
         * @return
         */
        internal double[] InverseMercator(double x, double y)
        {
            double lon = (x / 20037508.34d) * 180;
            double lat = (y / 20037508.34d) * 180;

            lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180)) - Math.PI / 2);

            return new[] { lon, lat };
        }

        /**
         * Tries to get the encodings of consecutive readings to be
         * adjacent with some overlap.
         * 
         * @param speed	Speed (in meters per second)
         * @return	Radius for given speed
         */
        public double RadiusForSpeed(double speed)
        {
            double overlap = 1.5;
            double coordinatesPerTimestep = speed * _timestep / _scale;
            int radius = (int)Math.Round(coordinatesPerTimestep / 2D * overlap);
            int minRadius = (int)Math.Ceiling((Math.Sqrt(w) - 1) / 2);
            return Math.Max(radius, minRadius);
        }

        /**
         * Returns a {@link EncoderBuilder} for constructing {@link GeospatialCoordinateEncoder}s
         * 
         * The base class architecture is put together in such a way where boilerplate
         * initialization can be kept to a minimum for implementing subclasses, while avoiding
         * the mistake-proneness of extremely long argument lists.
         * 
         * @see ScalarEncoder.Builder#setStuff(int)
         */
        public new class Builder : BuilderBase
        {

            private int _scale;
            private int _timestep;

            internal Builder() { }

            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize 
                //boilerplate variables.
                encoder = new GeospatialCoordinateEncoder();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////
                if (_scale == 0 || _timestep == 0)
                {
                    throw new InvalidOperationException("Scale or Timestep not set");
                }

                ((GeospatialCoordinateEncoder)encoder)._scale = _scale;
                ((GeospatialCoordinateEncoder)encoder)._timestep = _timestep;

                if (w <= 0 || w % 2 == 0)
                {
                    throw new ArgumentException("w must be odd, and must be a positive integer");
                }

                if (n <= 6 * w)
                {
                    throw new ArgumentException(
                        "n must be an int strictly greater than 6*w. For " +
                           "good results we recommend n be strictly greater than 11*w");
                }

                if (name == null || name.Equals("None"))
                {
                    name = new StringBuilder("[").Append(n).Append(":").Append(w).Append("]").ToString();
                }

                return (GeospatialCoordinateEncoder)encoder;
            }

            /**
             * Scale of the map, as measured by
             * distance between two coordinates
             * (in meters per dimensional unit)
             * @param scale
             * @return
             */
            public Builder Scale(int scale)
            {
                _scale = scale;
                return this;
            }

            /**
             * Time between readings
             * @param timestep
             * @return
             */
            public Builder Timestep(int timestep)
            {
                _timestep = timestep;
                return this;
            }
        }
    }
}
