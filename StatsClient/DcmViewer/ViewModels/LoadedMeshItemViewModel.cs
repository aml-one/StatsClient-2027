using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DCMViewer.Services;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
namespace DCMViewer.ViewModels;

public enum MeshCategory
{
    Model,
    Scan,
    Restoration,
    Abutment
}

public sealed class LoadedMeshItemViewModel : INotifyPropertyChanged
{
    /// <summary>Opacity at or above this value uses the opaque render pass.</summary>
    public const double OpaqueOpacityThreshold = 0.999;

    private MaterialPalette _palette;
    private double _specularIntensity = 1.0;
    private double _specularShininess = 90.0;
    private double _opacity = 1.0;
    private bool _isVisible = true;

    private EditableMeshState _editableMesh;
    private Point3D[]? _sculptBaselinePositions;
    private MaterialPalette? _savedPalette;
    private string? _savedTextureName;

    public LoadedMeshItemViewModel(
        string filePath,
        MeshGeometryModel3D model,
        MeshSnapshot meshSnapshot,
        MaterialPalette palette,
        Rect3D bounds,
        int vertexCount,
        int triangleCount,
        bool isEncrypted = false,
        bool isPackageLocked = false,
        MeshCategory category = MeshCategory.Model,
        ScanLayerArch scanArch = ScanLayerArch.None,
        bool isLoadFailed = false,
        string? loadError = null,
        string? appliedTextureName = null,
        DcmMeshWriteProfile? writeProfile = null)
    {
        FilePath = filePath;
        DisplayName = Path.GetFileName(filePath);
        ScanArch = scanArch;
        Model = model;
        _editableMesh = new EditableMeshState(meshSnapshot);
        WriteProfile = writeProfile;
        _palette = palette;
        Bounds = bounds;
        VertexCount = _editableMesh.VertexCount;
        TriangleCount = _editableMesh.TriangleCount;
        IsEncrypted = isEncrypted;
        IsPackageLocked = isPackageLocked;
        Category = category;
        IsLoadFailed = isLoadFailed;
        LoadError = loadError;
        AppliedTextureName = appliedTextureName;
        ApplyMaterial();
        CaptureSculptBaseline();
    }

    public DcmMeshWriteProfile? WriteProfile { get; }

    internal bool CanSaveSculptedDcm =>
        !IsLoadFailed &&
        WriteProfile is not null &&
        string.Equals(Path.GetExtension(FilePath), ".dcm", StringComparison.OrdinalIgnoreCase) &&
        HasSculptChanges;

    internal bool HasSculptChanges => !ArePositionsEqual(_sculptBaselinePositions, CaptureSculptPositions());

    internal void CaptureSculptBaseline() => _sculptBaselinePositions = CaptureSculptPositions();

    internal void MarkSculptSaved() => CaptureSculptBaseline();

    private static bool ArePositionsEqual(Point3D[]? baseline, Point3D[] current)
    {
        if (baseline is null || baseline.Length != current.Length)
        {
            return false;
        }

        for (var index = 0; index < baseline.Length; index++)
        {
            var delta = baseline[index] - current[index];
            if (delta.LengthSquared > 1e-12)
            {
                return false;
            }
        }

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath { get; }

    public string DisplayName { get; }

    public MeshGeometryModel3D Model { get; }

    public MeshSnapshot? MeshSnapshot => IsLoadFailed ? null : _editableMesh.ToSnapshot();

    internal Vector3D GetSurfaceNormalNear(Point3D point) => _editableMesh.GetNearestSurfaceNormal(point);

    internal Point3D[] BuildSculptBrushRingPoints(Point3D center, Vector3D normal, double radius)
    {
        if (IsLoadFailed)
        {
            return [center];
        }

        return _editableMesh.BuildSurfaceBrushRing(center, normal, radius);
    }

    internal void PushTemporaryMaterial(MaterialPalette palette, string textureName)
    {
        _savedPalette ??= _palette;
        _savedTextureName ??= AppliedTextureName;
        SetMaterialPalette(palette, textureName);
    }

    internal void PopTemporaryMaterial()
    {
        if (_savedPalette is null)
        {
            return;
        }

        SetMaterialPalette(_savedPalette.Value, _savedTextureName);
        _savedPalette = null;
        _savedTextureName = null;
    }

    internal Point3D[] CaptureSculptPositions() => _editableMesh.CapturePositions();

    internal void RestoreSculptPositions(Point3D[] positions)
    {
        if (IsLoadFailed)
        {
            return;
        }

        _editableMesh.RestorePositions(positions);
        RefreshGeometryAfterSculpt();
    }

    internal void ReplaceMeshGeometry(MeshSnapshot snapshot)
    {
        if (IsLoadFailed)
        {
            return;
        }

        _editableMesh = new EditableMeshState(snapshot);
        RefreshGeometryAfterSculpt();
    }

    internal bool TryApplySculptStroke(
        SculptBrushTool tool,
        Point3D center,
        Vector3D surfaceNormal,
        double radius,
        double strength,
        Vector3D? grabDelta)
    {
        if (IsLoadFailed)
        {
            return false;
        }

        return _editableMesh.ApplyStroke(tool, center, surfaceNormal, radius, strength, grabDelta);
    }

    internal void RefreshGeometryAfterSculpt()
    {
        if (IsLoadFailed)
        {
            return;
        }

        _editableMesh.PushToModel(Model);
        ApplyMaterial();
    }

    public Rect3D Bounds { get; }

    public int VertexCount { get; }

    public int TriangleCount { get; }

    public bool IsEncrypted { get; }

    public bool IsPackageLocked { get; }

    public MeshCategory Category { get; }

    public ScanLayerArch ScanArch { get; }

    public string SliderKnobIconPath => Category switch
    {
        MeshCategory.Model => "/DcmViewer/Images/model.png",
        MeshCategory.Scan => "/DcmViewer/Images/scan.png",
        MeshCategory.Restoration => "/DcmViewer/Images/restoration.png",
        MeshCategory.Abutment => "/DcmViewer/Images/abutment.png",
        _ => "/DcmViewer/Images/model.png"
    };

    public bool UseCategoryGroupControl =>
        Category is MeshCategory.Restoration or MeshCategory.Abutment;

    /// <summary>Layer visibility toggle; independent of opacity slider (failed/encrypted rows still toggle).</summary>
    public bool IsLayerCheckBoxEnabled => true;

    /// <summary>Maroon row styling for failed decode or package-locked encrypted files.</summary>
    public bool UsesFailedLayerStyle => IsLoadFailed || IsPackageLocked;

    public string? AppliedTextureName { get; private set; }

    public bool IsLoadFailed { get; }

    public string? LoadError { get; }

    public bool IsOpacityEnabled => !IsLoadFailed;

    public double Opacity
    {
        get => _opacity;
        set
        {
            var clampedValue = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_opacity - clampedValue) < 0.0001)
            {
                return;
            }

            _opacity = clampedValue;
            ApplyRenderState();
            OnPropertyChanged();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            ApplyRenderState();
            OnPropertyChanged();
        }
    }

    public void SetSpecularIntensity(double intensity)
    {
        _specularIntensity = Math.Clamp(intensity, 0.0, 1.0);
        ApplyMaterial();
    }

    public void SetSpecularShininess(double shininess)
    {
        _specularShininess = Math.Clamp(shininess, 1.0, 200.0);
        ApplyMaterial();
    }

    public void SetMaterialPalette(MaterialPalette palette, string? textureName = null)
    {
        _palette = palette;
        AppliedTextureName = textureName;
        ApplyMaterial();
    }

    public void ApplySectionPlane(Point3D planePoint, Vector3D planeNormal, bool enabled)
    {
        // 2D cross-section uses SectionGeometryService; 3D viewport meshes are not plane-clipped.
    }

    public void ApplyRenderState()
    {
        ApplyMaterial();
        Model.Visibility = _isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        Model.RenderOrder = GetRenderOrder();
    }

    private int GetRenderOrder()
    {
        var order = Category switch
        {
            MeshCategory.Scan => 0,
            MeshCategory.Model => 100,
            MeshCategory.Abutment => 200,
            MeshCategory.Restoration => 300,
            _ => 400
        };

        if (_opacity < OpaqueOpacityThreshold)
        {
            order += 50;
        }

        return order;
    }

    private void ApplyMaterial()
    {
        var frontDiffuse = _palette.FrontDiffuse.ToColor4(_opacity);
        var backDiffuse = _palette.BackDiffuse.ToColor4(_opacity);
        var specularIntensity = Math.Clamp(_specularIntensity * _palette.SpecularIntensityScale, 0.0, 1.0);
        var specular = _palette.FrontSpecular.ToColor4(specularIntensity);
        var shininess = _palette.SpecularShininessOverride > 0
            ? _palette.SpecularShininessOverride
            : _specularShininess;

        Model.Material = new PhongMaterial
        {
            AmbientColor = frontDiffuse * (float)_palette.AmbientScale,
            DiffuseColor = frontDiffuse,
            SpecularColor = specular,
            SpecularShininess = (float)shininess,
            EmissiveColor = backDiffuse * (float)_palette.EmissiveScale
        };

        // Phong alpha only — Helix transparent pass + embedded clear causes black background.
        Model.IsTransparent = false;
        Model.CullMode = SharpDX.Direct3D11.CullMode.None;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static LoadedMeshItemViewModel CreateFailed(
        string filePath,
        string error,
        MeshCategory category = MeshCategory.Model,
        ScanLayerArch scanArch = ScanLayerArch.None,
        bool isPackageLocked = false,
        bool isEncrypted = false)
    {
        var placeholder = new MeshGeometryModel3D
        {
            Visibility = System.Windows.Visibility.Collapsed
        };

        return new LoadedMeshItemViewModel(
            filePath,
            placeholder,
            new MeshSnapshot(Array.Empty<Point3D>(), Array.Empty<int>()),
            MaterialLibrary.Get(MaterialLibrary.DefaultName),
            Rect3D.Empty,
            0,
            0,
            isEncrypted: isEncrypted,
            isPackageLocked: isPackageLocked,
            category: category,
            scanArch: scanArch,
            isLoadFailed: true,
            loadError: error)
        {
            _isVisible = false
        };
    }
}
