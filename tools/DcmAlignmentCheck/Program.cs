using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Media3D;
using DCMViewer.Services;

static Point3D Center(Rect3D b) =>
    new(b.X + b.SizeX * 0.5, b.Y + b.SizeY * 0.5, b.Z + b.SizeZ * 0.5);

static double CenterDistance(Rect3D a, Rect3D b)
{
    var ca = Center(a);
    var cb = Center(b);
    var dx = ca.X - cb.X;
    var dy = ca.Y - cb.Y;
    var dz = ca.Z - cb.Z;
    return Math.Sqrt(dx * dx + dy * dy + dz * dz);
}

static bool Overlaps(Rect3D a, Rect3D b)
{
    if (a.IsEmpty || b.IsEmpty)
    {
        return false;
    }

    return a.X < b.X + b.SizeX && a.X + a.SizeX > b.X &&
           a.Y < b.Y + b.SizeY && a.Y + a.SizeY > b.Y &&
           a.Z < b.Z + b.SizeZ && a.Z + a.SizeZ > b.Z;
}

static void ReportPair(string label, Rect3D a, Rect3D b)
{
    Console.WriteLine($"  {label}: overlap={Overlaps(a, b)}, center distance={CenterDistance(a, b):F1} mm");
}

var parser = new DcmParser();

if (args.Any(a => string.Equals(a, "--batch", StringComparison.OrdinalIgnoreCase)))
{
    var ordersRoot = args.FirstOrDefault(a =>
        a.Contains("3Shape Dental System Orders", StringComparison.OrdinalIgnoreCase) &&
        Directory.Exists(a))
        ?? @"\\3SH-SRV-23\3Shape Dental System Orders";

    Console.WriteLine($"=== Batch transform comparison ({ordersRoot}) ===\n");
    foreach (var dir in Directory.GetDirectories(ordersRoot).Where(d => Directory.Exists(Path.Combine(d, "CAD"))).Take(30))
    {
        var scansDir = Path.Combine(dir, "Scans");
        if (!Directory.Exists(scansDir))
        {
            continue;
        }

        var prep = Directory.GetFiles(scansDir, "*.dcm", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Contains("Preparation", StringComparison.OrdinalIgnoreCase) &&
                                  !p.Contains("Antagonist", StringComparison.OrdinalIgnoreCase) &&
                                  !p.Contains("AbutmentAlignment", StringComparison.OrdinalIgnoreCase));
        var cad = Directory.GetFiles(dir, "*.dcm", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Contains(@"\CAD\", StringComparison.OrdinalIgnoreCase));
        if (prep is null || cad is null)
        {
            continue;
        }

        var prepB = parser.ParseFile(prep, sceneTransformKind: SceneTransformKind.Scan).Bounds;
        var dNone = CenterDistance(prepB, parser.ParseFile(cad, sceneTransformKind: SceneTransformKind.None).Bounds);
        var dPlace = CenterDistance(prepB, parser.ParseFile(cad, sceneTransformKind: SceneTransformKind.Designed).Bounds);
        var dFull = CenterDistance(prepB, parser.ParseFile(cad, sceneTransformKind: SceneTransformKind.DesignedFull).Bounds);
        var best = Math.Min(dNone, Math.Min(dPlace, dFull));
        var winner = best == dNone ? "none" : best == dPlace ? "placement" : "full";
        Console.WriteLine($"{Path.GetFileName(dir)}: none={dNone:F0} place={dPlace:F0} full={dFull:F0} -> {winner}");
    }

    return;
}

var orderArg = args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));
var orderFolder = orderArg;

if (string.IsNullOrWhiteSpace(orderFolder))
{
    var docs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Docs"));
    Console.WriteLine("=== Docs scan check ===\n");
    var prep = Path.Combine(docs, "Raw Preparation scan.dcm");
    var ant = Path.Combine(docs, "Raw Antagonist scan.dcm");
    var crown = Path.Combine(docs, "abutment5.dcm");

    foreach (var (label, path, kind) in new[]
    {
        ("Prep scan", prep, SceneTransformKind.Scan),
        ("Ant scan", ant, SceneTransformKind.Scan),
        ("Abutment5 designed", crown, SceneTransformKind.Designed),
    })
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"MISSING {label}");
            continue;
        }

        var mesh = parser.ParseFile(path, sceneTransformKind: kind);
        var c = Center(mesh.Bounds);
        Console.WriteLine($"{label}: center ({c.X:F1}, {c.Y:F1}, {c.Z:F1})");
    }

    if (File.Exists(prep) && File.Exists(ant))
    {
        var prepB = parser.ParseFile(prep, sceneTransformKind: SceneTransformKind.Scan).Bounds;
        var antB = parser.ParseFile(ant, sceneTransformKind: SceneTransformKind.Scan).Bounds;
        var antInv = parser.ParseFile(ant, sceneTransformKind: SceneTransformKind.ScanInverse).Bounds;

        Console.WriteLine("\n--- Docs alignment (forward scan transform for all) ---");
        ReportPair("prep vs ant", prepB, antB);
        ReportPair("prep vs ant (ant inverse auto-old)", prepB, antInv);
    }

    if (File.Exists(prep) && File.Exists(crown))
    {
        var prepB = parser.ParseFile(prep, sceneTransformKind: SceneTransformKind.Scan).Bounds;
        var crownDesigned = parser.ParseFile(crown, sceneTransformKind: SceneTransformKind.Designed).Bounds;
        var crownNone = parser.ParseFile(crown, sceneTransformKind: SceneTransformKind.None).Bounds;

        Console.WriteLine("\n--- Docs crown ---");
        ReportPair("prep vs crown (Designed transform)", prepB, crownDesigned);
        ReportPair("prep vs crown (no transform)", prepB, crownNone);
    }

    Console.WriteLine("\nUsage: DcmAlignmentCheck <orderFolder>");
    return;
}

if (!Directory.Exists(orderFolder))
{
    Console.WriteLine($"Order folder not found: {orderFolder}");
    return;
}

Console.WriteLine($"=== Order alignment check ===\n{orderFolder}\n");

var scansFolder = Path.Combine(orderFolder, "Scans");
string? prepPath = null;
string? antPath = null;
if (Directory.Exists(scansFolder))
{
    var allScans = Directory.GetFiles(scansFolder, "*.dcm", SearchOption.AllDirectories);
    prepPath = allScans.FirstOrDefault(p =>
        p.Contains("Preparation", StringComparison.OrdinalIgnoreCase) &&
        !p.Contains("Antagonist", StringComparison.OrdinalIgnoreCase) &&
        !p.Contains("AbutmentAlignment", StringComparison.OrdinalIgnoreCase));
    antPath = allScans.FirstOrDefault(p =>
        p.Contains("Antagonist", StringComparison.OrdinalIgnoreCase) &&
        !p.Contains("Preparation", StringComparison.OrdinalIgnoreCase));
}

var scanFiles = new List<string>();
if (!string.IsNullOrWhiteSpace(prepPath))
{
    scanFiles.Add(prepPath);
}

if (!string.IsNullOrWhiteSpace(antPath))
{
    scanFiles.Add(antPath);
}

var crownFiles = Directory.GetFiles(orderFolder, "*.dcm", SearchOption.AllDirectories)
    .Where(p => !p.Contains(@"\Scans\", StringComparison.OrdinalIgnoreCase))
    .Where(p => !p.Contains(@"\External models\", StringComparison.OrdinalIgnoreCase))
    .OrderBy(p =>
        p.Contains(@"\CAD\", StringComparison.OrdinalIgnoreCase) ? 0 :
        p.Contains(@"Anatomy elements", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
    .Take(3)
    .ToList();

if (scanFiles.Count == 0)
{
    Console.WriteLine("No preparation/antagonist scans found under Scans\\");
    return;
}

var prepBounds = parser.ParseFile(scanFiles[0], sceneTransformKind: SceneTransformKind.Scan).Bounds;
Console.WriteLine($"Prep: {Path.GetFileName(scanFiles[0])}");
Console.WriteLine($"  center {Center(prepBounds)}");

Rect3D? antBounds = null;
if (scanFiles.Count > 1)
{
    antBounds = parser.ParseFile(scanFiles[1], sceneTransformKind: SceneTransformKind.Scan).Bounds;
    Console.WriteLine($"Ant: {Path.GetFileName(scanFiles[1])}");
    ReportPair("prep vs ant", prepBounds, antBounds.Value);

    var prepInv = parser.ParseFile(scanFiles[0], sceneTransformKind: SceneTransformKind.ScanInverse).Bounds;
    var antInv = parser.ParseFile(scanFiles[1], sceneTransformKind: SceneTransformKind.ScanInverse).Bounds;
    ReportPair("prep(inv) vs ant", prepInv, antBounds.Value);
    ReportPair("prep vs ant(inv)", prepBounds, antInv);
    ReportPair("prep(inv) vs ant(inv)", prepInv, antInv);
}

foreach (var crownPath in crownFiles)
{
    var designed = parser.ParseFile(crownPath, sceneTransformKind: SceneTransformKind.Designed).Bounds;
    var designedFull = parser.ParseFile(crownPath, sceneTransformKind: SceneTransformKind.DesignedFull).Bounds;
    var bestFit = parser.ParseFile(crownPath, sceneTransformKind: SceneTransformKind.CadBestFit).Bounds;
    var none = parser.ParseFile(crownPath, sceneTransformKind: SceneTransformKind.None).Bounds;

    Console.WriteLine($"\nCrown: {crownPath.Replace(orderFolder + Path.DirectorySeparatorChar, "")}");
    ReportPair("prep vs placement-only", prepBounds, designed);
    ReportPair("prep vs library+placement", prepBounds, designedFull);
    ReportPair("prep vs CAD best-fit", prepBounds, bestFit);
    ReportPair("prep vs none", prepBounds, none);

    if (antBounds is not null)
    {
        ReportPair("ant vs placement-only", antBounds.Value, designed);
        ReportPair("ant vs library+placement", antBounds.Value, designedFull);
        ReportPair("ant vs CAD best-fit", antBounds.Value, bestFit);
    }
}

