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
        string? appliedTextureName = null)
    {
        FilePath = filePath;
        DisplayName = Path.GetFileName(filePath);
        ScanArch = scanArch;
        Model = model;
        MeshSnapshot = meshSnapshot;
        _palette = palette;
        Bounds = bounds;
        VertexCount = vertexCount;
        TriangleCount = triangleCount;
        IsEncrypted = isEncrypted;
        IsPackageLocked = isPackageLocked;
        Category = category;
        IsLoadFailed = isLoadFailed;
        LoadError = loadError;
        AppliedTextureName = appliedTextureName;
        ApplyMaterial();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath { get; }

    public string DisplayName { get; }

    public MeshGeometryModel3D Model { get; }

    public MeshSnapshot? MeshSnapshot { get; }

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
        Category is MeshCategory.Restoration or MeshCategory.Abutment
        && !IsLoadFailed
        && !IsPackageLocked;

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
            if (IsLoadFailed)
            {
                return;
            }

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

        Model.IsTransparent = _opacity < OpaqueOpacityThreshold;
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
