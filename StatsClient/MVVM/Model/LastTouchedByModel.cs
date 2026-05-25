namespace StatsClient.MVVM.Model
{
    public class LastTouchedByModel(string computerName, string dateTimeStr)
    {
        public string ComputerName { get; set; } = computerName;
        public string DateTimeStr { get; set; } = dateTimeStr;
    }
}
