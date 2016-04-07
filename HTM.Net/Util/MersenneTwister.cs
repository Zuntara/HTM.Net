using System;
using MathNet.Numerics.Random;

namespace HTM.Net.Util
{
    public interface IRandom
    {
        double NextDouble();
        int NextInt(int maxValue);
        double[][] GetMatrix(int rows, int cols, double? threshold = null);
    }

    // https://github.com/numenta/htm.java/blob/master/src/main/java/org/numenta/nupic/util/MersenneTwister.java
    ///** 
    // * <h3>MersenneTwister and MersenneTwisterFast</h3>
    // * <p><b>Version 20</b>, based on version MT199937(99/10/29)
    // * of the Mersenne Twister algorithm found at 
    // * <a href="http://www.math.keio.ac.jp/matumoto/emt.html">
    // * The Mersenne Twister Home Page</a>, with the initialization
    // * improved using the new 2002/1/26 initialization algorithm
    // * By Sean Luke, October 2004.
    // * 
    // * <p><b>MersenneTwister</b> is a drop-in subclass replacement
    // * for java.util.Random.  It is properly synchronized and
    // * can be used in a multithreaded environment.  On modern VMs such
    // * as HotSpot, it is approximately 1/3 slower than java.util.Random.
    // *
    // * <p><b>MersenneTwisterFast</b> is not a subclass of java.util.Random.  It has
    // * the same public methods as Random does, however, and it is
    // * algorithmically identical to MersenneTwister.  MersenneTwisterFast
    // * has hard-code inlined all of its methods directly, and made all of them
    // * final (well, the ones of consequence anyway).  Further, these
    // * methods are <i>not</i> synchronized, so the same MersenneTwisterFast
    // * instance cannot be shared by multiple threads.  But all this helps
    // * MersenneTwisterFast achieve well over twice the speed of MersenneTwister.
    // * java.util.Random is about 1/3 slower than MersenneTwisterFast.
    // *
    // * <h3>About the Mersenne Twister</h3>
    // * <p>This is a Java version of the C-program for MT19937: Integer version.
    // * The MT19937 algorithm was created by Makoto Matsumoto and Takuji Nishimura,
    // * who ask: "When you use this, send an email to: matumoto@math.keio.ac.jp
    // * with an appropriate reference to your work".  Indicate that this
    // * is a translation of their algorithm into Java.
    // *
    // * <p><b>Reference. </b>
    // * Makato Matsumoto and Takuji Nishimura,
    // * "Mersenne Twister: A 623-Dimensionally Equidistributed Uniform
    // * Pseudo-Random Number Generator",
    // * <i>ACM Transactions on Modeling and. Computer Simulation,</i>
    // * Vol. 8, No. 1, January 1998, pp 3--30.
    // *
    // * <h3>About this Version</h3>
    // *
    // * <p><b>Changes since V19:</b> nextFloat(boolean, boolean) now returns float,
    // * not double.
    // *
    // * <p><b>Changes since V18:</b> Removed old final declarations, which used to
    // * potentially speed up the code, but no longer.
    // *
    // * <p><b>Changes since V17:</b> Removed vestigial references to &amp;= 0xffffffff
    // * which stemmed from the original C code.  The C code could not guarantee that
    // * ints were 32 bit, hence the masks.  The vestigial references in the Java
    // * code were likely optimized out anyway.
    // *
    // * <p><b>Changes since V16:</b> Added nextDouble(includeZero, includeOne) and
    // * nextFloat(includeZero, includeOne) to allow for half-open, fully-closed, and
    // * fully-open intervals.
    // *
    // * <p><b>Changes Since V15:</b> Added serialVersionUID to quiet compiler warnings
    // * from Sun's overly verbose compilers as of JDK 1.5.
    // *
    // * <p><b>Changes Since V14:</b> made strictfp, with StrictMath.log and StrictMath.sqrt
    // * in nextGaussian instead of Math.log and Math.sqrt.  This is largely just to be safe,
    // * as it presently makes no difference in the speed, correctness, or results of the
    // * algorithm.
    // *
    // * <p><b>Changes Since V13:</b> clone() method CloneNotSupportedException removed.  
    // *
    // * <p><b>Changes Since V12:</b> clone() method added.  
    // *
    // * <p><b>Changes Since V11:</b> stateEquals(...) method added.  MersenneTwisterFast
    // * is equal to other MersenneTwisterFasts with identical state; likewise
    // * MersenneTwister is equal to other MersenneTwister with identical state.
    // * This isn't equals(...) because that requires a contract of immutability
    // * to compare by value.
    // *
    // * <p><b>Changes Since V10:</b> A documentation error suggested that
    // * setSeed(int[]) required an int[] array 624 long.  In fact, the array
    // * can be any non-zero length.  The new version also checks for this fact.
    // *
    // * <p><b>Changes Since V9:</b> readState(stream) and writeState(stream)
    // * provided.
    // *
    // * <p><b>Changes Since V8:</b> setSeed(int) was only using the first 28 bits
    // * of the seed; it should have been 32 bits.  For small-number seeds the
    // * behavior is identical.
    // *
    // * <p><b>Changes Since V7:</b> A documentation error in MersenneTwisterFast
    // * (but not MersenneTwister) stated that nextDouble selects uniformly from
    // * the full-open interval [0,1].  It does not.  nextDouble's contract is
    // * identical across MersenneTwisterFast, MersenneTwister, and java.util.Random,
    // * namely, selection in the half-open interval [0,1).  That is, 1.0 should
    // * not be returned.  A similar contract exists in nextFloat.
    // *
    // * <p><b>Changes Since V6:</b> License has changed from LGPL to BSD.
    // * New timing information to compare against
    // * java.util.Random.  Recent versions of HotSpot have helped Random increase
    // * in speed to the point where it is faster than MersenneTwister but slower
    // * than MersenneTwisterFast (which should be the case, as it's a less complex
    // * algorithm but is synchronized).
    // * 
    // * <p><b>Changes Since V5:</b> New empty constructor made to work the same
    // * as java.util.Random -- namely, it seeds based on the current time in
    // * milliseconds.
    // *
    // * <p><b>Changes Since V4:</b> New initialization algorithms.  See
    // * (see <a href="http://www.math.keio.ac.jp/matumoto/MT2002/emt19937ar.html">
    // * http://www.math.keio.ac.jp/matumoto/MT2002/emt19937ar.html</a>)
    // *
    // * <p>The MersenneTwister code is based on standard MT19937 C/C++ 
    // * code by Takuji Nishimura,
    // * with suggestions from Topher Cooper and Marc Rieffel, July 1997.
    // * The code was originally translated into Java by Michael Lecuyer,
    // * January 1999, and the original code is Copyright (c) 1999 by Michael Lecuyer.
    // *
    // * <h3>Java notes</h3>
    // * 
    // * <p>This implementation implements the bug fixes made
    // * in Java 1.2's version of Random, which means it can be used with
    // * earlier versions of Java.  See 
    // * <a href="http://www.javasoft.com/products/jdk/1.2/docs/api/java/util/Random.html">
    // * the JDK 1.2 java.util.Random documentation</a> for further documentation
    // * on the random-number generation contracts made.  Additionally, there's
    // * an undocumented bug in the JDK java.util.Random.nextBytes() method,
    // * which this code fixes.
    // *
    // * <p> Just like java.util.Random, this
    // * generator accepts a long seed but doesn't use all of it.  java.util.Random
    // * uses 48 bits.  The Mersenne Twister instead uses 32 bits (int size).
    // * So it's best if your seed does not exceed the int range.
    // *
    // * <p>MersenneTwister can be used reliably 
    // * on JDK version 1.1.5 or above.  Earlier Java versions have serious bugs in
    // * java.util.Random; only MersenneTwisterFast (and not MersenneTwister nor
    // * java.util.Random) should be used with them.
    // **/
    //[Serializable]
    //public class MersenneTwisterOrig : ICloneable, ISerializable, IRandom
    //{
    //    // Serialization
    //    private const long serialVersionUID = -4035832775130174188L;  // locked as of Version 15

    //    // Period parameters
    //    private const int N = 624;
    //    private const int M = 397;

    //    private static readonly int MATRIX_A;// = (int) 0x9908b0df;   //    private static final * constant vector a
    //    private static readonly int UPPER_MASK;// = (int) 0x80000000; // most significant w-r bits
    //    private const int LOWER_MASK = 0x7fffffff; // least significant r bits

    //    // Tempering parameters
    //    private static readonly int TEMPERING_MASK_B;// = (int) 0x9d2c5680;
    //    private static readonly int TEMPERING_MASK_C;// = (int) 0xefc60000;

    //    private int[] _mt; // the array for the state vector
    //    private int _mti; // mti==N+1 means mt[N] is not initialized
    //    private int[] _mag01;

    //    // a good initial seed (of int size, though stored in a long)
    //    //private static final long GOOD_SEED = 4357;

    //    /* implemented here because there's a bug in Random's implementation
    //       of the Gaussian code (divide by zero, and log(0), ugh!), yet its
    //       gaussian variables are private so we can't access them here.  :-( */

    //    private double __nextNextGaussian;
    //    private bool __haveNextNextGaussian;

    //    static MersenneTwisterOrig()
    //    {
    //        unchecked
    //        {
    //            MATRIX_A = (int)0x9908b0df;
    //            UPPER_MASK = (int)0x80000000;
    //            TEMPERING_MASK_B = (int)0x9d2c5680;
    //            TEMPERING_MASK_C = (int)0xefc60000;
    //        }
    //    }

    //    /**
    //     * Constructor using the default seed.
    //     */
    //    public MersenneTwisterOrig()
    //        : this(TimeUtils.CurrentTimeMillis())
    //    {

    //    }

    //    /**
    //     * Constructor using a given seed.  Though you pass this seed in
    //     * as a long, it's best to make sure it's actually an integer.
    //     */
    //    public MersenneTwisterOrig(int seed)
    //    {
    //        SetSeed(seed);
    //    }

    //    /**
    //     * Constructor using an array of integers as seed.
    //     * Your array must have a non-zero length.  Only the first 624 integers
    //     * in the array are used; if the array is shorter than this then
    //     * integers are repeatedly used in a wrap-around fashion.
    //     */
    //    public MersenneTwisterOrig(int[] array)
    //        //: base(TimeUtils.CurrentTimeMillis()) /* pick something at random just in case */
    //    {
    //        SetSeed(array);
    //    }


    //    /**
    //     * Initalize the pseudo random number generator.  Don't
    //     * pass in a long that's bigger than an int (Mersenne Twister
    //     * only uses the first 32 bits for its seed).   
    //     */

    //    [MethodImpl(MethodImplOptions.Synchronized)]
    //    public void SetSeed(long seed)
    //    {
    //        // it's always good style to call super
    //        //base.SetSeed(seed);

    //        // Due to a bug in java.util.Random clear up to 1.2, we're
    //        // doing our own Gaussian variable.
    //        __haveNextNextGaussian = false;

    //        _mt = new int[N];

    //        _mag01 = new int[2];
    //        _mag01[0] = 0x0;
    //        _mag01[1] = MATRIX_A;

    //        _mt[0] = (int)(seed & 0xffffffff);
    //        _mt[0] = (int)seed;
    //        for (_mti = 1; _mti < N; _mti++)
    //        {
    //            _mt[_mti] =
    //                (1812433253 * (_mt[_mti - 1] ^ (int)((uint)_mt[_mti - 1] >> 30)) + _mti);
    //            /* See Knuth TAOCP Vol2. 3rd Ed. P.106 for multiplier. */
    //            /* In the previous versions, MSBs of the seed affect   */
    //            /* only MSBs of the array mt[].                     */
    //            /* 2002/01/09 modified by Makoto Matsumoto           */
    //            // mt[mti] &= 0xffffffff;
    //            /* for >32 bit machines */
    //        }
    //    }

    //    /**
    //     * Sets the seed of the MersenneTwister using an array of integers.
    //     * Your array must have a non-zero length.  Only the first 624 integers
    //     * in the array are used; if the array is shorter than this then
    //     * integers are repeatedly used in a wrap-around fashion.
    //     */
    //    [MethodImpl(MethodImplOptions.Synchronized)]
    //    public void SetSeed(int[] array)
    //    {
    //        if (array.Length == 0)
    //            throw new ArgumentException("Array length must be greater than zero", nameof(array));
    //        int i, j, k;
    //        SetSeed(19650218);
    //        i = 1; j = 0;
    //        k = (N > array.Length ? N : array.Length);
    //        for (; k != 0; k--)
    //        {
    //            _mt[i] = ((_mt[i] ^ ((_mt[i - 1] ^ (int)(((uint)_mt[i - 1]) >> 30)) * 1664525)) + array[j] + j); /* non linear */
    //                                                                                                             // mt[i] &= 0xffffffff; /* for WORDSIZE > 32 machines */
    //            i++;
    //            j++;
    //            if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
    //            if (j >= array.Length) j = 0;
    //        }
    //        for (k = N - 1; k != 0; k--)
    //        {
    //            _mt[i] = ((_mt[i] ^ ((_mt[i - 1] ^ (int)(((uint)_mt[i - 1]) >> 30)) * 1566083941)) - i); /* non linear */
    //                                                                                                     // mt[i] &= 0xffffffff; /* for WORDSIZE > 32 machines */
    //            i++;
    //            if (i >= N)
    //            {
    //                _mt[0] = _mt[N - 1]; i = 1;
    //            }
    //        }
    //        unchecked
    //        {
    //            _mt[0] = (int)0x80000000; /* MSB is 1; assuring non-zero initial array */
    //        }
    //    }

    //    /**
    //     * Returns an integer with <i>bits</i> bits filled with a random number.
    //     */
    //    [MethodImpl(MethodImplOptions.Synchronized)]
    //    protected int Next(int bits)
    //    {
    //        int y;

    //        if (_mti >= N)              // generate N words at one time
    //        {
    //            int kk;
    //            int[] mt = _mt;         // locals are slightly faster 
    //            int[] mag01 = _mag01;   // locals are slightly faster 

    //            for (kk = 0; kk < N - M; kk++)
    //            {
    //                y = ((mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK));
    //                mt[kk] = (int)(mt[kk + M] ^ (int)((uint)(y) >> 1) ^ mag01[y & 0x1]);
    //            }
    //            for (; kk < N - 1; kk++)
    //            {
    //                y = ((mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK));
    //                mt[kk] = (int) (mt[kk + (M - N)] ^ (int)((uint)(y) >> 1) ^ mag01[y & 0x1]);
    //            }
    //            y = ((mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK));
    //            mt[N - 1] = (int) (mt[M - 1] ^ (int)((uint)(y) >> 1) ^ mag01[y & 0x1]);

    //            _mti = 0;
    //        }

    //        y = _mt[_mti++];
    //        y ^= (int)((uint)y >> 11);                  // TEMPERING_SHIFT_U(y)
    //        y ^= (y << 7) & TEMPERING_MASK_B;           // TEMPERING_SHIFT_S(y)
    //        y ^= (y << 15) & TEMPERING_MASK_C;          // TEMPERING_SHIFT_T(y)
    //        y ^= (int)((uint)y >> 18);                  // TEMPERING_SHIFT_L(y)

    //        return (int)((uint)y >> (32 - bits));       // hope that's right!
    //    }

    //    /** This method is missing from jdk 1.0.x and below.  JDK 1.1
    //    includes this for us, but what the heck.*/
    //    public bool NextBoolean() { return Next(1) != 0; }

    //    /** This generates a coin flip with a probability <tt>probability</tt>
    //    of returning true, else returning false. <tt>probability</tt> must
    //    be between 0.0 and 1.0, inclusive.  Not as precise a random real
    //    event as nextBoolean(double), but twice as fast. To explicitly
    //    use this, remember you may need to cast to float first. */
    //    public bool NextBoolean(float probability)
    //    {
    //        if (probability < 0.0f || probability > 1.0f)
    //            throw new ArgumentException("probability must be between 0.0 and 1.0 inclusive.", nameof(probability));
    //        if (probability == 0.0f) return false;            // fix half-open issues
    //        else if (probability == 1.0f) return true;        // fix half-open issues
    //        return NextFloat() < probability;
    //    }

    //    /** This generates a coin flip with a probability <tt>probability</tt>
    //    of returning true, else returning false. <tt>probability</tt> must
    //    be between 0.0 and 1.0, inclusive. */
    //    public bool NextBoolean(double probability)
    //    {
    //        if (probability < 0.0 || probability > 1.0)
    //            throw new ArgumentException("probability must be between 0.0 and 1.0 inclusive.", nameof(probability));
    //        if (probability == 0.0) return false;          // fix half-open issues
    //        else if (probability == 1.0) return true; // fix half-open issues
    //        return NextDouble() < probability;
    //    }

    //    public int NextInt()
    //    {
    //        return NextInt(int.MaxValue);
    //    }

    //    /** This method is missing from JDK 1.1 and below.  JDK 1.2
    //    includes this for us, but what the heck. */
    //    public int NextInt(int n)
    //    {
    //        if (n <= 0)
    //            throw new ArgumentException("n must be positive, got: " + n);

    //        if ((n & -n) == n)
    //            return (int)((n * (long)Next(31)) >> 31);

    //        int bits, val;
    //        do
    //        {
    //            bits = Next(31);
    //            val = bits % n;
    //        }
    //        while (bits - val + (n - 1) < 0);
    //        return val;
    //    }

    //    /** This method is for completness' sake. 
    //    Returns a long drawn uniformly from 0 to n-1.  Suffice it to say,
    //    n must be &gt; 0, or an IllegalArgumentException is raised. */
    //    public long NextLong(long n)
    //    {
    //        if (n <= 0)
    //            throw new ArgumentException("n must be positive, got: " + n);

    //        long bits, val;
    //        do
    //        {
    //            bits = (long)(((ulong)NextLong()) >> 1);
    //            val = bits % n;
    //        }
    //        while (bits - val + (n - 1) < 0);
    //        return val;
    //    }

    //    /** For completeness' sake, though it's not in java.util.Random.  */
    //    public long NextLong()
    //    {
    //        return Next(64);
    //    }

    //    /** A bug fix for versions of JDK 1.1 and below.  JDK 1.2 fixes
    //        this for us, but what the heck. */
    //    public new double NextDouble()
    //    {
    //        return (((long)Next(26) << 27) + Next(27)) / (double)(1L << 53);
    //    }

    //    /** Returns a double in the range from 0.0 to 1.0, possibly inclusive of 0.0 and 1.0 themselves.  Thus:
    //        <p><table border=0>
    //        <table>
    //        <tr><td>Expression</td><td>Interval</td></tr>
    //        <tr><td>nextDouble(false, false)</td><td>(0.0, 1.0)</td></tr>
    //        <tr><td>nextDouble(true, false)</td><td>[0.0, 1.0)</td></tr>
    //        <tr><td>nextDouble(false, true)</td><td>(0.0, 1.0]</td></tr>
    //        <tr><td>nextDouble(true, true)</td><td>[0.0, 1.0]</td></tr>
    //        </table>

    //        <p>This version preserves all possible random values in the double range.
    //    */
    //    public double NextDouble(bool includeZero, bool includeOne)
    //    {
    //        double d = 0.0;
    //        do
    //        {
    //            d = NextDouble();                          // grab a value, initially from half-open [0.0, 1.0)
    //            if (includeOne && NextBoolean()) d += 1.0;  // if includeOne, with 1/2 probability, push to [1.0, 2.0)
    //        }
    //        while ((d > 1.0) ||                            // everything above 1.0 is always invalid
    //            (!includeZero && d == 0.0));            // if we're not including zero, 0.0 is invalid
    //        return d;
    //    }

    //    /** A bug fix for versions of JDK 1.1 and below.  JDK 1.2 fixes
    //        this for us, but what the heck. */
    //    public float NextFloat()
    //    {
    //        return Next(24) / ((float)(1 << 24));
    //    }

    //    /** Returns a float in the range from 0.0f to 1.0f, possibly inclusive of 0.0f and 1.0f themselves.  Thus:
    //       <p><table border=0>
    //       <table>
    //       <tr><td>Expression</td><td>Interval</td></tr>
    //       <tr><td>nextFloat(false, false)</td><td>(0.0f, 1.0f)</td></tr>
    //       <tr><td>nextFloat(true, false)</td><td>[0.0f, 1.0f)</td></tr>
    //       <tr><td>nextFloat(false, true)</td><td>(0.0f, 1.0f]</td></tr>
    //       <tr><td>nextFloat(true, true)</td><td>[0.0f, 1.0f]</td></tr>
    //       </table>

    //       <p>This version preserves all possible random values in the float range.
    //   */
    //    public float NextFloat(bool includeZero, bool includeOne)
    //    {
    //        float d = 0.0f;
    //        do
    //        {
    //            d = NextFloat();                            // grab a value, initially from half-open [0.0f, 1.0f)
    //            if (includeOne && NextBoolean()) d += 1.0f; // if includeOne, with 1/2 probability, push to [1.0f, 2.0f)
    //        }
    //        while ((d > 1.0f) ||                          // everything above 1.0f is always invalid
    //            (!includeZero && d == 0.0f));          // if we're not including zero, 0.0f is invalid
    //        return d;
    //    }

    //    /** A bug fix for all versions of the JDK.  The JDK appears to
    //    use all four bytes in an integer as independent byte values!
    //    Totally wrong. I've submitted a bug report. */
    //    public new void NextBytes(byte[] bytes)
    //    {
    //        for (int x = 0; x < bytes.Length; x++) bytes[x] = (byte)Next(8);
    //    }

    //    /** For completeness' sake, though it's not in java.util.Random.  */
    //    public char NextChar()
    //    {
    //        // chars are 16-bit UniCode values
    //        return (char)(Next(16));
    //    }

    //    /** For completeness' sake, though it's not in java.util.Random. */
    //    public short NextShort()
    //    {
    //        return (short)(Next(16));
    //    }

    //    /** For completeness' sake, though it's not in java.util.Random.  */
    //    public byte NextByte()
    //    {
    //        return (byte)(Next(8));
    //    }

    //    [MethodImpl(MethodImplOptions.Synchronized)]
    //    public double NextGaussian()
    //    {
    //        if (__haveNextNextGaussian)
    //        {
    //            __haveNextNextGaussian = false;
    //            return __nextNextGaussian;
    //        }
    //        else
    //        {
    //            double v1, v2, s;
    //            do
    //            {
    //                v1 = 2 * NextDouble() - 1; // between -1.0 and 1.0
    //                v2 = 2 * NextDouble() - 1; // between -1.0 and 1.0
    //                s = v1 * v1 + v2 * v2;
    //            } while (s >= 1 || s == 0);
    //            double multiplier = Math.Sqrt(-2 * Math.Log(s) / s);
    //            __nextNextGaussian = v2 * multiplier;
    //            __haveNextNextGaussian = true;
    //            return v1 * multiplier;
    //        }
    //    }

    //    public bool StateEquals(object o)
    //    {
    //        if (o == this) return true;
    //        if (o == null || !(o is MersenneTwisterOrig))
    //            return false;
    //        MersenneTwisterOrig other = (MersenneTwisterOrig)o;
    //        if (_mti != other._mti) return false;
    //        for (int x = 0; x < _mag01.Length; x++)
    //            if (_mag01[x] != other._mag01[x]) return false;
    //        for (int x = 0; x < _mt.Length; x++)
    //            if (_mt[x] != other._mt[x]) return false;
    //        return true;
    //    }

    //    /** Reads the entire state of the MersenneTwister RNG from the stream */
    //    public void ReadState(BinaryReader stream)
    //    {
    //        int len = _mt.Length;
    //        for (int x = 0; x < len; x++) _mt[x] = stream.ReadInt32();

    //        len = _mag01.Length;
    //        for (int x = 0; x < len; x++) _mag01[x] = stream.ReadInt32();

    //        _mti = stream.ReadInt32();
    //        __nextNextGaussian = stream.ReadDouble();
    //        __haveNextNextGaussian = stream.ReadBoolean();
    //    }

    //    /** Writes the entire state of the MersenneTwister RNG to the stream */
    //    public void WriteState(BinaryWriter stream)
    //    {
    //        int len = _mt.Length;
    //        for (int x = 0; x < len; x++) stream.Write(_mt[x]);

    //        len = _mag01.Length;
    //        for (int x = 0; x < len; x++) stream.Write(_mag01[x]);

    //        stream.Write(_mti);
    //        stream.Write(__nextNextGaussian);
    //        stream.Write(__haveNextNextGaussian);
    //    }



    //    public object Clone()
    //    {
    //        try
    //        {
    //            IFormatter formatter = new BinaryFormatter();
    //            Stream stream = new MemoryStream();
    //            MersenneTwisterOrig cloned;
    //            using (stream)
    //            {
    //                formatter.Serialize(stream, this);
    //                stream.Seek(0, SeekOrigin.Begin);
    //                cloned = (MersenneTwisterOrig)formatter.Deserialize(stream);
    //            }

    //            cloned._mt = (int[])(_mt.Clone());
    //            cloned._mag01 = (int[])(_mag01.Clone());
    //            return cloned;
    //        }
    //        catch (Exception)
    //        {
    //            // should never happen
    //            throw;
    //        }
    //    }


    //    [MethodImpl(MethodImplOptions.Synchronized)]
    //    public void GetObjectData(SerializationInfo info, StreamingContext context)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    [Serializable]
    public class MersenneTwister : MathNet.Numerics.Random.MersenneTwister, IRandom
    {
        public MersenneTwister()
            : base(42)
        {

        }

        public MersenneTwister(int seed)
            : base(seed)
        {

        }

        public MersenneTwister(bool threadSafe)
            : base(threadSafe)
        {

        }

        public MersenneTwister(int seed, bool threadSafe)
            : base(seed, threadSafe)
        {

        }

        public int NextInt(int maxValue)
        {
            return Next(maxValue);
        }

        public int NextInt()
        {
            return Next();
        }

        public double NextDouble(double min, double max)
        {
            return min + (base.NextDouble() * (max - min));
        }

        public short NextShort(short maxValue)
        {
            return (short)Next(maxValue);
        }

        public short NextShort()
        {
            return (short)Next(short.MaxValue);
        }
        public char NextChar()
        {
            return (char)Next(short.MaxValue);
        }
        public bool NextBoolean()
        {
            return ((Random)this).NextBoolean();
        }

        public long NextLong()
        {
            return ((Random)this).NextFullRangeInt64();
        }
        public byte NextByte()
        {
            return ((Random)this).NextBytes(1)[0];
        }

        public bool NextBoolean(float probability)
        {
            if (probability < 0.0f || probability > 1.0f)
                throw new ArgumentException("probability must be between 0.0 and 1.0 inclusive.", nameof(probability));
            if (Math.Abs(probability) < float.Epsilon) return false;            // fix half-open issues
            else if (Math.Abs(probability - 1.0f) < float.Epsilon) return true;        // fix half-open issues
            return NextDouble() < probability;
        }

        public bool NextBoolean(double probability)
        {
            if (probability < 0.0f || probability > 1.0f)
                throw new ArgumentException("probability must be between 0.0 and 1.0 inclusive.", nameof(probability));
            if (Math.Abs(probability) < double.Epsilon) return false;            // fix half-open issues
            else if (Math.Abs(probability - 1.0d) < double.Epsilon) return true;        // fix half-open issues
            return NextDouble() < probability;
        }

        public double[][] GetMatrix(int rows, int cols, double? threshold = null)
        {
            double[][] matrix = new double[rows][];
            for (int i = 0; i < rows; i++)
            {
                matrix[i] = new double[cols];
                for (int j = 0; j < cols; j++)
                {
                    matrix[i][j] = NextDouble();
                    if (threshold.HasValue && matrix[i][j] <= threshold.Value)
                    {
                        matrix[i][j] = 0.0;
                    }
                }
            }
            return matrix;
        }

        public T Choice<T>(T[] array)
        {
            return array[Next(0, array.Length)];
        }
    }

    public class XorshiftRandom : Xorshift, IRandom
    {
        public XorshiftRandom(int seed)
            : base(seed)
        {

        }

        public double[][] GetMatrix(int rows, int cols, double? threshold = null)
        {
            double[][] matrix = new double[rows][];
            for (int i = 0; i < rows; i++)
            {
                matrix[i] = new double[cols];
                for (int j = 0; j < cols; j++)
                {
                    matrix[i][j] = NextDouble();
                    if (threshold.HasValue && matrix[i][j] <= threshold.Value)
                    {
                        matrix[i][j] = 0.0;
                    }
                }
            }
            return matrix;
        }

        public int NextInt(int maxValue)
        {
            return Next(maxValue);
        }
    }

    public static class TimeUtils
    {
        private static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int CurrentTimeMillis()
        {
            return (int)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }
    }
}
