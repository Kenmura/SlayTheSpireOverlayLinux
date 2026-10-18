namespace SlayTheSpireOverlay.Tests;

/// <summary>
/// Structural guards that need no game files, so they run in CI. They exist because agents tend to
/// append to the biggest file instead of splitting it.
/// </summary>
[TestClass]
public class SourceHygieneTests
{
    private const int MaxLines = 500;

    // Files that are already over the limit, with the size they must not exceed.
    // Splitting a file lets you DELETE its entry; never raise a number or add an entry.
    // NOTE: re-baselined when the Overlay feature work (outgoing damage, deck hover, synergy, boss prep, etc.)
    // landed alongside this guard. These files are the next candidates to split; only ever lower these numbers.
    private static readonly Dictionary<string, int> Allowlist = new()
    {
        ["src/SlayTheSpireOverlay.Godot/UI/RoomOverlayFactory.cs"] = 1592,
        ["src/SlayTheSpireOverlay.Godot/ModEntry.cs"] = 815,
        ["src/SlayTheSpireOverlay.Godot/Sts2RuntimeInspector.cs"] = 1148,
        ["src/SlayTheSpireOverlay.Core/Services/OverlayCalculators.cs"] = 1017,
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SlayTheSpireOverlay.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root (SlayTheSpireOverlay.slnx) not found.");
    }

    private static IEnumerable<(string Relative, int Lines)> SourceFiles()
    {
        string root = RepoRoot();
        string src = Path.Combine(root, "src");
        foreach (string file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal)) continue;
            yield return (relative, File.ReadLines(file).Count());
        }
    }

    [TestMethod]
    public void SourceFiles_StayWithinTheSizeLimit()
    {
        var offenders = SourceFiles()
            .Where(f => f.Lines > (Allowlist.TryGetValue(f.Relative, out int cap) ? cap : MaxLines))
            .Select(f => $"{f.Relative}: {f.Lines} lines (limit {(Allowlist.TryGetValue(f.Relative, out int c) ? c : MaxLines)}). Split it into focused files (partial classes are fine) instead of adding to it.")
            .ToList();
        Assert.IsEmpty(offenders, string.Join(Environment.NewLine, offenders));
    }

    [TestMethod]
    public void Allowlist_OnlyContainsFilesThatAreStillOversized()
    {
        var sizes = SourceFiles().ToDictionary(f => f.Relative, f => f.Lines);
        var stale = Allowlist
            .Where(e => !sizes.TryGetValue(e.Key, out int lines) || lines <= MaxLines)
            .Select(e => e.Key)
            .ToList();
        Assert.IsEmpty(stale, "Remove from the allowlist (it is now small enough or gone): " + string.Join(", ", stale));
    }
}
