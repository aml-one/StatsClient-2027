namespace StatsClient.MVVM.Model;

public sealed class PrescriptionExtractPreviewRow
{
    public PrescriptionExtractPreviewRow(string field, string value)
    {
        Field = field;
        Value = value;
    }

    public string Field { get; }

    public string Value { get; }
}
