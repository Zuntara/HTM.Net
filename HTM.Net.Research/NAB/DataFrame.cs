using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HTM.Net.Research.NAB;

public class DataFrame : IEquatable<DataFrame>
{
    /// <summary>
    /// Collection of present column names.
    /// </summary>
    public List<string> ColumnNames { get; set; }

    /// <summary>
    /// Collection of column data per column name. (actual row data)
    /// </summary>
    public Dictionary<string, List<(int origIndex, object value)>> ColumnData { get; set; }

    public DataFrame()
    {
        ColumnNames = new List<string>();
        ColumnData = new Dictionary<string, List<(int origIndex, object value)>>();
    }

    public DataFrame(Dictionary<string, IList> columnsWithData)
    {
        ColumnNames = new List<string>();
        ColumnData = new Dictionary<string, List<(int origIndex, object value)>>();

        foreach (KeyValuePair<string, IList> pair in columnsWithData)
        {
            int index = 0;
            ColumnNames.Add(pair.Key);
            ColumnData.Add(pair.Key, new List<(int origIndex, object value)>());

            foreach (var item in pair.Value)
            {
                ColumnData[pair.Key].Add((index++, item));
            }
        }
    }

    public int GetShape0()
    {
        return ColumnData.ElementAt(0).Value.Count;
    }

    public List<object> this[string column]
    {
        get
        {
            return ColumnData[column].Select(x => x.value).ToList();
        }
        set
        {
            Console.WriteLine("Setting column data");
            ColumnData[column] = value.Select((x, i) => (i, x)).ToList();
            if(!ColumnNames.Contains(column))
            {
                ColumnNames.Add(column);
            }
        }
    }

    public object this[string column, int rowIndex]
    {
        get
        {
            return ColumnData[column][rowIndex].value;
        }
        set
        {
            var list = ColumnData[column];
            list[rowIndex] = (rowIndex, value);
        }
    }

    public void Add(string column, List<object> value)
    {
        ColumnNames.Add(column);
        ColumnData.Add(column, new List<(int origIndex, object value)>());
        for (int i = 0; i < value.Count; i++)
        {
            ColumnData[column].Add((i, value[i]));
        }
    }

    public void Add(string column, int value)
    {
        var list = Enumerable.Range(0, GetShape0()).Select(i => (object)value).ToList();
        this.Add(column, list);
    }

    public void Remove(string column)
    {
        if (ColumnNames.Contains(column))
        {
            ColumnNames.Remove(column);
            ColumnData.Remove(column);
        }
    }

    public DataFrame Where(string column, Func<object, bool> predicate)
    {
        List<(int origIndex, object value)> arr = ColumnData[column];
        var indices = arr
            .Where(x => predicate(x.value))
            .Select((x, i) => (x.origIndex, i))
            .ToArray();

        var df = new DataFrame();
        foreach (KeyValuePair<string, List<(int origIndex, object value)>> pair in this.ColumnData)
        {
            List<(int origIndex, object value)> row = new List<(int origIndex, object value)>();
            foreach (var i in indices)
            {
                row.Add((i.origIndex, pair.Value[i.origIndex].value));
            }

            df.SetColumnData(pair.Key, row);
        }

        return df;
    }

    public List<int> GetColumnIndices(string column)
    {
        return ColumnData[column].Select(x => x.origIndex).ToList();
    }

    public void SetColumnData(string columnName, List<(int origIndex, object value)> data)
    {
        if (!ColumnNames.Contains(columnName))
        {
            ColumnNames.Add(columnName);
        }

        if (!ColumnData.ContainsKey(columnName))
        {
            ColumnData.Add(columnName, data);
        }
        else
        {
            ColumnData[columnName] = data;
        }
    }

    public void Populate(List<List<object>> rows, List<string> columns)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            var data = rows.Select(x => x[i]).ToList();
            this.Add(columns[i], data);
        }
    }

    public IEnumerable<Dictionary<string, object>> IterateRows()
    {
        var length = GetShape0();
        for (int i = 0; i < length; i++)
        {
            yield return GetRow(i);
        }
    }

    public IEnumerable<KeyValuePair<string, IList>> IterateColumns()
    {
        for (int i = 0; i < ColumnNames.Count; i++)
        {
            yield return GetColumnData(ColumnNames[i]);
        }
    }

    private KeyValuePair<string, IList> GetColumnData(string columnName)
    {
        return new KeyValuePair<string, IList>(columnName, ColumnData[columnName].Select(x => x.value).ToList());
    }

    private Dictionary<string, object> GetRow(int i)
    {
        // Return a row of the dataframe as a dictionary, keys are column names, values are the row values.
        var row = new Dictionary<string, object>();
        foreach (var column in ColumnNames)
        {
            row.Add(column, ColumnData[column][i].value);
        }
        return row;
    }

    public void ToCsv(string path, bool includeHeaders)
    {
        using StreamWriter sw = new StreamWriter(File.Create(path), Encoding.UTF8);
        if (includeHeaders)
        {
            sw.WriteLine(string.Join(",", ColumnNames));
        }

        foreach (var row in IterateRows())
        {
            sw.WriteLine(string.Join(",", row.Select(x =>
            {
                if (x.Value is DateTime dt)
                {
                    return dt.ToString("s");
                }

                return x.Value;
            })));
        }

        sw.Flush();
    }

    public static DataFrame LoadCsv(string path, bool header = true)
    {
        using StreamReader sr = new StreamReader(path, Encoding.UTF8);
        var rows = new List<List<object>>();
        var columns = new List<string>();
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            var values = line.Split(',');
            if (header)
            {
                columns = values.ToList();
                header = false;
            }
            else
            {
                rows.Add(values.Select(x => (object)x).ToList());
            }
        }
        var df = new DataFrame();
        df.Populate(rows, columns);
        return df;
    }

    public override string ToString()
    {
        return string.Join(", ", ColumnNames);
    }

    public bool Equals(DataFrame other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return 
            ColumnNames.SequenceEqual(other.ColumnNames)
            && ColumnData.Keys.SequenceEqual(other.ColumnData.Keys)
            && ColumnData.Values.Count == other.ColumnData.Values.Count
            && ColumnData.Values.Zip(other.ColumnData.Values, (a, b) => a.SequenceEqual(b)).All(x => x);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((DataFrame)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ColumnNames, ColumnData);
    }

    public static bool operator ==(DataFrame left, DataFrame right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DataFrame left, DataFrame right)
    {
        return !Equals(left, right);
    }

    public DataFrame Take(int count)
    {
        var data = IterateColumns()
            .ToDictionary(k => k.Key, v => (IList)((IList<object>)v.Value).Take(count).ToList());
        
        DataFrame df = new DataFrame(data);
        return df;
    }
}

