using System;
using Steamworks;
using UnityEngine;

public class SteamManager : MonoBehaviour
{
    private const uint TestAppId = 480;

    private void Awake()
    {
        try
        {
            SteamClient.Init(TestAppId);

            Debug.Log($"Steam 초기화 성공");
            Debug.Log($"AppID: {SteamClient.AppId}");
            Debug.Log($"SteamID: {SteamClient.SteamId}");
            Debug.Log($"사용자 이름: {SteamClient.Name}");
            Debug.Log($"서버 연결 상태: {SteamClient.IsLoggedOn}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Steam 초기화 실패: {exception}");
        }
    }

    private void OnApplicationQuit()
    {
        SteamClient.Shutdown();
    }
}

