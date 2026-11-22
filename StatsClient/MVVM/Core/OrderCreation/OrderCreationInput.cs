namespace StatsClient.MVVM.Core.OrderCreation;

public sealed class OrderCreationInput
{
    public string OrderID { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string PatientFirstName { get; set; } = string.Empty;
    public string PatientLastName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = "meIndicationRegular";
    public bool ModelSetupEnabled { get; set; }
    public string? OperatorName { get; set; }
    public string? ManufacturerName { get; set; }
    public string? OrderComments { get; set; }
    public string? ExtOrderID { get; set; }
    public List<int>? UnitNumbers { get; set; }
    public string? UnitNumbersCsv { get; set; }
    public List<OrderCreationRestorationInput> TeethSetup { get; set; } = [];
}

public sealed class OrderCreationRestorationInput
{
    public string CaseType { get; set; } = string.Empty;
    public List<int> Teeth { get; set; } = [];
    public List<int>? Pontics { get; set; }
    public string Material { get; set; } = string.Empty;
    public string? Shade { get; set; }
    public string? BridgeStyle { get; set; }
}
