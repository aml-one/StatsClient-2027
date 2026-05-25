namespace StatsClient.MVVM.Model;

public class PayPeriodStatisticsModel
{
    public string PayPeriod { get; set; } = "";
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public int UnpaidCases { get; set; }
    public int UnpaidUnits { get; set; }
    public int DesignedCases { get; set; }
    public int PaidUnits { get; set; }
    
    public string PeriodDisplay => $"{PeriodStartDate:MMM d} - {PeriodEndDate:MMM d}";
    public string UnpaidCasesDisplay => $"{UnpaidCases} cases";
    public string UnpaidUnitsDisplay => $"{UnpaidUnits} units";
    public string DesignedCasesDisplay => $"{DesignedCases} cases";
    public string PaidUnitsDisplay => $"{PaidUnits} units";
}
