using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions;
using SlayTheSpireMod.Common;
using SlayTheSpireTestSubjectVote.Godot.Patches;

namespace SlayTheSpireTestSubjectVote.Godot;

[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]
public static class ModEntry
{
    private const string LogPrefix = "[STS2 TestSubjectVote]";

    public static void Initialize()
    {
        GD.Print($"{LogPrefix} ModEntry.Initialize called!");

        var registrar = new PatchRegistrar(LogPrefix, message => GD.PrintErr(message));
        var harmony = new Harmony("SlayTheSpireTestSubjectVote.Godot");

        registrar.TryApply("PlayCardGate", () =>
        {
            VoteGate.OriginalExecuteAction = AccessTools.Method(typeof(PlayCardAction), "ExecuteAction")
                ?? throw new MissingMethodException("PlayCardAction.ExecuteAction");
            Patch(harmony, typeof(PlayCardGatePatch));
        });
        registrar.TryApply("RunStart", () => Patch(harmony, typeof(RunStartPatch)));

        GD.Print(registrar.Summary());
    }

    private static void Patch(Harmony harmony, Type patchClass)
    {
        var patched = harmony.CreateClassProcessor(patchClass).Patch();
        if (patched == null || patched.Count == 0) throw new MissingMethodException($"{patchClass.Name}: no matching method");
    }
}
