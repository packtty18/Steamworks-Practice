using System;
using Fusion;

public sealed class RoomSummary
{
    public string Name { get; }
    public int PlayerCount { get; }
    public int MaxPlayers { get; }
    public bool IsOpen { get; }
    public bool IsVisible { get; }
    public bool IsPrivate { get; }
    public string Region { get; }
    public bool CanJoin => IsOpen && IsVisible && IsPrivate == false && PlayerCount < MaxPlayers;

    private RoomSummary(
        string name,
        int playerCount,
        int maxPlayers,
        bool isOpen,
        bool isVisible,
        bool isPrivate,
        string region)
    {
        Name = name;
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
        IsOpen = isOpen;
        IsVisible = isVisible;
        IsPrivate = isPrivate;
        Region = region;
    }

    public static RoomSummary FromSessionInfo(SessionInfo sessionInfo)
    {
        if (sessionInfo == null)
        {
            throw new ArgumentNullException(nameof(sessionInfo));
        }

        bool isPrivate = TryGetIntProperty(
            sessionInfo,
            RoomSessionPropertyKeys.IsPrivate,
            out int privateValue) && privateValue == 1;

        string region = TryGetStringProperty(
            sessionInfo,
            RoomSessionPropertyKeys.Region,
            out string regionValue)
            ? regionValue
            : string.Empty;

        return new RoomSummary(
            sessionInfo.Name,
            sessionInfo.PlayerCount,
            sessionInfo.MaxPlayers,
            sessionInfo.IsOpen,
            sessionInfo.IsVisible,
            isPrivate,
            region);
    }

    private static bool TryGetIntProperty(
        SessionInfo sessionInfo,
        string key,
        out int value)
    {
        value = default;

        if (sessionInfo.Properties == null)
        {
            return false;
        }

        if (sessionInfo.Properties.TryGetValue(key, out SessionProperty property) == false)
        {
            return false;
        }

        if (property.PropertyValue is int intValue)
        {
            value = intValue;
            return true;
        }

        return false;
    }

    private static bool TryGetStringProperty(
        SessionInfo sessionInfo,
        string key,
        out string value)
    {
        value = string.Empty;

        if (sessionInfo.Properties == null)
        {
            return false;
        }

        if (sessionInfo.Properties.TryGetValue(key, out SessionProperty property) == false)
        {
            return false;
        }

        if (property.PropertyValue is string stringValue)
        {
            value = stringValue;
            return true;
        }

        return false;
    }
}
