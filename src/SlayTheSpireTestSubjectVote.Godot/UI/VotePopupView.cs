using Godot;
using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireTestSubjectVote.Godot.UI;

/// <summary>Handle to a pop-up that is on screen. Every mutation is deferred onto the main loop and guarded by IsInstanceValid.</summary>
public sealed class VotePopupView
{
    private const int TicksPerWaitingLine = 30;

    private readonly CanvasLayer _layer;
    private readonly Label _status;
    private readonly Label _subtitle;
    private readonly HBoxContainer _pips;
    private readonly Control _cardArea;
    private readonly Control? _cardPreview;
    private readonly Button[] _buttons;
    private readonly Action<Control>? _releaseCard;
    private readonly Action<Control>? _refreshCard;
    private int _ticks;
    private bool _skipRequested;

    internal VotePopupView(CanvasLayer layer, Label status, Label subtitle, HBoxContainer pips, Control cardArea, Control? cardPreview, Button[] buttons, Action<Control>? releaseCard, Action<Control>? refreshCard = null)
    {
        _layer = layer;
        _status = status;
        _subtitle = subtitle;
        _pips = pips;
        _cardArea = cardArea;
        _cardPreview = cardPreview;
        _buttons = buttons;
        _releaseCard = releaseCard;
        _refreshCard = refreshCard;
    }

    /// <summary>True once if the host clicked Skip since the last call. Read from the vote loop (main thread).</summary>
    public bool ConsumeSkipRequest()
    {
        bool requested = _skipRequested;
        _skipRequested = false;
        return requested;
    }

    internal void RequestSkip() => _skipRequested = true;

    /// <summary>Refreshes the tally. Called about ten times a second; also rotates the waiting line for the card player.</summary>
    public void Update(VoteTally tally)
    {
        if (_cardPreview != null && GodotObject.IsInstanceValid(_cardPreview)) _refreshCard?.Invoke(_cardPreview);
        int allow = tally.Allow;
        int reject = tally.Reject;
        int total = tally.Total;
        int tick = _ticks++;
        bool rotateLine = _buttons.Length == 0 && tick % TicksPerWaitingLine == 0;
        Deferred(() =>
        {
            if (GodotObject.IsInstanceValid(_status)) _status.Text = VoteCopy.Progress(allow + reject, total);
            if (rotateLine && GodotObject.IsInstanceValid(_subtitle)) _subtitle.Text = VoteCopy.WaitingLine(tick / TicksPerWaitingLine);
            RebuildPips(allow, reject, total);
        });
    }

    public void LockButtons() => Deferred(() =>
    {
        foreach (var button in _buttons)
        {
            if (GodotObject.IsInstanceValid(button)) button.Disabled = true;
        }
    });

    /// <summary>Slams a DENIED stamp over the card.</summary>
    public void ShowDenied() => Deferred(() =>
    {
        if (!GodotObject.IsInstanceValid(_cardArea)) return;
        var stamp = PopupStyle.Text(VoteCopy.DeniedStamp, 64, PopupStyle.RejectRed);
        stamp.RotationDegrees = -14f;
        stamp.PivotOffset = new Vector2(110, 40);
        stamp.Position = new Vector2(40, 170);
        stamp.Scale = new Vector2(2.6f, 2.6f);
        stamp.Modulate = new Color(1f, 1f, 1f, 0f);
        _cardArea.AddChild(stamp);
        var tween = stamp.CreateTween();
        tween.SetParallel();
        tween.TweenProperty(stamp, "scale", Vector2.One, 0.18);
        tween.TweenProperty(stamp, "modulate:a", 1.0, 0.18);
        SetSubtitle(string.Empty);
    });

    public void ShowApproved(string quip) => SetSubtitle(quip);

    public void Close() => Deferred(() =>
    {
        if (_cardPreview != null && GodotObject.IsInstanceValid(_cardPreview))
        {
            if (_cardPreview.GetParent() is { } parent) parent.RemoveChild(_cardPreview);
            if (_releaseCard != null) _releaseCard(_cardPreview);
            else _cardPreview.QueueFree();
        }
        if (GodotObject.IsInstanceValid(_layer)) _layer.QueueFree();
    });

    private void SetSubtitle(string text) => Deferred(() =>
    {
        if (GodotObject.IsInstanceValid(_subtitle)) _subtitle.Text = text;
    });

    private void RebuildPips(int allow, int reject, int total)
    {
        if (!GodotObject.IsInstanceValid(_pips)) return;
        foreach (var child in _pips.GetChildren()) child.QueueFree();
        for (int i = 0; i < total; i++)
        {
            Color? color = i < allow ? PopupStyle.AllowGreen : i < allow + reject ? PopupStyle.RejectRed : null;
            _pips.AddChild(PopupStyle.Pip(color));
        }
    }

    private static void Deferred(Action action) => Callable.From(action).CallDeferred();
}
