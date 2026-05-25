using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsClient.MVVM.Model;

public class AvailablePanCountModel
{
    public int Count { get; set; }
    public string? ComputerName { get; set; }
    public string? FriendlyName { get; set; }
    public double NumberFontSize { get; set; }
    public double TitleFontSize { get; set; }
    public double NamesFontSize { get; set; }
}
