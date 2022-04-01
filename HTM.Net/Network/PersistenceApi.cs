using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HTM.Net.Model;
using HTM.Net.Serialize;
using log4net;

namespace HTM.Net.Network
{
    /**
     * <p>
     * Executes check point behavior through the {@link #checkPoint(Observer)} method. The
     * checkPoint() method adds the specified {@link rx.Observer} to the list of those
     * observers notified following a check point operation. This "subscribe" action invokes
     * the underlying check point operation and returns a notification. The notification consists of
     * a byte[] containing the serialized {@link Network}.
     * </p><p>
     * <b>Typical usage is as follows:</b>
     * <pre>
     *  {@link Persistence} p = Persistence.get();
     *  
     *  p.checkPointOp().checkPoint(new Observer<byte[]>() { 
     *      public void onCompleted() {}
     *      public void onError(Throwable e) { e.printStackTrace(); }
     *      public void onNext(byte[] bytes) {
     *          // Do work here, use serialized Network byte[] here if desired...
     *      }
     *  });
     * 
     * Again, by subscribing to this CheckPointOp, the Network knows to check point after completion of 
     * the current compute cycle (it checks the List of Observers to see if it's non-empty).
     * Then after it notifies all current observers, it clears the list prior to the next 
     * following compute cycle. see {@link PAPI} for a more detailed discussion...
     * 
     * @author cogmission
     *
     * @param <T>  the notification return type
     */
    public interface ICheckPointOp<T>
    {
        /**
         * Registers the Observer for a single notification following the checkPoint
         * operation. The user will be notified with the byte[] of the {@link Network}
         * being serialized.
         * 
         * @param t     a {@link rx.Observer}
         * @return  a Subscription object which is meaningless.
         */
        IDisposable CheckPoint(IObserver<T> t);
    }

    /**
     * <p>
     * Offers 4 basic types of functionality:
     * </p>
     * <ul>
     *  <li>Obtain an instance of the low-level serializer</li>
     *  <li>Provide convenience methods for storing a {@link Network}</li>
     *  <li>Provide a method to <b>Check Point</b> a {@link Network} (snapshot it to the local file system).
     *  <li>Provide generic methods to store any HTM object which is {@link Persistable}</li>
     * </ul>
     * </p>
     * <p>
     * While the generic methods will work for the entire {@link Network} (since it is {@link Persistable}),
     * there are convenience methods to store a Network specifically. These come in the form of "store" and 
     * "load" methods:
     * <ul>
     *  <li>{@link #store(Network)}</li> 
     *  <li>{@link #storeAndGet(Network)}</li>
     *  <li>{@link #load()}</li>
     *  <li>{@link #load(String)}</li>
     * </ul>
     * </p>
     * <p>
     * The more generic persistence methods are of the form "write" and "read" as in:
     * <ul>
     *  <li>{@link #write(Persistable)}</li>
     *  <li>{@link #write(Persistable, String)}</li>
     *  <li>{@link #read(byte[])}</li>
     *  <li>{@link #read(String)}</li>
     * </ul>
     * </p>
     * <p>
     * To obtain an instance of the {@code PersistanceAPI}, simply call:
     * <pre>
     * PersistenceAPI api = Persistence.get();
     * </pre>
     * </p>
     * <p>
     * Although the PersistenceAPI is adequate for the majority of cases, you may also obtain an
     * instance of the underlying serializer (see {@link SerializerCore}) that the PersistenceAPI itself uses:
     * <pre>
     * SerializerCore core = Persistence.get().serializer();
     * </pre>
     * The core serializer can be used to serialize to and from an Input/Output stream, or to and from
     * byte arrays.
     * </p>
     * 
     * @see SerializerCore
     */
    public interface IPersistenceAPI
    {
        /**
         * Factory method to return a configured {@link NetworkSerializer}
         * 
         * If the "returnNew" flag is true, this method returns a new instance of 
         * {@link NetworkSerializer} and stores it for subsequent invocations of this
         * method. If false, the previously stored NetworkSerializer is returned.
         * 
         * @param config        the SerialConfig storing file storage parameters etc.
         * @param returnNew     NetworkSerializers are expensive to instantiate so specify
         *                      if the previous should be re-used or if you want a new one.
         * @return      a NetworkSerializer
         * @see SerialConfig
         */
        SerializerCore Serializer();
        /**
         * Convenience method to load a {@code Network} from the default or previously configured
         * location and serial file, and returns it. 
         * @return the deserialized Network
         * @see SerialConfig
         */
        Network Load();
        /**
         * Convenience method to load a {@code Network} from the specified serialized file name and
         * returns it.
         *  
         * @param fileName      the name of the serialization file.
         *    
         * @return  returns the specified Network
         * @see SerialConfig
         */
        Network Load(String fileName);
        /**
         * Convenience method to store the specified {@link Network} to the pre-configured 
         * (with {@link SerialConfig}) location and filename.
         * @param network   the {@code Network} to store
         */
        void Store(Network network);
        /**
         * Stores the specified {@link Network} at the pre-configured location, after
         * halting and shutting down the Network. To store the Network but keep it up
         * and running, please see the {@link #checkPointer()} method. 
         * 
         * The Network, may however be {@link #restart()}ed after this method is called.
         * 
         * @param <R> the type of the returned object
         * @param network       the {@link Network} to persist
         * @param returnBytes   flag indicating whether to return the interim byte array
         * 
         * @return the serialized Network in the format is either a byte[] or String (json),
         *          where byte[] is the default of type &lt;R&gt;
         */
        byte[] StoreAndGet(Network network);
        /**
         * Returns an {@link rx.Observable} operator that when subscribed to, invokes an operation
         * that stores the state of this {@code Network} while keeping the Network up and running.
         * The Network will be stored at the pre-configured location (in binary form only, not JSON).
         * 
         * @param network   the {@link Network} to check point
         * @return the {@link CheckPointOp} operator of type &lt;T&gt;
         */
        ICheckPointOp<byte[]> CheckPointer(Network network);
        /**
         * Reifies a {@link Persistable} from the specified file in the location and file name
         * configured by the {@link SerialConfig} passed in at construction time.
         * 
         * @return  the reified type &lt;R&gt;
         */
        TRead Read<TRead>() where TRead : IPersistable;
        /**
         * Reifies a {@link Persistable} from the specified file in the location
         * configured by the {@link SerialConfig} passed in at construction time.
         * 
         * @param <R> the type of the returned serialized form
         * @param fileName  the name of the file from which to get the serialized object.
         * @return  the reified type &lt;R&gt;
         */
        TRead Read<TRead>(String fileName) where TRead : IPersistable;
        /**
         * Loads a {@code Persistable} from the specified serialized byte array and
         * returns the de-serialized Persistable.
         *  
         * @param <R> the type of the returned serialized form
         * @param serializedBytes             the name of the serialization file.
         *    
         * @return  the reified type &lt;R&gt;
         */
        TRead ReadContent<TRead>(byte[] serializedBytes) where TRead : IPersistable;
        /**
         * Persists the {@link Persistable} subclass to the file system using the 
         * pre-configured {@link SerialConfig} specified at the time this object was
         * instantiated, or the default SerialConfig.
         * 
         * @param <T> the type of the stored object
         * @param <R> the type of the returned serialized form
         * @param instance  the subclass of Persistable to persist.
         * @return  a byte array containing the serialized object of type &lt;R&gt;
         */
        byte[] Write<T>(T instance) where T : IPersistable;
        /**
         * Persists the {@link Persistable} subclass to the file system using the 
         * pre-configured {@link SerialConfig} specified at the time this object was
         * instantiated, or the default SerialConfig.
         * 
         * @param <T> the type of the stored object
         * @param <R> the type of the returned serialized form
         * @param instance  the subclass of Persistable to persist.
         * @param fileName  the name of the file to which the object is stored.
         * @return  a byte array containing the serialized object of type &lt;R&gt;
         */
        byte[] Write<T>(T instance, string fileName) where T : IPersistable;
        /**
         * (optional)
         * Sets the {@link SerialConfig} for detailed control. In common practice
         * this object is initialized with a default that is fine.
         * @param config    
         */
        void SetConfig(SerialConfig config);
        /**
         * Returns the {@link SerialConfig} in use
         * @return  the SerialConfig in current use
         */
        SerialConfig GetConfig();

        /////////////////////////////////////////
        //        Convenience Methods          //
        /////////////////////////////////////////
        /**
         * Returns the last check pointed bytes of the last check point operation.
         * 
         * @return  a byte array
         */
        byte[] GetLastCheckPoint();
        /**
         * Returns the name of the most recently checkpointed {@code Network} file.
         * @return  the name of the most recently checkpointed {@code Network} file.
         */
        string GetLastCheckPointFileName();
        /**
         * Returns a {@code List} of check pointed file names.
         * @return a {@code List} of check pointed file names.
         */
        Dictionary<string, DateTime> ListCheckPointFiles();
        /**
         * Returns the checkpointed file previous to the specified file (older), or
         * null if one doesn't exist. The file name may be the entire filename (as
         * configured by the {@link SerialConfig} object which establishes both the
         * filename portion and the date portion formatting), or just the date
         * portion of the filename.
         * 
         * @param   checkPointFileName (can be entire name or just date portion)
         * 
         * @return  the full filename of the file checkpointed immediately previous
         *          to the file specified.
         */
        string GetPreviousCheckPoint(KeyValuePair<string, DateTime> checkPointData);
        /**
         * Convenience method which returns the store file fully qualified path. 
         * @return
         */
        string GetCurrentPath();
    }

    /**
     * Used to get a reference to a {@link PersistenceAPI} implementation via static
     * methods {@link Persistence#get()} and {@link Persistence#get(SerialConfig)}, where 
     * the {@link SerialConfig} object is used to determine file handling details such as:
     * <ul>
     *  <li>General {@link Network} storage file name</li>
     *  <li>The check pointed storage file name. (has two parts; name, and date - this is for the name)
     *  <li>The check pointed storage file name date extension. (the date part of the name)
     *  <li>The general storage and check pointed file directory</li> (always somewhere under the user's home directory)
     * </ul>
     * Note: there is a default constructor on {@link SerialConfig} which indicates the use of defaults which
     * are just fine for most circumstances; which is why these {@code Persistence} factory methods can be called
     * without a SerialConfig.
     * 
     * Normal usage is as follows:
     * <pre>
     * PersistenceAPI api = Peristence.get();
     * api.load()...
     * api.store()...
     * api.read()...
     * api.write()...
     * api.checkPointer(Network).checkPoint(Observer)...
     * ...
     * </pre>
     * </p>
     * <p>
     * And for low-level access only:
     * <pre>
     * SerializerCore core = api.serializer();
     * </pre>
     * </P
     * @author cogmission
     * @see SerialConfig
     * @see PersistenceAPI
     */
    public class Persistence
    {
        private static IPersistenceAPI access;

        public static IPersistenceAPI Get()
        {
            return Get(new SerialConfig());
        }

        public static IPersistenceAPI Get(SerialConfig config)
        {
            if (access == null)
            {
                access = new PersistenceAccess(config);
            }
            return access;
        }

        internal class PersistenceAccess : IPersistenceAPI
        {
            private const long serialVersionUID = 1L;

            protected static readonly ILog LOGGER = LogManager.GetLogger(typeof(IPersistenceAPI));

            // Time stamped serialization file format
            //public static DateTimeFormatInfo CHECKPOINT_TIMESTAMP_FORMAT = DateTimeFormat.forPattern(SerialConfig.CHECKPOINT_FORMAT_STRING);
            //private DateTimeFormatInfo checkPointFormatter = CHECKPOINT_TIMESTAMP_FORMAT;

            /// <summary>
            /// Indicates the underlying file settings
            /// </summary>
            private SerialConfig serialConfig;

            /// <summary>
            /// Stores the bytes of the last serialized object or null if there was a problem
            /// </summary>
            private static byte[] lastBytes;

            /// <summary>
            /// All instances in this classloader will share the same atomic reference
            /// to the last checkpoint file name holder which is perfectly fine.
            /// </summary>
            private static string lastCheckPointFileName;

            private SerializerCore defaultSerializer = new SerializerCore();

            //private ReentrantReadWriteLock rwl = new ReentrantReadWriteLock();
            //private Lock writeMonitor = rwl.writeLock();
            //private Lock readMonitor = rwl.readLock();

            public PersistenceAccess(SerialConfig config)
            {
                this.serialConfig = config == null ? new SerialConfig() : config;

                //this.checkPointFormatter = DateTimeFormat.forPattern(config.getCheckPointFormatString());
            }

            #region Implementation of IPersistenceAPI

            public SerializerCore Serializer()
            {
                if (defaultSerializer == null)
                {
                    defaultSerializer = new SerializerCore(SerialConfig.DEFAULT_REGISTERED_TYPES);
                }
                return defaultSerializer;
            }

            public Network Load()
            {
                LOGGER.Debug("PersistenceAccess load() called ...");

                String defaultFileName = serialConfig.GetFileName();
                byte[] bytes;
                try
                {
                    bytes = ReadFile(defaultFileName);
                }
                catch (IOException e)
                {
                    LOGGER.Error($"IOException in reading network: {e}");
                    throw;
                }
                Network network = Serializer().Deserialize<Network>(bytes);
                return (Network) network.PostDeSerialize();
            }

            public Network Load(string fileName)
            {
                LOGGER.Debug("PersistenceAccess load(" + fileName + ") called ...");

                byte[] bytes = ReadFile(fileName);
                Network network = Serializer().Deserialize<Network>(bytes);
                return network;
            }

            public void Store(Network network)
            {
                StoreAndGet(network);
            }

            public byte[] StoreAndGet(Network network)
            {
                // Make sure any serialized Network is first halted.
                network.PreSerialize();

                byte[] bytes = defaultSerializer.Serialize(network);

                try
                {
                    WriteFile(serialConfig, bytes);
                }
                catch (IOException e)
                {
                    LOGGER.Error($"IOException in writing network: {e}");
                    throw;
                }

                return bytes;
            }

            public ICheckPointOp<byte[]> CheckPointer(Network network)
            {
                network.SetCheckPointFunction<Network>(GetCheckPointFunction<Network>(network));
                return network.GetCheckPointOperator();
            }

            public TRead Read<TRead>() where TRead : IPersistable
            {
                LOGGER.Debug("PersistenceAccess reify() [serial config file name=" + serialConfig.GetFileName() + "] called ...");
                return Read<TRead>(serialConfig.GetFileName());
            }

            public TRead Read<TRead>(string fileName) where TRead : IPersistable
            {
                LOGGER.Debug("PersistenceAccess reify(" + fileName + ") called ...");
                byte[] bytes;
                try
                {
                    bytes = ReadFile(fileName);
                }
                catch (IOException e)
                {
                    LOGGER.Error($"Exception in reading: {e}");
                    throw;
                }
                return ReadContent<TRead>(bytes);
            }

            public TRead ReadContent<TRead>(byte[] serializedBytes) where TRead : IPersistable
            {
                LOGGER.Debug("PersistenceAccess reify(byte[]) called ...");

                TRead r = Serializer().Deserialize<TRead>(serializedBytes);
                return (TRead) r.PostDeSerialize();
            }

            public byte[] Write<T>(T instance) where T : IPersistable
            {
                LOGGER.Debug("PersistenceAccess persist(T) called ...");
                instance.PreSerialize();

                byte[] bytes = Serializer().Serialize(instance);

                try
                {
                    WriteFile(serialConfig, bytes);
                }
                catch (IOException e)
                {
                    LOGGER.Error($"IOException in writing: {e}");
                    throw;
                }

                return bytes;
            }

            public byte[] Write<T>(T instance, string fileName) where T : IPersistable
            {
                LOGGER.Debug("PersistenceAccess persist(T, " + fileName + ") called ...");
                instance.PreSerialize();

                byte[] bytes = Serializer().Serialize(instance);

                try
                {
                    WriteFile(fileName, bytes, serialConfig.GetOpenOptions());
                }
                catch (IOException e)
                {
                    LOGGER.Error($"IOException in writing: {e}");
                    throw;
                }

                return bytes;
            }

            public void SetConfig(SerialConfig config)
            {
                this.serialConfig = config;
                //this.checkPointFormatter = DateTimeFormat.forPattern(config.getCheckPointFormatString());
            }

            public SerialConfig GetConfig()
            {
                return serialConfig;
            }

            public byte[] GetLastCheckPoint()
            {
                return lastBytes;
            }

            public string GetLastCheckPointFileName()
            {
                return lastCheckPointFileName;
            }

            public Dictionary<string, DateTime> ListCheckPointFiles()
            {
                Dictionary<string, DateTime> chkPntFiles;
                try
                {
                    //readMonitor.lock () ;
                    String path = Environment.CurrentDirectory + "\\" + serialConfig.GetFileDir();
                    DirectoryInfo customDir = new DirectoryInfo(path);

                    chkPntFiles = customDir.GetFiles()
                        .Where(f => f.FullName.IndexOf(serialConfig.GetCheckPointFileName()) != -1)
                        .OrderBy(f => f.CreationTime)
                        .ToDictionary(f => f.FullName, f => f.CreationTime);
                }
                catch (Exception e)
                {
                    throw;
                }
                finally
                {
                    //readMonitor.unlock();
                }

                return chkPntFiles;
            }

            public string GetPreviousCheckPoint(KeyValuePair<string, DateTime> checkPointData)
            {
                //final DateTimeFormatter f = checkPointFormatter;

                DateTime cpfn = checkPointData.Value;

                var files = ListCheckPointFiles();

                string[] chkPntFiles = files
                     .Where(n => n.Value < cpfn)
                     .OrderBy(f => f.Value)
                     .Select(f => f.Key)
                     .ToArray();

                if (chkPntFiles.Length > 0)
                {
                    return chkPntFiles.Last();
                }

                return null;
            }

            public string GetCurrentPath()
            {
                return Environment.CurrentDirectory + "\\" + serialConfig.GetFileDir() +
                "\\" + serialConfig.GetFileName();
            }

            #endregion

            private byte[] ReadFile(string filePath)
            {
                FileInfo fileInPath = TestFileExists(filePath);
                if (!fileInPath.Exists) return null;
                try
                {
                    return File.ReadAllBytes(fileInPath.FullName);
                }
                catch (Exception e)
                {
                    LOGGER.Error("Exception in reading file: " + e);
                    throw;
                }
            }

            private FileInfo TestFileExists(string fileName)
            {
                lock (this)
                {
                    try
                    {
                        string path = Environment.CurrentDirectory + "\\" + serialConfig.GetFileDir();
                        DirectoryInfo customDir = new DirectoryInfo(path);
                        Directory.CreateDirectory(customDir.FullName);

                        FileInfo serializedFile = new FileInfo(fileName.IndexOf(customDir.FullName) != -1 ? fileName : customDir.FullName + "\\" + fileName);
                        if (!serializedFile.Exists)
                        {
                            throw new FileNotFoundException($"File '{fileName}' was not found");
                        }
                        return serializedFile;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }

                //try
                //{
                //    readMonitor.lock () ;

                //    String path = System.getProperty("user.home") + File.separator + serialConfig.getFileDir();
                //    File customDir = new File(path);
                //    // Make sure container directory exists
                //    customDir.mkdirs();

                //    File serializedFile = new File(fileName.indexOf(customDir.getAbsolutePath()) != -1 ?
                //        fileName : customDir.getAbsolutePath() + File.separator + fileName);
                //    if (!serializedFile.exists())
                //    {
                //        throw new FileNotFoundException("File \"" + fileName + "\" was not found.");
                //    }

                //    return serializedFile;
                //}
                //catch (IOException io)
                //{
                //    throw io;
                //}
                //finally
                //{
                //    readMonitor.unlock();
                //}
            }

            private void WriteFile(SerialConfig config, byte[] bytes)
            {
                WriteFile(config.GetFileName(), bytes, config.GetOpenOptions());
            }

            /**
             * Writes the file specified by "fileName" using the pre-configured location 
             * specified by the {@link SerialConfig}.
             *   
             * @param fileName          the file name to use
             * @param bytes             the content to write
             * @param options           the file handling rules to use
             * @throws IOException      if there is a problem writing the file
             */
            private void WriteFile(string fileName, byte[] bytes, params FileMode[] options)
            {
                try
                {
                    //writeMonitor.lock();
                    FileInfo path = EnsurePathExists(serialConfig, fileName);

                    using (var s = File.Open(path.FullName, options.First()))
                    {
                        s.Write(bytes, 0, bytes.Length);
                    }
                }
                catch (Exception)
                {
                    lastBytes = null;
                    throw;
                }
                finally
                {
                    //writeMonitor.unlock();
                }

                lastBytes = bytes;
            }

            internal FileInfo EnsurePathExists(SerialConfig config)
            {
                return EnsurePathExists(config, config.GetFileName());
            }

            private FileInfo EnsurePathExists(SerialConfig config, String fileName)
            {
                FileInfo serializedFile = null;

                try
                {
                    //writeMonitor.lock () ;

                    String path = Environment.CurrentDirectory + "\\" + config.GetFileDir();
                    DirectoryInfo customDir = new DirectoryInfo(path);
                    // Make sure container directory exists
                    Directory.CreateDirectory(customDir.FullName);

                    // Check to make sure the fileName doesn't already include the full path.
                    serializedFile = new FileInfo(fileName.IndexOf(customDir.FullName) != -1 ?
                        fileName : customDir.FullName + "\\" + fileName);
                    if (!serializedFile.Exists)
                    {
                        using (serializedFile.Create())
                        {
                            // just create it.
                        }
                    }
                }
                catch (Exception io)
                {
                    throw;
                }
                finally
                {
                    //writeMonitor.unlock();
                }

                return serializedFile;
            }

            /**
             * Returns a {@link Function} to set on the specified network as a callback
             * with privileged access.
             * 
             * This {@code Function} writes the state of the specified {@link Network} to the
             * pre-configured check point file location using the format specified in the 
             * {@link SerialConfig} specified during construction or later set on this object.
             * 
             * @param network       the {@link Network} to check point
             * @return  a Function which checkpoints
             */
            internal Func<T, byte[]> GetCheckPointFunction<T>(Network network)
                    where T : IPersistable
            {
                return t =>
                {
                    t.PreSerialize();

                    //string oldCheckPointFileName = lastCheckPointFileName.GetAndSet(
                    //    serialConfig.getAbsoluteSerialDir() + File.separator + serialConfig.getCheckPointFileName() +
                    //        checkPointFormatter.print(new DateTime()));
                    string oldCheckPointFileName = lastCheckPointFileName;
                    lastCheckPointFileName = serialConfig.GetAbsoluteSerialDir() + "\\" +
                                             serialConfig.GetCheckPointFileName() +
                                             DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", DateTimeFormatInfo.InvariantInfo);

                    byte[] bytes = defaultSerializer.Serialize(network);
                    try
                    {
                        WriteFile(lastCheckPointFileName, bytes, serialConfig.GetCheckPointOpenOptions());
                    }
                    catch (IOException io)
                    {
                        throw;
                    }

                    if (serialConfig.isOneCheckPointOnly() && oldCheckPointFileName != null)
                    {
                        if (File.Exists(oldCheckPointFileName))
                            File.Delete(oldCheckPointFileName);
                    }

                    return bytes;
                };
            }
        }
    }
}