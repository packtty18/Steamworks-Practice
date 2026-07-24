using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FusionRoomService : MonoBehaviour, IRoomService, INetworkRunnerCallbacks
{
    private const string LocalUserIdPrefix = "local-";

    [Header("Fusion")]
    [SerializeField]
    private NetworkRunner runnerPrefab;

    [SerializeField]
    private bool provideInput = true;

    [Header("Scene")]
    [SerializeField]
    private int roomSceneBuildIndex = -1;

    [SerializeField]
    private int gameSceneBuildIndex = -1;

    [Header("Player")]
    [SerializeField]
    private NetworkRoomPlayer roomPlayerPrefab;

    [Header("Settings")]
    [SerializeField]
    private PhotonFusionSettingsSO photonSettings;

    private readonly List<RoomSummary> _cachedRooms = new();
    private readonly Dictionary<PlayerRef, NetworkRoomPlayer> _roomPlayers = new();
    private readonly NetworkPlayerRegistry _playerRegistry = new();

    private FusionRunnerFactory _runnerFactory;
    private NetworkRunner _runner;
    private CancellationTokenSource _connectionCancellationTokenSource;
    private RoomCreateOptions _currentCreateOptions;
    private NetworkConnectionState _connectionState = NetworkConnectionState.Idle;

    public NetworkRunner Runner => _runner;
    public NetworkConnectionState ConnectionState => _connectionState;
    public IReadOnlyList<RoomSummary> CachedRooms => _cachedRooms;
    public NetworkPlayerRegistry PlayerRegistry => _playerRegistry;

    public event Action<NetworkConnectionState> ConnectionStateChanged;
    public event Action<IReadOnlyList<RoomSummary>> RoomListChanged;
    public event Action<string> MessageReceived;
    public event Action HostClosedRoom;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _runnerFactory = new FusionRunnerFactory(runnerPrefab, provideInput);
    }

    private void OnDestroy()
    {
        CancelConnection();
    }

    public async UniTask<RoomJoinResult> CreateRoomAsync(
        RoomCreateOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _currentCreateOptions = options;

        StartGameArgs args = CreateStartGameArgs(
            GameMode.Host,
            options.RoomName,
            options.Region,
            options.MaxPlayers,
            options.IsPrivate == false,
            null);

        args.SessionProperties = CreateSessionProperties(options);

        return await StartRunnerAsync(args, cancellationToken);
    }

    public async UniTask<RoomJoinResult> JoinRoomAsync(
        RoomJoinOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        StartGameArgs args = CreateStartGameArgs(
            GameMode.Client,
            options.RoomName,
            options.Region,
            default,
            true,
            options.Password);

        args.EnableClientSessionCreation = false;

        return await StartRunnerAsync(args, cancellationToken);
    }

    public async UniTask<RoomJoinResult> QuickJoinAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        RoomSummary targetRoom = _cachedRooms
            .Where(room => room.CanJoin)
            .OrderByDescending(room => room.PlayerCount)
            .FirstOrDefault();

        if (targetRoom != null)
        {
            RoomJoinOptions joinOptions = new RoomJoinOptions(
                targetRoom.Name,
                region,
                string.Empty);

            return await JoinRoomAsync(joinOptions, cancellationToken);
        }

        StartGameArgs args = CreateStartGameArgs(
            GameMode.Client,
            null,
            region,
            default,
            true,
            null);

        args.EnableClientSessionCreation = false;
        args.MatchmakingMode = MatchmakingMode.FillRoom;
        args.SessionProperties = new Dictionary<string, SessionProperty>
        {
            { RoomSessionPropertyKeys.IsPrivate, 0 }
        };

        return await StartRunnerAsync(args, cancellationToken);
    }

    public async UniTask LeaveRoomAsync(CancellationToken cancellationToken = default)
    {
        CancelConnection();

        if (_runner == null)
        {
            SetConnectionState(NetworkConnectionState.Idle);
            return;
        }

        SetConnectionState(NetworkConnectionState.Leaving);

        NetworkRunner runnerToShutdown = _runner;
        _runner = null;

        try
        {
            if (runnerToShutdown.IsRunning)
            {
                await runnerToShutdown
                    .Shutdown()
                    .AsUniTask()
                    .AttachExternalCancellation(cancellationToken);
            }
        }
        finally
        {
            ClearRuntimeState();
            _runnerFactory.Destroy(runnerToShutdown, this);
            SetConnectionState(NetworkConnectionState.Idle);
        }
    }

    public void SetLocalReady(bool isReady)
    {
        if (_runner == null)
        {
            return;
        }

        if (TryGetLocalRoomPlayer(out NetworkRoomPlayer localPlayer) == false)
        {
            return;
        }

        localPlayer.SetReady(isReady);
    }

    public bool CanHostStartGame()
    {
        if (_runner == null || _runner.IsServer == false)
        {
            return false;
        }

        if (_roomPlayers.Count == 0)
        {
            return false;
        }

        foreach (NetworkRoomPlayer roomPlayer in _roomPlayers.Values)
        {
            if (roomPlayer == null || roomPlayer.IsReady == false)
            {
                return false;
            }
        }

        return true;
    }

    public void StartGame()
    {
        if (CanHostStartGame() == false)
        {
            PublishMessage("모든 플레이어가 준비되어야 게임을 시작할 수 있습니다.");
            return;
        }

        if (gameSceneBuildIndex < 0)
        {
            PublishMessage("게임 씬 Build Index가 설정되지 않았습니다.");
            return;
        }

        SetConnectionState(NetworkConnectionState.LoadingGame);
        _runner.LoadScene(SceneRef.FromIndex(gameSceneBuildIndex), LoadSceneMode.Single);
    }

    private async UniTask<RoomJoinResult> StartRunnerAsync(
        StartGameArgs startGameArgs,
        CancellationToken externalCancellationToken)
    {
        if (_connectionState != NetworkConnectionState.Idle)
        {
            return RoomJoinResult.Fail("이미 연결 중이거나 방에 참가한 상태입니다.", ShutdownReason.Error);
        }

        ApplyPhotonSettings(startGameArgs.CustomLobbyName);
        SetConnectionState(NetworkConnectionState.Connecting);

        _connectionCancellationTokenSource?.Cancel();
        _connectionCancellationTokenSource?.Dispose();
        _connectionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            externalCancellationToken,
            this.GetCancellationTokenOnDestroy());

        NetworkRunner createdRunner = null;

        try
        {
            createdRunner = _runnerFactory.Create(this);
            _runner = createdRunner;
            startGameArgs.SceneManager = createdRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            startGameArgs.StartGameCancellationToken = _connectionCancellationTokenSource.Token;

            StartGameResult result = await createdRunner
                .StartGame(startGameArgs)
                .AsUniTask()
                .AttachExternalCancellation(_connectionCancellationTokenSource.Token);

            if (result.Ok)
            {
                SetConnectionState(NetworkConnectionState.InRoom);
                return RoomJoinResult.Success("방 참가에 성공했습니다.");
            }

            await ShutdownFailedRunnerAsync(createdRunner);
            SetConnectionState(NetworkConnectionState.Idle);

            return RoomJoinResult.Fail("방 참가에 실패했습니다.", result.ShutdownReason);
        }
        catch (OperationCanceledException)
        {
            if (createdRunner != null)
            {
                await ShutdownFailedRunnerAsync(createdRunner);
            }

            SetConnectionState(NetworkConnectionState.Idle);
            throw;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            if (createdRunner != null)
            {
                await ShutdownFailedRunnerAsync(createdRunner);
            }

            SetConnectionState(NetworkConnectionState.Idle);
            return RoomJoinResult.Fail(exception.Message, ShutdownReason.Error);
        }
    }

    private StartGameArgs CreateStartGameArgs(
        GameMode gameMode,
        string roomName,
        string region,
        int maxPlayers,
        bool isVisible,
        string password)
    {
        NetworkSceneInfo sceneInfo = CreateInitialSceneInfo();

        StartGameArgs args = new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = roomName,
            Scene = sceneInfo,
            IsOpen = true,
            IsVisible = isVisible,
            CustomLobbyName = NormalizeRegion(region),
            ConnectionToken = CreateConnectionToken(password)
        };

        if (maxPlayers > 0)
        {
            args.PlayerCount = maxPlayers;
        }

        return args;
    }

    private NetworkSceneInfo CreateInitialSceneInfo()
    {
        int buildIndex = roomSceneBuildIndex >= 0
            ? roomSceneBuildIndex
            : SceneManager.GetActiveScene().buildIndex;

        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);
        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();

        if (sceneRef.IsValid)
        {
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);
        }

        return sceneInfo;
    }

    private Dictionary<string, SessionProperty> CreateSessionProperties(RoomCreateOptions options)
    {
        return new Dictionary<string, SessionProperty>
        {
            { RoomSessionPropertyKeys.IsPrivate, options.IsPrivate ? 1 : 0 },
            { RoomSessionPropertyKeys.Region, NormalizeRegion(options.Region) }
        };
    }

    private byte[] CreateConnectionToken(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return Encoding.UTF8.GetBytes(password.Trim());
    }

    private void ApplyPhotonSettings(string region)
    {
        if (photonSettings == null)
        {
            return;
        }

        photonSettings.Apply(region);
    }

    private string NormalizeRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return photonSettings == null ? string.Empty : photonSettings.FixedRegion;
        }

        return region.Trim().ToLowerInvariant();
    }

    private bool IsPasswordAccepted(byte[] token)
    {
        if (_currentCreateOptions == null || _currentCreateOptions.IsPrivate == false)
        {
            return true;
        }

        string password = token == null || token.Length == 0
            ? string.Empty
            : Encoding.UTF8.GetString(token);

        return string.Equals(
            _currentCreateOptions.Password,
            password,
            StringComparison.Ordinal);
    }

    private Vector3 CreateRoomPlayerSpawnPosition(PlayerRef player)
    {
        const float spacing = 2f;
        int playerIndex = Mathf.Max(0, player.PlayerId - 1);
        return new Vector3(playerIndex * spacing, 0f, 0f);
    }

    private bool TryGetLocalRoomPlayer(out NetworkRoomPlayer localPlayer)
    {
        localPlayer = null;

        if (_runner == null)
        {
            return false;
        }

        if (_roomPlayers.TryGetValue(_runner.LocalPlayer, out localPlayer) && localPlayer != null)
        {
            return true;
        }

        NetworkRoomPlayer[] roomPlayers = FindObjectsByType<NetworkRoomPlayer>(FindObjectsSortMode.None);

        foreach (NetworkRoomPlayer roomPlayer in roomPlayers)
        {
            if (roomPlayer == null || roomPlayer.Object == null)
            {
                continue;
            }

            if (roomPlayer.Object.HasInputAuthority == false)
            {
                continue;
            }

            localPlayer = roomPlayer;
            _roomPlayers[_runner.LocalPlayer] = roomPlayer;
            return true;
        }

        return false;
    }

    private async UniTask ShutdownFailedRunnerAsync(NetworkRunner failedRunner)
    {
        try
        {
            if (failedRunner != null && failedRunner.IsRunning)
            {
                await failedRunner.Shutdown().AsUniTask();
            }
        }
        finally
        {
            if (_runner == failedRunner)
            {
                _runner = null;
            }

            _runnerFactory.Destroy(failedRunner, this);
            ClearRuntimeState();
        }
    }

    private void CancelConnection()
    {
        if (_connectionCancellationTokenSource == null)
        {
            return;
        }

        _connectionCancellationTokenSource.Cancel();
        _connectionCancellationTokenSource.Dispose();
        _connectionCancellationTokenSource = null;
    }

    private void ClearRuntimeState()
    {
        _currentCreateOptions = null;
        _roomPlayers.Clear();
        _playerRegistry.Clear();
    }

    private void SetConnectionState(NetworkConnectionState connectionState)
    {
        if (_connectionState == connectionState)
        {
            return;
        }

        _connectionState = connectionState;
        ConnectionStateChanged?.Invoke(connectionState);
    }

    private void PublishMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.Log($"[FusionRoomService] {message}");
        MessageReceived?.Invoke(message);
    }

    public void OnConnectedToServer(NetworkRunner callbackRunner)
    {
        if (callbackRunner != _runner)
        {
            return;
        }

        PublishMessage("서버에 연결되었습니다.");
    }

    public void OnPlayerJoined(NetworkRunner callbackRunner, PlayerRef player)
    {
        if (callbackRunner != _runner)
        {
            return;
        }

        bool isLocal = player == callbackRunner.LocalPlayer;
        bool isHost = callbackRunner.IsServer && player == callbackRunner.LocalPlayer;

        _playerRegistry.Register(
            player,
            $"{LocalUserIdPrefix}{player.PlayerId}",
            $"Player {player.PlayerId}",
            isLocal,
            isHost);

        if (callbackRunner.IsServer && roomPlayerPrefab != null)
        {
            NetworkRoomPlayer roomPlayer = callbackRunner.Spawn(
                roomPlayerPrefab,
                CreateRoomPlayerSpawnPosition(player),
                Quaternion.identity,
                player);

            _roomPlayers[player] = roomPlayer;
        }

        PublishMessage($"플레이어가 참가했습니다. PlayerRef={player}");
    }

    public void OnPlayerLeft(NetworkRunner callbackRunner, PlayerRef player)
    {
        if (callbackRunner != _runner)
        {
            return;
        }

        _playerRegistry.Remove(player);

        if (callbackRunner.IsServer && _roomPlayers.TryGetValue(player, out NetworkRoomPlayer roomPlayer))
        {
            if (roomPlayer != null)
            {
                callbackRunner.Despawn(roomPlayer.Object);
            }

            _roomPlayers.Remove(player);
        }

        PublishMessage($"플레이어가 나갔습니다. PlayerRef={player}");
    }

    public void OnSessionListUpdated(NetworkRunner callbackRunner, List<SessionInfo> sessionList)
    {
        if (callbackRunner != _runner)
        {
            return;
        }

        _cachedRooms.Clear();

        foreach (SessionInfo sessionInfo in sessionList)
        {
            RoomSummary roomSummary = RoomSummary.FromSessionInfo(sessionInfo);
            _cachedRooms.Add(roomSummary);
        }

        RoomListChanged?.Invoke(_cachedRooms);
    }

    public void OnShutdown(NetworkRunner callbackRunner, ShutdownReason shutdownReason)
    {
        bool wasActiveRunner = callbackRunner == _runner;

        if (wasActiveRunner)
        {
            _runner = null;
            SetConnectionState(NetworkConnectionState.Idle);
        }

        ClearRuntimeState();
        _runnerFactory.Destroy(callbackRunner, this);
        PublishMessage($"Runner가 종료되었습니다. Reason={shutdownReason}");
    }

    public void OnDisconnectedFromServer(NetworkRunner callbackRunner, NetDisconnectReason reason)
    {
        if (callbackRunner != _runner)
        {
            return;
        }

        HostClosedRoom?.Invoke();
        PublishMessage($"호스트 연결이 끊어졌습니다. Reason={reason}");
    }

    public void OnConnectRequest(
        NetworkRunner callbackRunner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
        if (IsPasswordAccepted(token))
        {
            request.Accept();
            return;
        }

        request.Refuse();
    }

    public void OnConnectFailed(
        NetworkRunner callbackRunner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        PublishMessage($"연결 실패: {reason}, Address={remoteAddress}");
    }

    public void OnSceneLoadStart(NetworkRunner callbackRunner)
    {
        PublishMessage("네트워크 씬 로딩을 시작합니다.");
    }

    public void OnSceneLoadDone(NetworkRunner callbackRunner)
    {
        if (_connectionState == NetworkConnectionState.LoadingGame)
        {
            SetConnectionState(NetworkConnectionState.InGame);
        }

        PublishMessage("네트워크 씬 로딩이 완료되었습니다.");
    }

    public void OnInput(NetworkRunner callbackRunner, NetworkInput input)
    {
        RoomPlayerInputData inputData = new RoomPlayerInputData();

        inputData.SetForward(Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow));
        inputData.SetBackward(Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow));
        inputData.SetLeft(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow));
        inputData.SetRight(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow));

        input.Set(inputData);
    }

    public void OnInputMissing(NetworkRunner callbackRunner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner callbackRunner, NetworkObject networkObject, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner callbackRunner, NetworkObject networkObject, PlayerRef player)
    {
    }

    public void OnReliableDataReceived(
        NetworkRunner callbackRunner,
        PlayerRef player,
        ReliableKey key,
        ArraySegment<byte> data)
    {
    }

    public void OnReliableDataReceived(
        NetworkRunner callbackRunner,
        PlayerRef player,
        ReliableKey key,
        ReadOnlySpan<byte> data)
    {
    }

    public void OnReliableDataProgress(
        NetworkRunner callbackRunner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner callbackRunner, SimulationMessagePtr message)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner callbackRunner, Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(NetworkRunner callbackRunner, HostMigrationToken hostMigrationToken)
    {
    }
}
