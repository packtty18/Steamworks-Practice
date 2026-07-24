using System;
using System.Collections.Generic;
using Fusion;

public sealed class NetworkPlayerRegistry
{
    private readonly Dictionary<PlayerRef, NetworkPlayerProfile> _profiles = new();

    public IReadOnlyCollection<NetworkPlayerProfile> Profiles => _profiles.Values;

    public event Action Changed;

    public void Register(
        PlayerRef playerRef,
        string userId,
        string nickname,
        bool isLocal,
        bool isHost)
    {
        _profiles[playerRef] = new NetworkPlayerProfile(
            playerRef,
            userId,
            nickname,
            isLocal,
            isHost);

        Changed?.Invoke();
    }

    public void Remove(PlayerRef playerRef)
    {
        if (_profiles.Remove(playerRef))
        {
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        if (_profiles.Count == 0)
        {
            return;
        }

        _profiles.Clear();
        Changed?.Invoke();
    }
}
