namespace SlayTheSpireTestSubjectVote.Core.Voting;

/// <summary>
/// Version of the mod's own network messages. Sent first in every hello (8 bits). A peer on a different version
/// is not treated as having a compatible mod, so no vote is held that it could not answer.
/// Bump this whenever a message layout changes.
/// </summary>
public static class ModProtocol
{
    public const int Version = 1;

    public static bool IsCompatible(int remoteVersion) => remoteVersion == Version;
}
