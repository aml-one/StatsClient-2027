namespace DCMViewer.Services;

/// <summary>Which viewer interactions are active for each design workflow phase.</summary>
public enum StatsDesignWorkflowPhase
{
    Margin,
    Inner,
    Shape,
    Sculpt,
    SafetyCheck,
    Export
}

/// <summary>
/// Ordered design workflow steps for navigation and UI.
/// </summary>
public static class StatsDesignWorkflowCoordinator
{
    public static readonly IReadOnlyList<StatsDesignWorkflowPhase> OrderedPhases =
    [
        StatsDesignWorkflowPhase.Margin,
        StatsDesignWorkflowPhase.Inner,
        StatsDesignWorkflowPhase.Shape,
        StatsDesignWorkflowPhase.Sculpt,
        StatsDesignWorkflowPhase.Export
    ];

    public static StatsDesignWorkflowPhase Parse(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return StatsDesignWorkflowPhase.Margin;
        }

        return step.Trim() switch
        {
            "Margin" => StatsDesignWorkflowPhase.Margin,
            "Inner" => StatsDesignWorkflowPhase.Inner,
            "Cement" => StatsDesignWorkflowPhase.Inner,
            "Shape" => StatsDesignWorkflowPhase.Shape,
            "Sculpt" => StatsDesignWorkflowPhase.Sculpt,
            "Safety" or "SafetyCheck" => StatsDesignWorkflowPhase.SafetyCheck,
            "Export" => StatsDesignWorkflowPhase.Export,
            _ => StatsDesignWorkflowPhase.Margin
        };
    }

    public static string ToStepName(StatsDesignWorkflowPhase phase) => phase switch
    {
        StatsDesignWorkflowPhase.Margin => "Margin",
        StatsDesignWorkflowPhase.Inner => "Inner",
        StatsDesignWorkflowPhase.Shape => "Shape",
        StatsDesignWorkflowPhase.Sculpt => "Sculpt",
        StatsDesignWorkflowPhase.SafetyCheck => "Safety",
        StatsDesignWorkflowPhase.Export => "Export",
        _ => "Margin"
    };

    public static string ToDisplayLabel(StatsDesignWorkflowPhase phase) => phase switch
    {
        StatsDesignWorkflowPhase.Margin => "Margin",
        StatsDesignWorkflowPhase.Inner => "Inner shell",
        StatsDesignWorkflowPhase.Shape => "Library",
        StatsDesignWorkflowPhase.Sculpt => "Sculpt",
        StatsDesignWorkflowPhase.SafetyCheck => "Safety",
        StatsDesignWorkflowPhase.Export => "Export",
        _ => "Margin"
    };

    public static bool EnablesMarginPicking(StatsDesignWorkflowPhase phase) =>
        phase == StatsDesignWorkflowPhase.Margin;

    public static bool EnablesSculpting(StatsDesignWorkflowPhase phase, bool hasClosedCrown) =>
        (phase == StatsDesignWorkflowPhase.Sculpt || phase == StatsDesignWorkflowPhase.SafetyCheck) && hasClosedCrown;

    public static bool EnablesSafetyTools(StatsDesignWorkflowPhase phase, bool hasClosedCrown) =>
        (phase == StatsDesignWorkflowPhase.Sculpt || phase == StatsDesignWorkflowPhase.SafetyCheck) && hasClosedCrown;

    public static int IndexOf(StatsDesignWorkflowPhase phase)
    {
        for (var i = 0; i < OrderedPhases.Count; i++)
        {
            if (OrderedPhases[i] == phase)
            {
                return i;
            }
        }

        return -1;
    }

    public static StatsDesignWorkflowPhase? Next(StatsDesignWorkflowPhase current) =>
        IndexOf(current) is int index && index >= 0 && index < OrderedPhases.Count - 1
            ? OrderedPhases[index + 1]
            : null;

    public static StatsDesignWorkflowPhase? Previous(StatsDesignWorkflowPhase current) =>
        IndexOf(current) is int index && index > 0 ? OrderedPhases[index - 1] : null;
}
