namespace StatsClient.MVVM.Model;

public class DesignerPaymentSummary
{
    public string? DesignerName { get; set; }
    public int PaymentIssues { get; set; } = 0;
    public int UnPaidCases { get; set; } = 0;
}
