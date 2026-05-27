using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using StatsClient.MVVM.Core;

namespace StatsClient.Benchmarks;

[CPUUsageDiagnoser]
[MemoryDiagnoser]
public class DcmFinderBenchmark
{
    private string _orderFolder = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _orderFolder = Path.Combine(Path.GetTempPath(), "StatsClientBench", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_orderFolder);

        string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string xmlSource = Path.Combine(solutionRoot, "StatsClient", "Temp", "7345-9-B1-HIGGINS-MCLEAN-LONG-306311762-ITERO.xml");
        string xmlTarget = Path.Combine(_orderFolder, $"{Path.GetFileNameWithoutExtension(xmlSource)}.xml");

        File.Copy(xmlSource, xmlTarget, overwrite: true);
        Directory.CreateDirectory(Path.Combine(_orderFolder, "Scans", "Upper"));
        Directory.CreateDirectory(Path.Combine(_orderFolder, "Scans", "Lower"));
        Directory.CreateDirectory(Path.Combine(_orderFolder, "Scans", "Misc"));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_orderFolder))
        {
            Directory.Delete(_orderFolder, recursive: true);
        }
    }

    [Benchmark]
    public int FindFromOrderFolder_CountFiles()
    {
        return DCMFinder.FindFromOrderFolder(_orderFolder).AllFiles.Count;
    }
}
