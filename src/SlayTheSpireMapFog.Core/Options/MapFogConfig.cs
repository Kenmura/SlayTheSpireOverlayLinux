namespace SlayTheSpireMapFog.Core.Options;

public enum FogMode
{
    FullFog = 0,
    HiddenIcons = 1
}

public class MapFogConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// FullFog hides future nodes and paths completely; HiddenIcons keeps the graph visible with '?' icons.
    /// Defaults to FullFog.
    /// </summary>
    public FogMode Mode { get; set; } = FogMode.FullFog;

    /// <summary>
    /// Whether the Act Boss icon at the top of the map remains visible through the fog.
    /// Defaults to true.
    /// </summary>
    public bool ShowBoss { get; set; } = true;

    /// <summary>
    /// Number of steps ahead of the current node to reveal (defaults to 1 step ahead).
    /// </summary>
    public int VisionDepth { get; set; } = 1;
}
