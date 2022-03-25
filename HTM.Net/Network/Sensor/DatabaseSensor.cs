using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;

using HTM.Net.Encoders;
using HTM.Net.Util;

using Microsoft.Data.SqlClient;

using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Network.Sensor;

[Serializable]
public class DatabaseSensor : Sensor<DbDataReader>
{
    [NonSerialized]
    protected IMetaStream stream;

    protected readonly SensorParams @params;

    /**
         * Protected constructor. Instances of this class should be obtained 
         * through the {@link #create(SensorParams)} factory method.
         * 
         * @param params
         */
    protected DatabaseSensor(SensorParams @params)
    {
        this.@params = @params;

        if (!@params.HasKey("CONN_STRING"))
        {
            throw new ArgumentException("Passed improperly formed Tuple: no key for \"CONN_STRING\"");
        }
        if (!@params.HasKey("QUERY"))
        {
            throw new ArgumentException("Passed improperly formed Tuple: no key for \"QUERY\"");
        }

        string connString = (string)@params.Get("CONN_STRING");
        string query = (string)@params.Get("QUERY");

        SqlConnection connection = new SqlConnection(connString);
        connection.Open(SqlConnectionOverrides.OpenWithoutRetry);

        SqlCommand command = new SqlCommand(query, connection);
        var reader = command.ExecuteReader();

        if (!reader.HasRows)
        {
            throw new ArgumentException("Nothing to read");
        }

        try
        {
            stream = new DatabaseStream(reader);
        }
        catch (IOException e)
        {
            Console.WriteLine(e);
        }
    }

    /**
         * Factory method to allow creation through the {@link SensorFactory} in
         * the {@link Sensor#create(SensorFactory, SensorParams)} method of the 
         * parent {@link Sensor} class. This indirection allows the decoration of 
         * the returned {@code Sensor} type by wrapping it in an {@link HTMSensor}
         * (which is the current implementation but could be any wrapper).
         * 
         * @param p     the {@link SensorParams} which describe connection or source
         *              data details.
         * @return      the Sensor.
         */
    public static Sensor<DbDataReader> Create(SensorParams p)
    {
        Sensor<DbDataReader> fs = new DatabaseSensor(p);
        return fs;
    }

    public override SensorParams GetSensorParams()
    {
        return @params;
    }

    /**
         * Returns the configured {@link MetaStream} if this is of
         * Type Stream, otherwise it throws an {@link UnsupportedOperationException}
         * 
         * @return  the MetaStream
         */
    public override IMetaStream GetInputStream()
    {
        return stream;
    }

    public override MultiEncoder GetEncoder()
    {
        throw new NotImplementedException();
    }

    public override bool EndOfStream()
    {
        throw new NotImplementedException();
    }

    /**
         * Returns the values specifying meta information about the 
         * underlying stream.
         */
    public override IValueList GetMetaInfo()
    {
        return stream.GetMeta();
    }

    public override void InitEncoder(Parameters p)
    {
        throw new NotImplementedException();
    }
}

public class DatabaseStream : IMetaStream<string[]>
{
    private readonly SqlDataReader _reader;
    internal IStream<string[]> _contentStream;

    public DatabaseStream(SqlDataReader reader)
    {
        _reader = reader;

        _contentStream = new Stream<string[]>(CreateObservableFromReader(reader));
    }

    private IObservable<string[]> CreateObservableFromReader(SqlDataReader reader)
    {
        ReplaySubject<string[]> subject = new ReplaySubject<string[]>();

        int fields = reader.FieldCount;
        while (reader.Read())
        {
            string[] values = new string[fields];
            for (int i = 0; i < fields; i++)
            {
                values[i] = reader.GetValue(i).ToString();
            }

            subject.OnNext(values);
        }

        subject.OnCompleted();

        return subject;
    }

    public IValueList GetMeta()
    {
        var schema = _reader.GetColumnSchema();

        var cols = schema.Select(c => c).ToArray();
        return new DbHeaderValueList(cols);
    }

    public bool IsTerminal()
    {
        return !_reader.HasRows;
    }

    public bool IsParallel()
    {
        return false;
    }

    public IStream<int[]> Map(Func<string[], int[]> mapFunc)
    {
        return _contentStream.Map(mapFunc);
    }

    public void ForEach(Action<object> action)
    {
        throw new NotImplementedException();
    }

    public long Count()
    {
        return _contentStream.Count();
    }

    public class DbHeaderValueList : IValueList
    {
        /// <summary>
        /// Container for the field values
        /// </summary>
        private readonly Tuple[] _headerValues;

        private readonly DbColumn[] _columns;

        public DbHeaderValueList(DbColumn[] columns)
        {
            _columns = columns;
            _headerValues = new Tuple[3];
            _headerValues[0] = new Tuple(columns.Select(c => c.ColumnName));
            _headerValues[1] = new Tuple(columns.Select(c => c.DataTypeName));
            _headerValues[2] = new Tuple(new string[0]);
        }

        private FieldMetaType ConvertToFieldType<TResult>(string? type)
        {
            throw new NotImplementedException();
        }

        public Tuple[] GetHeaderValues()
        {
            return _headerValues;
        }

        public Tuple GetRow(int row)
        {
            if (row >= _headerValues.Length)
            {
                return null;
            }
            return _headerValues[row];
        }

        public int Size()
        {
            return _headerValues.Length;
        }

        public bool IsLearn()
        {
            throw new NotImplementedException();
        }

        public bool IsReset()
        {
            throw new NotImplementedException();
        }

        public List<FieldMetaType> GetFieldTypes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            _columns.ToList().ForEach(l => sb.Append(l).Append("\n"));
            return sb.ToString();
        }
    }
}

