using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class NetworkTestPresenter : MonoBehaviour
{
    private const int DefaultMaxPlayers = 6;

    [SerializeField]
    private FusionRoomService roomService;

    [SerializeField]
    private ClipboardInviteService inviteService;

    [SerializeField]
    private NetworkTestView view;

    private bool _isReady;

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<NetworkTestView>();
        }
    }

    private void OnEnable()
    {
        if (view != null)
        {
            view.CreateRoomClicked += CreateRoom;
            view.JoinRoomClicked += JoinRoom;
            view.QuickJoinClicked += QuickJoin;
            view.LeaveRoomClicked += LeaveRoom;
            view.InviteClicked += Invite;
            view.ReadyClicked += ToggleReady;
            view.StartGameClicked += StartGame;
        }

        if (roomService != null)
        {
            roomService.ConnectionStateChanged += OnConnectionStateChanged;
            roomService.RoomListChanged += OnRoomListChanged;
            roomService.MessageReceived += OnMessageReceived;
            roomService.HostClosedRoom += OnHostClosedRoom;
        }
    }

    private void OnDisable()
    {
        if (view != null)
        {
            view.CreateRoomClicked -= CreateRoom;
            view.JoinRoomClicked -= JoinRoom;
            view.QuickJoinClicked -= QuickJoin;
            view.LeaveRoomClicked -= LeaveRoom;
            view.InviteClicked -= Invite;
            view.ReadyClicked -= ToggleReady;
            view.StartGameClicked -= StartGame;
        }

        if (roomService != null)
        {
            roomService.ConnectionStateChanged -= OnConnectionStateChanged;
            roomService.RoomListChanged -= OnRoomListChanged;
            roomService.MessageReceived -= OnMessageReceived;
            roomService.HostClosedRoom -= OnHostClosedRoom;
        }
    }

    private void CreateRoom()
    {
        CreateRoomAsync().Forget();
    }

    private void JoinRoom()
    {
        JoinRoomAsync().Forget();
    }

    private void QuickJoin()
    {
        QuickJoinAsync().Forget();
    }

    private void LeaveRoom()
    {
        LeaveRoomAsync().Forget();
    }

    private async UniTaskVoid CreateRoomAsync()
    {
        try
        {
            view.SetInteractable(false);

            RoomCreateOptions options = new RoomCreateOptions(
                view.RoomName,
                view.GetMaxPlayers(DefaultMaxPlayers),
                view.Region,
                view.IsPrivate,
                view.Password);

            RoomJoinResult result = await roomService.CreateRoomAsync(
                options,
                this.GetCancellationTokenOnDestroy());

            ReportResult(result);
        }
        catch (Exception exception)
        {
            view.AppendLog(exception.Message);
        }
        finally
        {
            view.SetInteractable(true);
        }
    }

    private async UniTaskVoid JoinRoomAsync()
    {
        try
        {
            view.SetInteractable(false);

            RoomJoinOptions options = new RoomJoinOptions(
                view.RoomName,
                view.Region,
                view.Password);

            RoomJoinResult result = await roomService.JoinRoomAsync(
                options,
                this.GetCancellationTokenOnDestroy());

            ReportResult(result);
        }
        catch (Exception exception)
        {
            view.AppendLog(exception.Message);
        }
        finally
        {
            view.SetInteractable(true);
        }
    }

    private async UniTaskVoid QuickJoinAsync()
    {
        try
        {
            view.SetInteractable(false);

            RoomJoinResult result = await roomService.QuickJoinAsync(
                view.Region,
                this.GetCancellationTokenOnDestroy());

            ReportResult(result);
        }
        catch (Exception exception)
        {
            view.AppendLog(exception.Message);
        }
        finally
        {
            view.SetInteractable(true);
        }
    }

    private async UniTaskVoid LeaveRoomAsync()
    {
        await roomService.LeaveRoomAsync(this.GetCancellationTokenOnDestroy());
    }

    private void Invite()
    {
        if (inviteService == null)
        {
            return;
        }

        RoomJoinOptions options = new RoomJoinOptions(
            view.RoomName,
            view.Region,
            view.Password);

        inviteService.Invite(options);
    }

    private void ToggleReady()
    {
        _isReady = !_isReady;
        roomService.SetLocalReady(_isReady);
        view.AppendLog(_isReady ? "준비 완료" : "준비 취소");
    }

    private void StartGame()
    {
        roomService.StartGame();
    }

    private void ReportResult(RoomJoinResult result)
    {
        if (result == null)
        {
            return;
        }

        view.AppendLog(result.Message);
    }

    private void OnConnectionStateChanged(NetworkConnectionState connectionState)
    {
        view.SetStatus(connectionState.ToString());
    }

    private void OnRoomListChanged(System.Collections.Generic.IReadOnlyList<RoomSummary> rooms)
    {
        view.SetRoomList(rooms);
    }

    private void OnMessageReceived(string message)
    {
        view.AppendLog(message);
    }

    private void OnHostClosedRoom()
    {
        view.SetStatus("호스트가 방을 종료했습니다.");
        view.AppendLog("호스트가 나가서 방이 종료되었습니다. 팝업 UI를 연결할 지점입니다.");
    }
}
