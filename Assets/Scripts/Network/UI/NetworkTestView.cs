using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class NetworkTestView : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private InputField roomNameInput;

    [SerializeField]
    private InputField maxPlayersInput;

    [SerializeField]
    private InputField regionInput;

    [SerializeField]
    private Toggle privateRoomToggle;

    [SerializeField]
    private InputField passwordInput;

    [Header("Buttons")]
    [SerializeField]
    private Button createRoomButton;

    [SerializeField]
    private Button joinRoomButton;

    [SerializeField]
    private Button quickJoinButton;

    [SerializeField]
    private Button leaveRoomButton;

    [SerializeField]
    private Button inviteButton;

    [SerializeField]
    private Button readyButton;

    [SerializeField]
    private Button startGameButton;

    [Header("Output")]
    [SerializeField]
    private Text statusText;

    [SerializeField]
    private Text roomListText;

    [SerializeField]
    private Text logText;

    public event Action CreateRoomClicked;
    public event Action JoinRoomClicked;
    public event Action QuickJoinClicked;
    public event Action LeaveRoomClicked;
    public event Action InviteClicked;
    public event Action ReadyClicked;
    public event Action StartGameClicked;

    public string RoomName => roomNameInput == null ? string.Empty : roomNameInput.text;
    public string Region => regionInput == null ? string.Empty : regionInput.text;
    public string Password => passwordInput == null ? string.Empty : passwordInput.text;
    public bool IsPrivate => privateRoomToggle != null && privateRoomToggle.isOn;

    private void Awake()
    {
        Bind(createRoomButton, () => CreateRoomClicked?.Invoke());
        Bind(joinRoomButton, () => JoinRoomClicked?.Invoke());
        Bind(quickJoinButton, () => QuickJoinClicked?.Invoke());
        Bind(leaveRoomButton, () => LeaveRoomClicked?.Invoke());
        Bind(inviteButton, () => InviteClicked?.Invoke());
        Bind(readyButton, () => ReadyClicked?.Invoke());
        Bind(startGameButton, () => StartGameClicked?.Invoke());
    }

    public int GetMaxPlayers(int fallbackValue)
    {
        if (maxPlayersInput == null)
        {
            return fallbackValue;
        }

        if (int.TryParse(maxPlayersInput.text, out int maxPlayers))
        {
            return maxPlayers;
        }

        return fallbackValue;
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    public void SetRoomList(IReadOnlyList<RoomSummary> rooms)
    {
        if (roomListText == null)
        {
            return;
        }

        if (rooms == null || rooms.Count == 0)
        {
            roomListText.text = "공개방 없음";
            return;
        }

        StringBuilder builder = new StringBuilder();

        foreach (RoomSummary room in rooms)
        {
            builder.Append(room.Name);
            builder.Append(" | ");
            builder.Append(room.PlayerCount);
            builder.Append("/");
            builder.Append(room.MaxPlayers);
            builder.Append(" | ");
            builder.Append(room.CanJoin ? "Joinable" : "Locked");
            builder.AppendLine();
        }

        roomListText.text = builder.ToString();
    }

    public void AppendLog(string message)
    {
        if (logText == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        logText.text = $"{logText.text}\n{message}";
    }

    public void SetInteractable(bool isInteractable)
    {
        SetInteractable(createRoomButton, isInteractable);
        SetInteractable(joinRoomButton, isInteractable);
        SetInteractable(quickJoinButton, isInteractable);
    }

    private void Bind(Button button, Action action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.AddListener(() => action?.Invoke());
    }

    private void SetInteractable(Button button, bool isInteractable)
    {
        if (button != null)
        {
            button.interactable = isInteractable;
        }
    }
}
