using System;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Research.Taurus.HtmEngine.Runtime;
using HTM.Net.Util;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository;

/// <summary>
/// Table Metric
/// </summary>
public class Metric
{
    public string Uid { get; set; }
    public string DataSource { get; set; }
    public string Name { get; set; }
    public string Server { get; set; }
    public string Description { get; set; }
    public string Location { get; set; }
    public string Parameters { get; set; }
    public MetricStatus Status { get; set; }
    public string Message { get; set; }
    public DateTime? LastTimeStamp { get; set; }
    public int? PollInterval { get; set; }
    public string TagName { get; set; }
    public string ModelParams { get; set; }
    public long LastRowId { get; set; }

    // Ignored in db
    public string DisplayName { get; set; }

    public Metric Clone(string[] allowedKeys)
    {
        BeanUtil bu = BeanUtil.GetInstance();
        Metric m = new Metric();

        var props = GetType().GetProperties();

        foreach (string key in allowedKeys)
        {
            var prop = props.SingleOrDefault(p => p.Name.Equals(key, StringComparison.InvariantCultureIgnoreCase));
            if (prop == null)
            {
                Debug.WriteLine("!!!! prop not found > " + key);
            }
            bu.SetSimpleProperty(m, key, prop.GetValue(this));
        }

        return m;
    }
}