using Godot;
using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireTestSubjectVote.Godot.UI;

/// <summary>Builds the vote pop-ups from built-in Godot nodes only (never subclass Control in a mod assembly).</summary>
public static class VotePopupFactory
{
    private static readonly Vector2 CardArea = new(300, 420);

    /// <param name="cardPreview">The game's own card node for the held card (built by the caller; this namespace cannot touch game types). May be null.</param>
    public static VotePopupView CreateForVoter(string playerName, string cardName, Control? cardPreview, int strength, Action<bool> onVote)
    {
        var allow = PopupStyle.Action(VoteCopy.AllowLabel, new Color(0.18f, 0.42f, 0.23f), PopupStyle.AllowGreen);
        var reject = PopupStyle.Action(VoteCopy.RejectLabel, new Color(0.48f, 0.17f, 0.17f), PopupStyle.RejectRed);
        var buttons = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 14);
        buttons.AddChild(allow);
        buttons.AddChild(reject);

        var view = Assemble(blockInput: true, VoteCopy.Headline(playerName, cardName), cardPreview, VoteCopy.CostLine(strength), buttons, allow, reject);
        allow.Pressed += () => { view.LockButtons(); onVote(true); };
        reject.Pressed += () => { view.LockButtons(); onVote(false); };
        return view;
    }

    public static VotePopupView CreateForWaiting(string cardName, Control? cardPreview) =>
        Assemble(blockInput: false, $"{cardName} is on hold", cardPreview, VoteCopy.WaitingLine(0), actions: null);

    private static VotePopupView Assemble(bool blockInput, string headline, Control? cardPreview, string subline, Control? actions, params Button[] buttons)
    {
        var layer = new CanvasLayer { Layer = 100 };
        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = blockInput ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", PopupStyle.Panel());
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 8);

        var subtitle = PopupStyle.Text(subline, 16, blockInput ? PopupStyle.Warning : PopupStyle.Cream);
        var pips = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        var status = PopupStyle.Text(string.Empty, 15, PopupStyle.Cream);
        var cardArea = BuildCardArea(cardPreview);

        column.AddChild(PopupStyle.Text(headline, 22, PopupStyle.Gold));
        column.AddChild(cardArea);
        column.AddChild(subtitle);
        if (actions != null) column.AddChild(actions);
        column.AddChild(pips);
        column.AddChild(status);
        panel.AddChild(column);
        center.AddChild(panel);
        layer.AddChild(dim);
        layer.AddChild(center);

        Callable.From(() =>
        {
            if (Engine.GetMainLoop() is SceneTree tree) tree.Root.AddChild(layer);
        }).CallDeferred();
        return new VotePopupView(layer, status, subtitle, pips, cardArea, cardPreview, buttons);
    }

    private static Control BuildCardArea(Control? cardPreview)
    {
        var area = new Control { CustomMinimumSize = CardArea, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (cardPreview == null) return area;
        IgnoreMouse(cardPreview);
        cardPreview.Position = CardArea / 2f;
        area.AddChild(cardPreview);
        return area;
    }

    private static void IgnoreMouse(Node node)
    {
        if (node is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren()) IgnoreMouse(child);
    }
}
