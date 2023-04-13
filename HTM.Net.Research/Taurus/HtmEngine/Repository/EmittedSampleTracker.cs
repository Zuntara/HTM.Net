using System;
using System.ComponentModel.DataAnnotations;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository;

/// <summary>
/// Table
/// </summary>
public class EmittedSampleTracker
{
    [MaxLength(50)]
    public string Key { get; set; }
    public DateTime SampleTs { get; set; }
}