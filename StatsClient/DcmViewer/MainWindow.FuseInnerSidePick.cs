using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using DCMViewer.Services;
using DCMViewer.ViewModels;
using HelixToolkit.Wpf.SharpDX;

namespace DCMViewer;

public partial class MainWindow
{
    private bool _isFuseInnerSidePicking;
    private int _fuseInnerSidePickIndex;
    private LoadedMeshItemViewModel? _fuseInnerSideActiveTarget;
    private readonly List<FuseInnerSideHint> _fuseInnerSideHints = [];
    private IReadOnlyList<FuseInnerSidePickItem> _fuseInnerSidePickItems = [];
    private TaskCompletionSource<IReadOnlyList<FuseInnerSideHint>?>? _fuseInnerSidePickTcs;

    public Task<IReadOnlyList<FuseInnerSideHint>?> PickFuseInnerSideHintsAsync(
        IReadOnlyList<FuseInnerSidePickItem> items)
    {
        CancelFuseInnerSidePicking(null);
        _fuseInnerSidePickItems = items;
        _fuseInnerSidePickIndex = 0;
        _fuseInnerSideHints.Clear();
        _fuseInnerSidePickTcs = new TaskCompletionSource<IReadOnlyList<FuseInnerSideHint>?>();
        BeginFuseInnerSidePick();
        return _fuseInnerSidePickTcs.Task;
    }

    private void BeginFuseInnerSidePick()
    {
        if (_fuseInnerSidePickItems.Count == 0)
        {
            CompleteFuseInnerSidePicking([]);
            return;
        }

        _isFuseInnerSidePicking = true;
        HighlightActiveFuseInnerSideLayer();
        UpdateFuseInnerSidePickStatus();
        Viewport.Focus();
    }

    private void HighlightActiveFuseInnerSideLayer()
    {
        ClearFuseInnerSideLayerHighlight();

        if (_fuseInnerSidePickIndex < 0 || _fuseInnerSidePickIndex >= _fuseInnerSidePickItems.Count)
        {
            return;
        }

        _fuseInnerSideActiveTarget = _fuseInnerSidePickItems[_fuseInnerSidePickIndex].Target;
        _fuseInnerSideActiveTarget.PushTemporaryMaterial(MaterialLibrary.Get("WorkingSide"), "WorkingSide");
    }

    private void ClearFuseInnerSideLayerHighlight()
    {
        if (_fuseInnerSideActiveTarget is not null)
        {
            _fuseInnerSideActiveTarget.PopTemporaryMaterial();
            _fuseInnerSideActiveTarget = null;
        }
    }

    private void UpdateFuseInnerSidePickStatus()
    {
        if (_fuseInnerSidePickIndex >= _fuseInnerSidePickItems.Count)
        {
            return;
        }

        var item = _fuseInnerSidePickItems[_fuseInnerSidePickIndex];
        _viewModel.SetTransientStatus(
            $"Unified shell: click the working (inner) side on \"{item.DisplayName}\" ({_fuseInnerSidePickIndex + 1}/{_fuseInnerSidePickItems.Count}). Esc to cancel.");
    }

    private bool TryHandleFuseInnerSidePick(MouseButtonEventArgs e)
    {
        if (!_isFuseInnerSidePicking || _fuseInnerSidePickIndex >= _fuseInnerSidePickItems.Count)
        {
            return false;
        }

        var active = _fuseInnerSidePickItems[_fuseInnerSidePickIndex];
        var hits = Viewport.FindHits(e.GetPosition(Viewport));
        var hit = hits.FirstOrDefault(h =>
            h.ModelHit is MeshGeometryModel3D model &&
            ReferenceEquals(_viewModel.FindLoadedFileByModel(model), active.Target));

        if (hit is null)
        {
            _viewModel.SetTransientStatus($"Click on \"{active.DisplayName}\" — the layer highlighted in green.");
            e.Handled = true;
            return true;
        }

        var point = new Point3D(hit.PointHit.X, hit.PointHit.Y, hit.PointHit.Z);
        var normal = active.Target.GetSurfaceNormalNear(point);
        if (normal.LengthSquared > 1e-12)
        {
            normal.Normalize();
        }
        else
        {
            normal = new Vector3D(0, 0, 1);
        }

        _fuseInnerSideHints.Add(new FuseInnerSideHint(active.MeshIndex, point, normal));
        _fuseInnerSidePickIndex++;

        if (_fuseInnerSidePickIndex >= _fuseInnerSidePickItems.Count)
        {
            CompleteFuseInnerSidePicking(_fuseInnerSideHints.ToArray());
        }
        else
        {
            HighlightActiveFuseInnerSideLayer();
            UpdateFuseInnerSidePickStatus();
        }

        e.Handled = true;
        return true;
    }

    private void CompleteFuseInnerSidePicking(IReadOnlyList<FuseInnerSideHint> hints)
    {
        _isFuseInnerSidePicking = false;
        ClearFuseInnerSideLayerHighlight();
        _viewModel.UpdateDefaultStatusText();
        _fuseInnerSidePickTcs?.TrySetResult(hints);
        _fuseInnerSidePickTcs = null;
    }

    private void CancelFuseInnerSidePicking(IReadOnlyList<FuseInnerSideHint>? result)
    {
        if (!_isFuseInnerSidePicking && _fuseInnerSidePickTcs is null)
        {
            return;
        }

        _isFuseInnerSidePicking = false;
        ClearFuseInnerSideLayerHighlight();
        _viewModel.UpdateDefaultStatusText();
        _fuseInnerSidePickTcs?.TrySetResult(result);
        _fuseInnerSidePickTcs = null;
    }

    internal bool TryCancelFuseInnerSidePickingFromKeyboard()
    {
        if (!_isFuseInnerSidePicking)
        {
            return false;
        }

        CancelFuseInnerSidePicking(null);
        return true;
    }
}
