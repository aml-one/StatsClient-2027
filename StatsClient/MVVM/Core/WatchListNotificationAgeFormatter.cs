namespace StatsClient.MVVM.Core;

public static class WatchListNotificationAgeFormatter
{
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    /// <summary>
    /// Formats elapsed time since <paramref name="appearedUtc"/> as mm:ss, h:mm:ss, or "N day(s) ago".
    /// </summary>
    public static string Format(DateTime appearedUtc)
    {
        var age = DateTime.UtcNow - appearedUtc;
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;

        if (age >= OneDay)
        {
            int days = Math.Max(1, (int)Math.Floor(age.TotalDays));
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        if (age >= OneHour)
        {
            int hours = (int)age.TotalHours;
            return $"{hours}:{age.Minutes:D2}:{age.Seconds:D2}";
        }

        int minutes = (int)age.TotalMinutes;
        return $"{minutes:D2}:{age.Seconds:D2}";
    }
}
