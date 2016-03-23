using System;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Algorithms
{
    /**
 * Helper class for computing moving average and sliding window
 * 
 * @author Numenta
 * @author David Ray
 */
    public class MovingAverage
    {
        private Calculation calc;

        private int windowSize;

        /**
         * Constructs a new {@code MovingAverage}
         * 
         * @param historicalValues  list of entry values
         * @param windowSize        length over which to take the average
         */
        public MovingAverage(List<double> historicalValues, int windowSize)
            : this(historicalValues, -1, windowSize)
        {
            
        }

        /**
         * Constructs a new {@code MovingAverage}
         * 
         * @param historicalValues  list of entry values
         * @param windowSize        length over which to take the average
         */
        public MovingAverage(List<double> historicalValues, double total, int windowSize)
        {
            if (windowSize <= 0)
            {
                throw new ArgumentException("Window size must be > 0");
            }
            this.windowSize = windowSize;

            calc = new Calculation();
            calc.historicalValues =
                historicalValues == null || historicalValues.Count < 1 ?
                    new List<double>(windowSize) : historicalValues;
            calc.total = total != -1 ? total : calc.historicalValues.Sum();
        }

        /**
         * Routine for computing a moving average
         * 
         * @param slidingWindow     a list of previous values to use in the computation that
         *                          will be modified and returned
         * @param total             total the sum of the values in the  slidingWindow to be used in the
         *                          calculation of the moving average
         * @param newVal            newVal a new number to compute the new windowed average
         * @param windowSize        windowSize how many values to use in the moving window
         * @return
         */
        public static Calculation Compute(List<double> slidingWindow, double total, double newVal, int windowSize)
        {
            return Compute(null, slidingWindow, total, newVal, windowSize);
        }

        /**
         * Internal method which does actual calculation
         * 
         * @param calc              Re-used calculation object
         * @param slidingWindow     a list of previous values to use in the computation that
         *                          will be modified and returned
         * @param total             total the sum of the values in the  slidingWindow to be used in the
         *                          calculation of the moving average
         * @param newVal            newVal a new number to compute the new windowed average
         * @param windowSize        windowSize how many values to use in the moving window
         * @return
         */
        private static Calculation Compute(Calculation calc, List<double> slidingWindow, double total, double newVal, int windowSize)
        {

            if (slidingWindow == null)
            {
                throw new ArgumentException("slidingWindow cannot be null.");
            }

            if (slidingWindow.Count == windowSize)
            {
                var substract = slidingWindow[0];
                slidingWindow.RemoveAt(0);
                total -= substract;
            }
            slidingWindow.Add(newVal);
            total += newVal;

            if (calc == null)
            {
                return new Calculation(slidingWindow, total / (double)slidingWindow.Count, total);
            }

            return copyInto(calc, slidingWindow, total / (double)slidingWindow.Count, total);
        }

        /**
         * Called to compute the next moving average value.
         * 
         * @param newValue  new point data
         * @return
         */
        public double Next(double newValue)
        {
            Compute(calc, calc.historicalValues, calc.total, newValue, windowSize);
            return calc.average;
        }

        /**
         * Returns the sliding window buffer used to calculate the moving average.
         * @return
         */
        public List<double> GetSlidingWindow()
        {
            return calc.historicalValues;
        }

        /**
         * Returns the current running total
         * @return
         */
        public double GetTotal()
        {
            return calc.total;
        }

        /**
         * Returns the size of the window over which the 
         * moving average is computed.
         * 
         * @return
         */
        public int GetWindowSize()
        {
            return windowSize;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((calc == null) ? 0 : calc.GetHashCode());
            result = prime * result + windowSize;
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            MovingAverage other = (MovingAverage)obj;
            if (calc == null)
            {
                if (other.calc != null)
                    return false;
            }
            else if (!calc.Equals(other.calc))
                return false;
            if (windowSize != other.windowSize)
                return false;
            return true;
        }

        /**
         * Internal method to update running totals.
         * 
         * @param c
         * @param slidingWindow
         * @param value
         * @param total
         * @return
         */
        private static Calculation copyInto(Calculation c, List<double> slidingWindow, double average, double total)
        {
            c.historicalValues = slidingWindow;
            c.average = average;
            c.total = total;
            return c;
        }

        /**
         * Container for calculated data
         */
        public class Calculation
        {
            internal double average;
            internal List<double> historicalValues;
            internal double total;

            public Calculation()
            {

            }

            public Calculation(List<double> historicalValues, double currentValue, double total)
            {
                this.average = currentValue;
                this.historicalValues = historicalValues;
                this.total = total;
            }

            /**
             * Returns the current value at this point in the calculation.
             * @return
             */
            public double GetAverage()
            {
                return average;
            }

            /**
             * Returns a list of calculated values in the order of their
             * calculation.
             * 
             * @return
             */
            public List<double> GetHistoricalValues()
            {
                return historicalValues;
            }

            /**
             * Returns the total
             * @return
             */
            public double GetTotal()
            {
                return total;
            }

            public override int GetHashCode()
            {
                const int prime = 31;
                int result = 1;
                long temp;
                temp = BitConverter.DoubleToInt64Bits(average);
                result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
                result = prime * result + ((historicalValues == null) ? 0 : historicalValues.GetHashCode());
                temp = BitConverter.DoubleToInt64Bits(total);
                result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
                return result;
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                    return true;
                if (obj == null)
                    return false;
                if (GetType() != obj.GetType())
                    return false;
                Calculation other = (Calculation)obj;
                if (BitConverter.DoubleToInt64Bits(average) != BitConverter.DoubleToInt64Bits(other.average))
                    return false;
                if (historicalValues == null)
                {
                    if (other.historicalValues != null)
                        return false;
                }
                else if (!historicalValues.Equals(other.historicalValues))
                    return false;
                if (BitConverter.DoubleToInt64Bits(total) != BitConverter.DoubleToInt64Bits(other.total))
                    return false;
                return true;
            }

        }
    }
}