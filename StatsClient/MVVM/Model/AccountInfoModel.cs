namespace StatsClient.MVVM.Model;

public class AccountInfoModel
{
    public string? FriendlyName { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Website { get; set; }
    public AccountCredential? Credentials { get; set; }
    public string? ApplicationName { get; set; }
    public string? ApplicationPath { get; set; }
    public string? Color { get; set; } = "#46596F";
}

public class AccountCredential
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}