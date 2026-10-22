using Godot;
using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireTestSubjectVote.Godot.UI;

/// <summary>Handle to a pop-up that is on screen. Every mutation is deferred onto the main loop and guarded by IsInstanceValid.</summary>
public sealed class VotePopupView
{
    private readonly CanvasLayer _layer;
    private readonly Label _status;
    private readonly Button[] _buttons;

    internal VotePopupView(CanvasLayer layer, Label status, params Button[] buttons)
    {
        _layer = layer;
        _status = status;
        _buttons = buttons;
    }

    public void Update(VoteTally tally) =>
        SetStatus(VoteCopy.Progress(tally.Allow + tally.Reject, tally.Total));

    public void LockButtons() => Deferred(() =>
    {
        foreach (var button in _buttons)
        {
            if (GodotObject.IsInstanceValid(button)) button.Disabled = true;
        }
    });

    public void ShowDenied() => SetStatus(VoteCopy.DeniedStamp);

    public void ShowApproved(string quip) => SetStatus(quip);

    public void Close() => Deferred(() =>
    {
        if (GodotObject.IsInstanceValid(_layer)) _layer.QueueFree();
    });

    private void SetStatus(string text) => Deferred(() =>
    {
        if (GodotObject.IsInstanceValid(_status)) _status.Text = text;
    });

    private static void Deferred(Action action) => Callable.From(action).CallDeferred();
}
