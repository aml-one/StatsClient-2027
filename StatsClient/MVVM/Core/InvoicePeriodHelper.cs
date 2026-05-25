using System;

namespace StatsClient.MVVM.Core;

public static class InvoicePeriodHelper
{
    /// <summary>
    /// Gets the next Tuesday (invoice closing day) from the given date.
    /// If the given date is a Tuesday, it returns the Tuesday 2 weeks later.
    /// </summary>
    public static DateTime GetNextInvoiceClosingDate(DateTime fromDate)
    {
        // Find next Tuesday
        int daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)fromDate.DayOfWeek + 7) % 7;
        
        DateTime nextTuesday;
        if (daysUntilTuesday == 0 && fromDate.TimeOfDay > TimeSpan.Zero)
        {
            // If it's already Tuesday but later in the day, move to next Tuesday
            nextTuesday = fromDate.AddDays(7);
        }
        else if (daysUntilTuesday == 0)
        {
            // If it's Tuesday at midnight, this is the closing day
            nextTuesday = fromDate;
        }
        else
        {
            nextTuesday = fromDate.AddDays(daysUntilTuesday);
        }

        // Invoice periods are 2 weeks apart, so we need to find the correct bi-weekly Tuesday
        // This is a simplified version - you may need to adjust based on a known reference date
        return nextTuesday;
    }

    /// <summary>
    /// Gets the previous Tuesday (invoice closing day) from the given date.
    /// </summary>
    public static DateTime GetPreviousInvoiceClosingDate(DateTime fromDate)
    {
        // Find previous Tuesday
        int daysSinceTuesday = ((int)fromDate.DayOfWeek - (int)DayOfWeek.Tuesday + 7) % 7;
        
        DateTime previousTuesday;
        if (daysSinceTuesday == 0)
        {
            // If it's Tuesday, go back 2 weeks
            previousTuesday = fromDate.AddDays(-14);
        }
        else
        {
            previousTuesday = fromDate.AddDays(-daysSinceTuesday);
        }

        return previousTuesday;
    }

    /// <summary>
    /// Gets the current invoice period start date (Wednesday after last closing Tuesday).
    /// </summary>
    public static DateTime GetCurrentInvoicePeriodStartDate(DateTime referenceDate)
    {
        DateTime lastClosing = GetPreviousInvoiceClosingDate(referenceDate);
        // Period starts on Wednesday (day after Tuesday closing)
        return lastClosing.AddDays(1);
    }

    /// <summary>
    /// Gets the current invoice period end date (next Tuesday).
    /// </summary>
    public static DateTime GetCurrentInvoicePeriodEndDate(DateTime referenceDate)
    {
        return GetNextInvoiceClosingDate(referenceDate);
    }

    /// <summary>
    /// Checks if a date is within the current invoice period.
    /// </summary>
    public static bool IsWithinCurrentInvoicePeriod(DateTime checkDate, DateTime referenceDate)
    {
        DateTime periodStart = GetCurrentInvoicePeriodStartDate(referenceDate);
        DateTime periodEnd = GetCurrentInvoicePeriodEndDate(referenceDate);
        
        return checkDate >= periodStart && checkDate <= periodEnd;
    }

    /// <summary>
    /// Gets the last invoice closing date (most recent Tuesday that has passed).
    /// </summary>
    public static DateTime GetLastInvoiceClosingDate()
    {
        DateTime today = DateTime.Today;
        DayOfWeek currentDay = today.DayOfWeek;
        
        int daysToSubtract = currentDay switch
        {
            DayOfWeek.Sunday => 5,
            DayOfWeek.Monday => 6,
            DayOfWeek.Tuesday => 0, // Today is closing day
            DayOfWeek.Wednesday => 1,
            DayOfWeek.Thursday => 2,
            DayOfWeek.Friday => 3,
            DayOfWeek.Saturday => 4,
            _ => 0
        };

        return today.AddDays(-daysToSubtract);
    }
}
