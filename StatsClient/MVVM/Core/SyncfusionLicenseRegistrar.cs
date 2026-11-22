namespace StatsClient.MVVM.Core;

public static class SyncfusionLicenseRegistrar
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
            "Ngo9BigBOggjHTQxAR8/V1JGaF5cXGpCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH1ceXRcQ2heVkZ+XkpWYEs=");
        _registered = true;
    }
}
