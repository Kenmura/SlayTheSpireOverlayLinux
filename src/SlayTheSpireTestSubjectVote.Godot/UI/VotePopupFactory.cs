using Godot;
using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireTestSubjectVote.Godot.UI;

/// <summary>Per-pop-up settings that depend on game state the UI namespace cannot read itself.</summary>
/// <param name="CanSkip">True on the host: shows the Skip button and hint (the escape hatch for a stuck vote).</param>
/// <param name="SkipKeyName">Name of the host's skip hotkey, shown in the hint.</param>
/// <param name="RefreshCard">Re-asserts what the card preview shows; called every tick because pooled game nodes can be re-skinned by the game. Null means never.</param>
/// <param name="ReleaseCard">Hands the card preview back to the game (its node pool). Null means just free it.</param>
public readonly record struct PopupOptions(bool CanSkip, string SkipKeyName, Action<Control>? ReleaseCard, Action<Control>? RefreshCard = null);


/// <summary>Builds the vote pop-ups from built-in Godot nodes only (never subclass Control in a mod assembly).</summary>
public static class VotePopupFactory
{
    private static readonly Vector2 CardArea = new(300, 420);

    /// <param name="cardPreview">The game's own card node for the held card (built by the caller; this namespace cannot touch game types). May be null.</param>
    public static VotePopupView CreateForVoter(string playerName, string cardName, Control? cardPreview, int strength, Action<bool> onVote, PopupOptions options)
    {
        var allow = PopupStyle.Action(VoteCopy.AllowLabel, new Color(0.18f, 0.42f, 0.23f), PopupStyle.AllowGreen);
        var reject = PopupStyle.Action(VoteCopy.RejectLabel, new Color(0.48f, 0.17f, 0.17f), PopupStyle.RejectRed);
        var buttons = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 14);
        buttons.AddChild(allow);
        buttons.AddChild(reject);

        var view = Assemble(blockInput: true, VoteCopy.Headline(playerName, cardName), cardPreview, VoteCopy.CostLine(strength), buttons, options, allow, [allow, reject]);
        allow.Pressed += () => { view.LockButtons(); onVote(true); };
        reject.Pressed += () => { view.LockButtons(); onVote(false); };
        return view;
    }

    public static VotePopupView CreateForWaiting(string cardName, Control? cardPreview, PopupOptions options) =>
        Assemble(blockInput: false, $"{cardName} is on hold", cardPreview, VoteCopy.WaitingLine(0), actions: null, options, firstFocus: null, lockable: []);

    private static VotePopupView Assemble(bool blockInput, string headline, Control? cardPreview, string subline, Control? actions, PopupOptions options, Button? firstFocus, Button[] lockable)
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

        Button? skip = null;
        if (options.CanSkip)
        {
            skip = PopupStyle.Action(VoteCopy.SkipLabel, new Color(0.30f, 0.26f, 0.18f), PopupStyle.Waiting);
            column.AddChild(PopupStyle.Text(VoteCopy.SkipHint(options.SkipKeyName), 13, PopupStyle.Waiting));
            column.AddChild(skip);
        }

        panel.AddChild(column);
        center.AddChild(panel);
        layer.AddChild(dim);
        layer.AddChild(center);

        var focusTarget = firstFocus ?? skip;
        Callable.From(() =>
        {
            if (Engine.GetMainLoop() is not SceneTree tree) return;
            tree.Root.AddChild(layer);
            if (focusTarget != null && GodotObject.IsInstanceValid(focusTarget)) focusTarget.GrabFocus();
        }).CallDeferred();

        var view = new VotePopupView(layer, status, subtitle, pips, cardArea, cardPreview, lockable, options.ReleaseCard, options.RefreshCard);
        if (skip != null) skip.Pressed += view.RequestSkip;
        return view;
    }

    private static Control BuildCardArea(Control? cardPreview)
    {
        var area = new Control { CustomMinimumSize = CardArea, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
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
