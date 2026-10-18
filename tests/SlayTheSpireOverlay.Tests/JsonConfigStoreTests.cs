using SlayTheSpireMod.Common;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class JsonConfigStoreTests
{
    public sealed class Cfg
    {
        public bool Enabled { get; set; } = true;

        public int Depth { get; set; } = 1;
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), "sts2cfg-" + Guid.NewGuid().ToString("N"), "cfg.json");

    [TestMethod]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var cfg = JsonConfigStore.Load<Cfg>(TempPath());
        Assert.IsTrue(cfg.Enabled);
        Assert.AreEqual(1, cfg.Depth);
    }

    [TestMethod]
    public void SaveThenLoad_RoundTripsAndLeavesNoTempFile()
    {
        string path = TempPath();
        Assert.IsTrue(JsonConfigStore.Save(path, new Cfg { Enabled = false, Depth = 3 }));
        var loaded = JsonConfigStore.Load<Cfg>(path);
        Assert.IsFalse(loaded.Enabled);
        Assert.AreEqual(3, loaded.Depth);
        Assert.IsFalse(File.Exists(path + ".tmp"));
    }

    [TestMethod]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        Assert.AreEqual(1, JsonConfigStore.Load<Cfg>(path).Depth);
    }

    [TestMethod]
    public void Save_UnwritablePath_ReportsErrorAndReturnsFalse()
    {
        string? error = null;
        string blocker = Path.Combine(Path.GetTempPath(), "sts2cfg-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "i am a file, not a directory");
        bool ok = JsonConfigStore.Save(Path.Combine(blocker, "cfg.json"), new Cfg(), e => error = e);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }
}
