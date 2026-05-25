using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsClient.MVVM.Model;

public class DuplicatePanNumberOrdersModel
{
    public string? OrderID_1 { get; set; }
    public string? Patient_LastName_1 { get; set; }
    public string? Patient_FirstName_1 { get; set; }
    public string? Created_1 { get; set; }
    public string? Customer_1 { get; set; }

    public string? OrderID_2 { get; set; }
    public string? Patient_LastName_2 { get; set; }
    public string? Patient_FirstName_2 { get; set; }
    public string? Created_2 { get; set; }
    public string? Customer_2 { get; set; }

    public string? PanNr { get; set; }
}
