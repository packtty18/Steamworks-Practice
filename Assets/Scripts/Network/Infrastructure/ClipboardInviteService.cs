using UnityEngine;

public sealed class ClipboardInviteService : MonoBehaviour, IInviteService
{
    public void Invite(RoomJoinOptions options)
    {
        if (options == null)
        {
            return;
        }

        string passwordPart = string.IsNullOrWhiteSpace(options.Password)
            ? string.Empty
            : $" password={options.Password}";

        GUIUtility.systemCopyBuffer =
            $"room={options.RoomName} region={options.Region}{passwordPart}";

        Debug.Log($"[Invite] 초대 정보가 클립보드에 복사되었습니다. Room={options.RoomName}");
    }
}
