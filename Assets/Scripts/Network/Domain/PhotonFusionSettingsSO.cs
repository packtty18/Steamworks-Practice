using Fusion.Photon.Realtime;
using Photon.Realtime;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PhotonFusionSettings",
    menuName = "Snap/Network/Photon Fusion Settings")]
public sealed class PhotonFusionSettingsSO : ScriptableObject
{
    [Header("Connection")]
    [SerializeField]
    [Tooltip("비워두면 Photon이 적절한 Region을 자동 선택합니다.")]
    private string fixedRegion = "kr";

    [SerializeField]
    [Min(1)]
    private int maxPlayers = 6;

    public string AppId => PhotonAppSettings.Global.AppSettings.AppIdFusion;
    public string FixedRegion => fixedRegion;
    public int MaxPlayers => maxPlayers;
    public bool IsValid => string.IsNullOrWhiteSpace(AppId) == false;

    public void Apply()
    {
        Apply(fixedRegion);
    }

    public void Apply(string regionOverride)
    {
        if (IsValid == false)
        {
            Debug.LogError(
                $"[{nameof(PhotonFusionSettingsSO)}] " +
                "PhotonAppSettings에 Fusion App ID가 비어 있습니다.");

            return;
        }

        AppSettings appSettings = PhotonAppSettings.Global.AppSettings;

        appSettings.FixedRegion = string.IsNullOrWhiteSpace(regionOverride)
            ? string.Empty
            : regionOverride.Trim().ToLowerInvariant();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxPlayers = Mathf.Max(1, maxPlayers);

        if (fixedRegion != null)
        {
            fixedRegion = fixedRegion.Trim().ToLowerInvariant();
        }
    }
#endif
}
