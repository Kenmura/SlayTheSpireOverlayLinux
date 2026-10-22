using Godot;
using SlayTheSpireMod.Common;

namespace SlayTheSpireTestSubjectVote.Godot;

[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]
public static class ModEntry
{
    private const string LogPrefix = "[STS2 TestSubjectVote]";

    public static void Initialize()
    {
        GD.Print($"{LogPrefix} ModEntry.Initialize called!");

        var registrar = new PatchRegistrar(LogPrefix, message => GD.PrintErr(message));

        GD.Print(registrar.Summary());
    }
}
