using System;
using System.Collections.Generic;
using System.IO;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Util;
using log4net;
using Tuple = System.Tuple;

namespace HTM.Net.Serialize
{
    [Serializable]
    public class SerialConfig
    {
        protected static readonly ILog LOGGER = LogManager.GetLogger(typeof(SerialConfig));

        /** The known types we are serializing */
        public static readonly Type[] DEFAULT_REGISTERED_TYPES = new Type[]
        {
            typeof (Region), typeof (Layer<>), typeof (Cell), typeof (Column), typeof (Synapse),
            typeof (ProximalDendrite), typeof (DistalDendrite), typeof (Segment), typeof (IInference),
            typeof (ManualInput), typeof (BitHistory), typeof (Tuple), typeof (NamedTuple), typeof (Parameters),
            typeof (ComputeCycle), typeof (Classification<>), typeof (FieldMetaType), typeof (Pool),
            typeof (Persistable)
        };

        /** The default format for the timestamp portion of the checkpoint file name */
        public const string CHECKPOINT_FORMAT_STRING = "YYYY-MM-dd_HH-mm-ss.SSS";

        /** Default directory to store the serialized file */
        public const String SERIAL_DIR = "HTMNetwork";
        /** Default directory to store the serialized file */
        public const String SERIAL_TEST_DIR = "HTMNetworkTest";
        /** Default serialized Network file name for the {@link Network#store()} method. */
        private const String SERIAL_FILE = "Network.ser";
        /** Default checkpoint Network file name for the {@link CheckPointer#checkPoint(rx.Observer)} method. */
        private const String CHECKPOINT_FILE = "Network_Checkpoint_";

        public static readonly FileMode[] PRODUCTION_OPTIONS = new FileMode[]
        {
            FileMode.Create,
            FileMode.CreateNew
        };

        public static readonly FileMode[] CHECKPOINT_OPTIONS = new FileMode[]
        {
            FileMode.Create,
            FileMode.CreateNew,
        };

        private String fileName;
        private String fileDir;
        private List<Type> registry;
        private FileMode[] options = PRODUCTION_OPTIONS;
        private FileMode[] checkPointOptions = CHECKPOINT_OPTIONS;

        private String checkPointFileName = CHECKPOINT_FILE;

        private String checkPointFormatString = CHECKPOINT_FORMAT_STRING;

        /** Specifies that as a new CheckPoint file is written, the old one is deleted */
        private bool oneCheckPointOnly;

        /**
     * Constructs a new {@code SerialConfig} which uses the default serialization
     * file name
     */
        public SerialConfig() : this(null)
        {

        }

        /**
         * Constructs a new {@code SerialConfig} which will use the specified file name
         * 
         * @param fileName  the file name to use
         */
        public SerialConfig(string fileName)
            : this(fileName, (String)null)
        {

        }

        /**
     * Constructs a new {@code SerialConfig} which will use the specific file name
     * and file directory.
     * 
     * @param fileName      the file name to use
     * @param fileDir       the file directory to use
     */
        public SerialConfig(String fileName, String fileDir)
            : this(fileName, fileDir, null)
        {

        }

        /**
         * Constructs a new {@code SerialConfig} which will use the specified file name
         * 
         * Pre-registering the "known" classes which will be serialized is highly 
         * recommended. These may be specified by the list named "registeredTypes".
         * 
         * Because the ID assigned is affected by the IDs registered before it, the order 
         * classes are registered is important when using this method. The order must be the 
         * same at deserialization as it was for serialization.
         * 
         * @param fileName          the file name to use
         * @param fileDir           the directory to use.
         * @param registeredTypes   list of classes indicating expected types to serialize
         */
        public SerialConfig(String fileName, String fileDir, List<Type> registeredTypes)
            : this(fileName, fileDir, registeredTypes, PRODUCTION_OPTIONS)
        {

        }

        /**
         * Constructs a new {@code SerialConfig} which will use the specified file name
         * 
         * Pre-registering the "known" classes which will be serialized is highly 
         * recommended. These may be specified by the list named "registeredTypes".
         * 
         * Because the ID assigned is affected by the IDs registered before it, the order 
         * classes are registered is important when using this method. The order must be the 
         * same at deserialization as it was for serialization.
         * 
         * The {@link OpenOption}s argument specifies how the file should be opened (either 
         * create new, truncate, append, read, write etc). see ({@link StandardOpenOption})
         * 
         * @param fileName          the file name to use
         * @param registeredTypes   list of classes indicating expected types to serialize 
         * @param openOptions       A list of options to use for how to work with the serialization 
         *                          file.
         */
        public SerialConfig(String fileName, String fileDir, List<Type> registeredTypes, params FileMode[] openOptions)
        {

            this.fileName = fileName == null ? SERIAL_FILE : fileName;
            this.fileDir = fileDir == null ? SERIAL_DIR : fileDir;

            if (registeredTypes == null)
            {
                LOGGER.Debug("List of registered serialize class types was null. Using the default...");
            }
            this.registry = registeredTypes == null ? new List<Type>(DEFAULT_REGISTERED_TYPES) : registeredTypes;

            if (openOptions == null)
            {
                LOGGER.Debug("The OpenOptions were null. Using the default...");
            }

            this.options = openOptions == null ? PRODUCTION_OPTIONS : openOptions;
        }

        /**
     * The file name used to store the serialized object.
     * @return the file name to use
     */
        public String GetFileName()
        {
            return fileName;
        }

        /**
         * Returns the directory within which files are saved.
         * @return  the save directory
         */
        public String GetFileDir()
        {
            return fileDir;
        }

        /**
         * Return the absolute path to the serialize directory.
         * @return  the absolute path to the serialize directory.
         */
        public String GetAbsoluteSerialDir()
        {
            return Environment.CurrentDirectory + "\\" + fileDir;
        }

        /**
         * Returns the name portion of the checkpoint file name. The check pointed
         * file will have a name consisting of two parts, the "name" and the "timestamp".
         * To set the  
         * @return
         */
        public String GetCheckPointFileName()
        {
            return checkPointFileName;
        }

        /**
         * Sets the name portion of the checkpointed file's name.
         * @param name  the name portion of the checkpointed file's name.
         */
        public void SetCheckPointFileName(String name)
        {
            this.checkPointFileName = name;
        }

        /**
         * Sets the format string for the date portion of the checkpoint file name.
         * @return  the format string for the date portion of the checkpoint file name.
         */
        public String GetCheckPointFormatString()
        {
            return this.checkPointFormatString;
        }

        /**
         * Sets the format string for the date portion of the checkpoint file name.
         * @param formatString  the format to use on the current timestamp.
         */
        public void SetCheckPointTimeFormatString(String formatString)
        {
            if (formatString == null || string.IsNullOrWhiteSpace(formatString))
            {
                throw new NullReferenceException("Cannot use a null or empty format string.");
            }

            checkPointFormatString = formatString;
        }

        /**
         * @return the registry
         */
        public List<Type> GetRegistry()
        {
            return registry;
        }

        /**
         * Returns a list of the configured serialization file treatment
         * options.
         * @return  a list of OpenOptions
         */
        public FileMode[] GetOpenOptions()
        {
            return options;
        }

        /**
         * Returns the NIO File options used to determine how to create files,
         * overwrite files etc.
         * @return  the NIO File options
         * @see StandardOpenOption
         */
        public FileMode[] GetCheckPointOpenOptions()
        {
            return checkPointOptions;
        }

        /**
         * Sets the NIO File options used to determine how to create files,
         * overwrite files etc.
         * 
         * @param options   the NIO File options
         * @see StandardOpenOption
         */
        public void setCheckPointOpenOptions(FileMode[] options)
        {
            this.checkPointOptions = options;
        }

        /**
         * Specifies that as a new CheckPoint file is written, the old one is deleted
         * @param b     true to maintain at most one file, false to keep writing new files (default).
         */
        public void setOneCheckPointOnly(bool b)
        {
            this.oneCheckPointOnly = b;
        }

        /**
         * Returns a flag indicating whether only one check point file will exist at a time, or not.
         * @return  the flag specifying this condition.
         */
        public bool isOneCheckPointOnly()
        {
            return oneCheckPointOnly;
        }
    }
}