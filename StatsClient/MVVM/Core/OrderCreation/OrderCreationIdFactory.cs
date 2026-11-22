using System.Security.Cryptography;

namespace StatsClient.MVVM.Core.OrderCreation;

internal static class OrderCreationIdFactory
{
    public static string NewId(string prefix)
    {
        byte[] data = RandomNumberGenerator.GetBytes(16);
        return prefix + Convert.ToHexString(data);
    }

    public static string NewNumericOrderIdCandidate()
    {
        long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int salt = RandomNumberGenerator.GetInt32(100, 999);
        long value = (3012700000000L + (unixMs % 9999999000L)) * 1000L + salt;
        string s = value.ToString();
        return s.Length > 13 ? s[^13..] : s;
    }
}
