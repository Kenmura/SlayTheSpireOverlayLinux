namespace SlayTheSpireTestSubjectVote.Core.Voting;

/// <summary>All player-facing text, kept pure so the tone and the runtime Strength number are unit-tested.</summary>
public static class VoteCopy
{
    private static readonly string[] Waiting =
    [
        "Your teammates are deciding your fate...",
        "The jury is out. Try to look innocent.",
        "Somewhere, a thumb hovers...",
    ];

    private static readonly string[] Quips =
    [
        "Bold choice...",
        "The people have spoken. Mostly yes.",
        "Well, it's your funeral.",
    ];

    public static string AllowLabel => "Let it happen";

    public static string RejectLabel => "Absolutely not";

    public static string DeniedStamp => "DENIED";

    public static string Headline(string playerName, string cardName) =>
        $"{playerName} wants to play {cardName}. Are we feeling lucky?";

    public static string CostLine(int strength) =>
        $"The Test Subject will get {strength} Strength. Hope you like pain.";

    public static string WaitingLine(int tick) => Pick(Waiting, tick);

    public static string ApprovedQuip(int pick) => Pick(Quips, pick);

    public static string Progress(int votesIn, int total) => $"{votesIn} of {total} votes in";

    private static string Pick(string[] lines, int index) => lines[((index % lines.Length) + lines.Length) % lines.Length];
}
