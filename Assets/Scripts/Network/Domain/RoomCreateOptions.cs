using System;

public sealed class RoomCreateOptions
{
    private const int MinimumPlayerCount = 1;
    private const int MaximumPlayerCount = 255;

    public string RoomName { get; }
    public int MaxPlayers { get; }
    public string Region { get; }
    public bool IsPrivate { get; }
    public string Password { get; }

    public RoomCreateOptions(
        string roomName,
        int maxPlayers,
        string region,
        bool isPrivate,
        string password)
    {
        RoomName = NormalizeRequiredText(roomName, nameof(roomName));
        MaxPlayers = ClampPlayerCount(maxPlayers);
        Region = NormalizeOptionalText(region);
        IsPrivate = isPrivate;
        Password = NormalizeOptionalText(password);

        if (IsPrivate && string.IsNullOrWhiteSpace(Password))
        {
            throw new ArgumentException("비밀방은 패스워드가 필요합니다.", nameof(password));
        }
    }

    private static string NormalizeRequiredText(string text, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("값이 비어 있을 수 없습니다.", parameterName);
        }

        return text.Trim();
    }

    private static string NormalizeOptionalText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Trim();
    }

    private static int ClampPlayerCount(int playerCount)
    {
        if (playerCount < MinimumPlayerCount)
        {
            return MinimumPlayerCount;
        }

        if (playerCount > MaximumPlayerCount)
        {
            return MaximumPlayerCount;
        }

        return playerCount;
    }
}
