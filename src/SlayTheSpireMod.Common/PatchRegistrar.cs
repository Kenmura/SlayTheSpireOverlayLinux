namespace SlayTheSpireMod.Common;

/// <summary>
/// Runs patch-application steps independently so one missing game member cannot stop the rest,
/// and reports exactly which patches were skipped and why.
/// </summary>
public sealed class PatchRegistrar
{
    private readonly Action<string> _log;
    private readonly string _prefix;
    private readonly List<string> _applied = [];
    private readonly List<(string Name, string Reason)> _skipped = [];

    public PatchRegistrar(string logPrefix, Action<string> log)
    {
        _prefix = logPrefix;
        _log = log;
    }

    public int AppliedCount => _applied.Count;

    public int SkippedCount => _skipped.Count;

    public IReadOnlyList<(string Name, string Reason)> Skipped => _skipped;

    public bool TryApply(string name, Action apply)
    {
        try
        {
            apply();
            _applied.Add(name);
            return true;
        }
        catch (Exception ex)
        {
            _skipped.Add((name, ex.Message));
            _log($"{_prefix} patch '{name}' skipped: {ex.Message}");
            return false;
        }
    }

    public string Summary() => $"{_prefix} patches: applied {AppliedCount}, skipped {SkippedCount}";
}
