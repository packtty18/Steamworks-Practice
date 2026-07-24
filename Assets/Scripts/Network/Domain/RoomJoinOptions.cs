using System;

public sealed class RoomJoinOptions
{
    public string RoomName { get; }
    public string Region { get; }
    public string Password { get; }

    public RoomJoinOptions(
        string roomName,
        string region,
        string password)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            throw new ArgumentException("방 이름이 비어 있을 수 없습니다.", nameof(roomName));
        }

        RoomName = roomName.Trim();
        Region = string.IsNullOrWhiteSpace(region) ? string.Empty : region.Trim();
        Password = string.IsNullOrWhiteSpace(password) ? string.Empty : password.Trim();
    }
}
