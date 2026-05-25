using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsClient.MVVM.Model;

public class CommentRulesModel
{
    public string? RuleName { get; set; } = "";
    public string? Customer { get; set; }
    public string? ItemsContains { get; set; }
    public string? ExtraText { get; set; }
}
