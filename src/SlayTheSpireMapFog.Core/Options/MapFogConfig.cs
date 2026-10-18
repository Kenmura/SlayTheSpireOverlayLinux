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

    /// <summary>
    /// Keep every node on the current floor or any floor below it exposed, so unpicked alternatives stay visible.
    /// Defaults to true.
    /// </summary>
    public bool RevealPassedNodes { get; set; } = true;

    /// <summary>
    /// Draw a dark fog over the unexplored part of the map instead of leaving the bare background.
    /// Defaults to true.
    /// </summary>
    public bool DarkFog { get; set; } = true;

    /// <summary>
    /// Opacity/strength of the dark fog overlay (clamped between 0.3 and 1.0, defaults to 0.9).
    /// </summary>
    public float FogOpacity { get; set; } = 0.90f;
}
