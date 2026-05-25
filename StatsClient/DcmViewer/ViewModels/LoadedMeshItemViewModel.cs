using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DCMViewer.Services;

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
    // Convention for _opacityBrushes: [0] = front diffuse, [1] = back diffuse, [2] = front specular.
    private readonly IReadOnlyList<SolidColorBrush> _opacityBrushes;
    private readonly IReadOnlyList<SolidColorBrush> _specularBrushes;
    private Color[] _baseSpecularColors;
    private double _opacity = 1.0;
    private bool _isVisible = true;

    public LoadedMeshItemViewModel(
        string filePath,
        Model3D model,
        IEnumerable<SolidColorBrush> opacityBrushes,
        IEnumerable<SolidColorBrush>? specularBrushes,
        Rect3D bounds,
        int vertexCount,
        int triangleCount,
        bool isEncrypted = false,
        bool isPackageLocked = false,
        MeshCategory category = MeshCategory.Model,
        bool isLoadFailed = false,
        string? loadError = null,
        string? appliedTextureName = null)
    {
        FilePath = filePath;
        DisplayName = Path.GetFileName(filePath);
        Model = model;
        _opacityBrushes = opacityBrushes.ToArray();
        _specularBrushes = specularBrushes?.ToArray() ?? Array.Empty<SolidColorBrush>();
        _baseSpecularColors = _specularBrushes.Select(brush => brush.Color).ToArray();
        Bounds = bounds;
        VertexCount = vertexCount;
        TriangleCount = triangleCount;
        IsEncrypted = isEncrypted;
        IsPackageLocked = isPackageLocked;
        Category = category;
        IsLoadFailed = isLoadFailed;
        LoadError = loadError;
        AppliedTextureName = appliedTextureName;
        ApplyOpacity();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath { get; }

    public string DisplayName { get; }

    public Model3D Model { get; }

    public MeshGeometry3D? MeshGeometry => (Model as GeometryModel3D)?.Geometry as MeshGeometry3D;

    public Rect3D Bounds { get; }

    public int VertexCount { get; }

    public int TriangleCount { get; }

    public bool IsEncrypted { get; }

    public bool IsPackageLocked { get; }

    public MeshCategory Category { get; }

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

    /// <summary>Name of the texture/material from <see cref="MaterialLibrary"/> currently applied to this mesh.</summary>
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
            ApplyOpacity();
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
            OnPropertyChanged();
        }
    }

    public void SetSpecularIntensity(double intensity)
    {
        var clampedIntensity = Math.Clamp(intensity, 0.0, 1.0);

        for (var i = 0; i < _specularBrushes.Count; i++)
        {
            var baseColor = _baseSpecularColors[i];
            _specularBrushes[i].Color = Color.FromRgb(
                ScaleComponent(baseColor.R, clampedIntensity),
                ScaleComponent(baseColor.G, clampedIntensity),
                ScaleComponent(baseColor.B, clampedIntensity));
        }
    }

    /// <summary>
    /// Replaces the diffuse and specular colors of this mesh with those from
    /// <paramref name="palette"/>.  Call <see cref="SetSpecularIntensity"/> afterwards
    /// to re-apply the current finish level.
    /// </summary>
    public void SetMaterialPalette(MaterialPalette palette, string? textureName = null)
    {
        if (_opacityBrushes.Count >= 1) _opacityBrushes[0].Color = palette.FrontDiffuse;
        if (_opacityBrushes.Count >= 2) _opacityBrushes[1].Color = palette.BackDiffuse;
        if (_opacityBrushes.Count >= 3) _opacityBrushes[2].Color = palette.FrontSpecular;

        // Reset specular base colors so intensity scaling uses the new values.
        for (var i = 0; i < _baseSpecularColors.Length; i++)
        {
            if (i < _specularBrushes.Count)
                _baseSpecularColors[i] = _specularBrushes[i].Color;
        }

        AppliedTextureName = textureName;
    }

    private void ApplyOpacity()
    {
        foreach (var brush in _opacityBrushes)
        {
            brush.Opacity = _opacity;
        }
    }

    private static byte ScaleComponent(byte component, double intensity)
    {
        var scaled = (int)Math.Round(component * intensity);
        return (byte)Math.Clamp(scaled, 0, 255);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static LoadedMeshItemViewModel CreateFailed(
        string filePath,
        string error,
        MeshCategory category = MeshCategory.Model,
        bool isPackageLocked = false,
        bool isEncrypted = false)
    {
        return new LoadedMeshItemViewModel(
            filePath,
            new Model3DGroup(),
            Array.Empty<SolidColorBrush>(),
            Array.Empty<SolidColorBrush>(),
            Rect3D.Empty,
            0,
            0,
            isEncrypted: isEncrypted,
            isPackageLocked: isPackageLocked,
            category: category,
            isLoadFailed: true,
            loadError: error)
        {
            _isVisible = false
        };
    }
}
