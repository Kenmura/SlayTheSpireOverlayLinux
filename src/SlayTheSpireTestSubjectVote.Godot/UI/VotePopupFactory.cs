using Godot;
using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireTestSubjectVote.Godot.UI;

/// <summary>Builds the vote pop-ups from built-in Godot nodes only (never subclass Control in a mod assembly).</summary>
public static class VotePopupFactory
{
    public static VotePopupView CreateForVoter(string playerName, string cardName, int strength, Action<bool> onVote)
    {
        var status = Label(string.Empty);
        var allow = new Button { Text = VoteCopy.AllowLabel, MouseFilter = Control.MouseFilterEnum.Stop };
        var reject = new Button { Text = VoteCopy.RejectLabel, MouseFilter = Control.MouseFilterEnum.Stop };
        var buttons = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        buttons.AddChild(allow);
        buttons.AddChild(reject);

        var layer = Build(blockInput: true, Label(VoteCopy.Headline(playerName, cardName)), Label(VoteCopy.CostLine(strength)), buttons, status);
        var view = new VotePopupView(layer, status, allow, reject);
        allow.Pressed += () => { view.LockButtons(); onVote(true); };
        reject.Pressed += () => { view.LockButtons(); onVote(false); };
        Show(layer);
        return view;
    }

    public static VotePopupView CreateForWaiting(string cardName)
    {
        var status = Label(string.Empty);
        var layer = Build(blockInput: false, Label($"{cardName} is on hold"), Label(VoteCopy.WaitingLine(0)), status);
        Show(layer);
        return new VotePopupView(layer, status);
    }

    private static Label Label(string text) => new() { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };

    private static CanvasLayer Build(bool blockInput, params Control[] rows)
    {
        var layer = new CanvasLayer { Layer = 100 };
        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = blockInput ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        foreach (var row in rows) column.AddChild(row);
        panel.AddChild(column);
        center.AddChild(panel);
        layer.AddChild(dim);
        layer.AddChild(center);
        return layer;
    }

    private static void Show(CanvasLayer layer)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            Callable.From(() => tree.Root.AddChild(layer)).CallDeferred();
        }
    }
}
