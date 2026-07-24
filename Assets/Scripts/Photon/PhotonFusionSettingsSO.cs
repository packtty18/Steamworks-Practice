using Fusion.Photon.Realtime;
using Photon.Realtime;
using UnityEngine;

[CreateAssetMenu(
        fileName = "PhotonFusionSettings",
        menuName = "Snap/Network/Photon Fusion Settings")]
    public class PhotonFusionSettingsSO : ScriptableObject
    {
        [Header("Photon Application")]
        [SerializeField]
        [Tooltip("Photon Dashboard에서 생성한 Fusion 2 App ID")]
        private string appId;

        [Header("Connection")]
        [SerializeField]
        [Tooltip("비워두면 Photon이 자동으로 가장 적합한 리전을 선택합니다.")]
        private string fixedRegion = "kr";

        [SerializeField]
        [Min(1)]
        private int maxPlayers = 6;

        public string AppId => appId;
        public string FixedRegion =>fixedRegion;
        public int MaxPlayers =>  maxPlayers;
        public bool IsValid => string.IsNullOrWhiteSpace(appId) == false;


        /// <summary>
        /// SO에 저장된 설정을 Photon Fusion 전역 설정에 적용합니다.
        /// 반드시 NetworkRunner.StartGame() 이전에 호출해야 합니다.
        /// </summary>
        public void Apply()
        {
            if (IsValid == false)
            {
                Debug.LogError(
                    $"[{nameof(PhotonFusionSettingsSO)}] Photon Fusion App ID가 비어 있습니다.");

                return;
            }

            AppSettings appSettings = PhotonAppSettings.Global.AppSettings;

            appSettings.AppIdFusion = appId.Trim();
            appSettings.FixedRegion = string.IsNullOrWhiteSpace(fixedRegion)
                ? string.Empty
                : fixedRegion.Trim().ToLowerInvariant();
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
    