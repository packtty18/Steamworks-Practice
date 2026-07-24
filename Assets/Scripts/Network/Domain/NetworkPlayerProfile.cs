using Fusion;

public sealed class NetworkPlayerProfile
{
    public PlayerRef PlayerRef { get; }
    public string UserId { get; }
    public string Nickname { get; }
    public bool IsLocal { get; }
    public bool IsHost { get; }

    public NetworkPlayerProfile(
        PlayerRef playerRef,
        string userId,
        string nickname,
        bool isLocal,
        bool isHost)
    {
        PlayerRef = playerRef;
        UserId = userId;
        Nickname = string.IsNullOrWhiteSpace(nickname) ? "Player" : nickname.Trim();
        IsLocal = isLocal;
        IsHost = isHost;
    }
}
