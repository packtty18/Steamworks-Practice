using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;

public interface IRoomService
{
    NetworkRunner Runner { get; }
    NetworkConnectionState ConnectionState { get; }
    IReadOnlyList<RoomSummary> CachedRooms { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }

    event Action<NetworkConnectionState> ConnectionStateChanged;
    event Action<IReadOnlyList<RoomSummary>> RoomListChanged;
    event Action<string> MessageReceived;
    event Action HostClosedRoom;

    UniTask<RoomJoinResult> CreateRoomAsync(
        RoomCreateOptions options,
        CancellationToken cancellationToken = default);

    UniTask<RoomJoinResult> JoinRoomAsync(
        RoomJoinOptions options,
        CancellationToken cancellationToken = default);

    UniTask<RoomJoinResult> QuickJoinAsync(
        string region,
        CancellationToken cancellationToken = default);

    UniTask LeaveRoomAsync(CancellationToken cancellationToken = default);

    void SetLocalReady(bool isReady);
    bool CanHostStartGame();
    void StartGame();
}
