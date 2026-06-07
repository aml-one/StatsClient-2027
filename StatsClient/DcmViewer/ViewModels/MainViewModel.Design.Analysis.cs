using DCMViewer.Infrastructure;

using DCMViewer.Services;

using System.Collections.ObjectModel;

using System.Windows.Media.Media3D;



namespace DCMViewer.ViewModels;



public partial class MainViewModel

{

    private string _undercutStatusText = "Close the margin to analyze undercuts.";

    private readonly ObservableCollection<Point3D> _undercutHotspotPoints = [];



    public ReadOnlyObservableCollection<Point3D> UndercutHotspotPoints { get; private set; } = null!;



    public string UndercutStatusText

    {

        get => _undercutStatusText;

        private set

        {

            if (string.Equals(_undercutStatusText, value, StringComparison.Ordinal))

            {

                return;

            }



            _undercutStatusText = value;

            OnPropertyChanged();

        }

    }



    public RelayCommand AnalyzeUndercutsCommand { get; private set; } = null!;



    private void InitDesignAnalysisCommands()

    {

        UndercutHotspotPoints = new ReadOnlyObservableCollection<Point3D>(_undercutHotspotPoints);

        AnalyzeUndercutsCommand = new RelayCommand(

            () => RefreshUndercutAnalysis(showStatus: true),

            () => IsDesignHostMode && IsMarginClosed && MarginPointCount >= 3);

    }



    internal void RefreshUndercutAnalysis(bool showStatus = false)

    {

        if (!IsDesignHostMode || !IsMarginClosed || MarginPointCount < 3)

        {

            UndercutStatusText = "Close the margin to analyze undercuts.";

            ClearUndercutHotspots();

            AnalyzeUndercutsCommand.RaiseCanExecuteChanged();

            return;

        }



        var marginLoop = BuildMarginDesignLoop();
        var prepMeshes = StatsDesignPrepMeshResolver.CollectSnapshotsForMargin(_loadedFiles, marginLoop);



        if (prepMeshes.Count == 0)

        {

            UndercutStatusText = "No scan surface found for the margin. Show the mesh the margin was drawn on.";

            ClearUndercutHotspots();

            return;

        }

        var axis = InsertionAxis;

        if (axis.LengthSquared < 1e-12)

        {

            axis = StatsDesignInsertionAxis.Calculate(_marginPoints);

        }



        StatsDesignUndercutAnalyzer.Result? worst = null;

        foreach (var prep in prepMeshes)

        {

            var result = StatsDesignUndercutAnalyzer.Analyze(prep, marginLoop, axis);

            if (worst is null || result.MaxUndercutDepthMm > worst.MaxUndercutDepthMm)

            {

                worst = result;

            }

        }



        UndercutStatusText = worst?.Summary ?? "Undercut analysis unavailable.";

        UpdateUndercutHotspots(worst?.Hotspots ?? []);

        AnalyzeUndercutsCommand.RaiseCanExecuteChanged();



        if (showStatus)

        {

            SetTransientStatus(UndercutStatusText);

        }

    }



    private void UpdateUndercutHotspots(IReadOnlyList<Point3D> hotspots)

    {

        _undercutHotspotPoints.Clear();

        foreach (var point in hotspots)

        {

            _undercutHotspotPoints.Add(point);

        }



        OnPropertyChanged(nameof(UndercutHotspotPoints));

    }



    private void ClearUndercutHotspots()

    {

        if (_undercutHotspotPoints.Count == 0)

        {

            return;

        }



        _undercutHotspotPoints.Clear();

        OnPropertyChanged(nameof(UndercutHotspotPoints));

    }

}

