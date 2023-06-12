using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HTM.Net.Research.NAB;

public interface IDataFile
{
    string SrcPath { get; set; }
    //string FileName { get; }
    DataFrame Data { get; set; }
    void Write(string newPath = null);
    void ModifyData(string columnName, List<object> data = null, bool write = false);
    List<DateTime> GetTimestampRange(DateTime t1, DateTime t2);
    IDataFile Clone();
    void UpdateColumnType(string timestamp, Type type, Func<string, object> conversion);
}

public class DataFile : IDataFile
{
    public string SrcPath { get; set; }
    public string FileName { get; }
    public DataFrame Data { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="srcPath">Filename of datafile to read.</param>
    public DataFile(string srcPath)
    {
        SrcPath = srcPath;
        FileName = Path.GetFileName(srcPath);
        Data = DataFrame.LoadCsv(srcPath, header: true);
    }

    /// <summary>
    /// Write datafile to srcPath or newPath if given.
    /// </summary>
    /// <param name="newPath">Path to write datafile to. If Path is not given, write to source Path</param>
    public void Write(string newPath = null)
    {
        var path = newPath ?? SrcPath;
        Data.ToCsv(path, true);
    }

    /// <summary>
    /// Add columnName to datafile if data is given otherwise remove columnName.
    /// </summary>
    /// <param name="columnName"> Name of the column in the datafile to either add or remove.</param>
    /// <param name="data">(pandas.Series)   Column data to be added to datafile. Data length should be as long as the length of other columns.</param>
    /// <param name="write">Flag to choose whether to write modifications to source Path.</param>
    public void ModifyData(string columnName, List<object> data = null, bool write = false)
    {
        if (data != null && data is List<object>)
        {
            Data[columnName] = data;
        }
        else
        {
            if (Data != null && Data.ColumnNames.Contains(columnName))
            {
                Data.Remove(columnName);
            }
        }

        if (write)
        {
            Write();
        }
    }

    public List<DateTime> GetTimestampRange(DateTime t1, DateTime t2)
    {
        List<DateTime> ans = new List<DateTime>();
        var tmp = Data.Where("timestamp", row => (DateTime)row >= t1);
        tmp = tmp.Where("timestamp", row => (DateTime)row <= t2);
        foreach (var row in tmp.IterateRows())
        {
            ans.Add((DateTime)row["timestamp"]);
        }
        return ans;
    }

    public override string ToString()
    {
        string ans = "";
        ans += $"Path:                {this.SrcPath}\n";
        ans += $"file name:           {this.FileName}\n";
        ans += $"data size:           {this.Data.GetShape0()}\n";
        ans += $"sample line:         {string.Join(", ", this.Data.IterateRows()?.FirstOrDefault()?.Keys.ToList() ?? new List<string>())}\n";
        return ans;
    }

    public IDataFile Clone()
    {
        DataFile file = new DataFile(this.SrcPath);

        return file;
    }

    public void UpdateColumnType(string timestamp, Type type, Func<string, object> conversion)
    {
        Data[timestamp] = Data[timestamp].Select(x => conversion(x as string)).ToList();
    }
}

public class VirtualDataFile : IDataFile
{
    public string SrcPath { get; set; }
    public DataFrame Data { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name">name of the dataset</param>
    /// <param name="data">Column based dataset</param>
    public VirtualDataFile(string name, Dictionary<string, IList> data)
    {
        SrcPath = name;
        Data = new DataFrame(data);
    }

    /// <summary>
    /// Write datafile to srcPath or newPath if given.
    /// </summary>
    /// <param name="newPath">Path to write datafile to. If Path is not given, write to source Path</param>
    public void Write(string newPath = null)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Add columnName to datafile if data is given otherwise remove columnName.
    /// </summary>
    /// <param name="columnName"> Name of the column in the datafile to either add or remove.</param>
    /// <param name="data">(pandas.Series)   Column data to be added to datafile. Data length should be as long as the length of other columns.</param>
    /// <param name="write">Flag to choose whether to write modifications to source Path.</param>
    public void ModifyData(string columnName, List<object> data = null, bool write = false)
    {
        if (data != null && data is List<object>)
        {
            Data[columnName] = data;
        }
        else
        {
            if (Data != null && Data.ColumnNames.Contains(columnName))
            {
                Data.Remove(columnName);
            }
        }

        if (write)
        {
            Write();
        }
    }

    public List<DateTime> GetTimestampRange(DateTime t1, DateTime t2)
    {
        List<DateTime> ans = new List<DateTime>();
        var tmp = Data.Where("timestamp", row => (DateTime)row >= t1);
        tmp = tmp.Where("timestamp", row => (DateTime)row <= t2);
        foreach (var row in tmp.IterateRows())
        {
            ans.Add((DateTime)row["timestamp"]);
        }
        return ans;
    }

    public override string ToString()
    {
        string ans = "";
        ans += $"data size:           {this.Data.GetShape0()}\n";
        ans += $"sample line:         {string.Join(", ", this.Data.IterateRows()?.FirstOrDefault()?.Keys.ToList() ?? new List<string>())}\n";
        return ans;
    }

    public IDataFile Clone()
    {
        VirtualDataFile file = new VirtualDataFile(SrcPath, Data.IterateColumns().ToDictionary(k => k.Key, v => v.Value));

        return file;
    }

    public void UpdateColumnType(string timestamp, Type type, Func<string, object> conversion)
    {
        Data[timestamp] = Data[timestamp].Select(x => conversion(x as string)).ToList();
    }
}

public interface ICorpus
{
    //string SrcRoot { get; }
    //int NumDataFiles { get; }
    Dictionary<string, IDataFile> DataFiles { get; }
    void AddColumn(string columnName, Dictionary<string, List<object>> data, bool write = false);
    void RemoveColumn(string columnName, bool write = false);
    ICorpus Copy(string newRoot);
    //void AddDataSet(string relativePath, IDataFile dataSet);
}

/// <summary>
/// Class for storing and manipulating a corpus of data where each datafile is
/// stored as a DataFile object.
/// </summary>
public class Corpus : ICorpus
{
    public string SrcRoot { get; }
    public int NumDataFiles { get; private set; }
    public Dictionary<string, IDataFile> DataFiles { get; }

    public Corpus(string srcRoot)
    {
        SrcRoot = srcRoot;
        DataFiles = GetDataFiles();
        NumDataFiles = DataFiles.Count;
    }

    /// <summary>
    /// Collect all CSV data files from srcRoot directory.
    /// </summary>
    /// <returns></returns>
    private Dictionary<string, IDataFile> GetDataFiles()
    {
        var filePaths = Directory.GetFiles(SrcRoot, "*.csv", SearchOption.AllDirectories);
        var dataSets = filePaths
            .Where(f => f.EndsWith(".csv"))
            .Select(path => (IDataFile)new DataFile(path))
            .ToList();

        return dataSets.ToDictionary(d => Path.GetRelativePath(SrcRoot, d.SrcPath));
    }

    public void AddColumn(string columnName, Dictionary<string, List<object>> data, bool write = false)
    {
        foreach (var relativePath in DataFiles.Keys.ToList())
        {
            DataFiles[relativePath].ModifyData(columnName, data[relativePath], write);
        }
    }

    public void RemoveColumn(string columnName, bool write = false)
    {
        foreach (var relativePath in DataFiles.Keys.ToList())
        {
            DataFiles[relativePath].ModifyData(columnName, null, write);
        }
    }

    public ICorpus Copy(string newRoot)
    {
        if (!newRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            newRoot += Path.DirectorySeparatorChar;
        }
        if (Directory.Exists(newRoot))
        {
            Console.WriteLine($"> Directory already exists: {newRoot}");
            return null;
        }
        else
        {
            Directory.CreateDirectory(newRoot);
        }

        Corpus newCorpus = new Corpus(newRoot);
        foreach (string relativePath in DataFiles.Keys)
        {
            newCorpus.AddDataSet(relativePath, DataFiles[relativePath]);
        }
        return newCorpus;
    }

    public void AddDataSet(string relativePath, IDataFile dataSet)
    {
        DataFiles[relativePath] = dataSet.Clone();
        string newPath = Path.Combine(SrcRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(newPath));
        DataFiles[relativePath].SrcPath = newPath;
        DataFiles[relativePath].Write();
        NumDataFiles = DataFiles.Count;
    }
}

public class VirtualCorpus : ICorpus
{
    public string Name { get; }

    public int NumDataFiles { get; private set; }

    public Dictionary<string, IDataFile> DataFiles { get; }

    public VirtualCorpus(string name, params VirtualDataFile[] dataFiles)
    {
        Name = name;
        DataFiles = dataFiles.ToDictionary(d => d.SrcPath, d => (IDataFile)d);
    }

    public void AddColumn(string columnName, Dictionary<string, List<object>> data, bool write = false)
    {
        foreach (var relativePath in DataFiles.Keys.ToList())
        {
            DataFiles[relativePath].ModifyData(columnName, data[relativePath], write);
        }
    }

    public void RemoveColumn(string columnName, bool write = false)
    {
        foreach (var relativePath in DataFiles.Keys.ToList())
        {
            DataFiles[relativePath].ModifyData(columnName, null, write);
        }
    }

    public ICorpus Copy(string newName)
    {
        VirtualCorpus newCorpus = new VirtualCorpus(newName);
        foreach (string relativePath in DataFiles.Keys)
        {
            newCorpus.AddDataSet(relativePath, DataFiles[relativePath]);
        }
        return newCorpus;
    }

    public void AddDataSet(string name, IDataFile dataSet)
    {
        DataFiles[name] = dataSet.Clone();
        NumDataFiles = DataFiles.Count;
    }
}