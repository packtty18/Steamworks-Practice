#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Editor;
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NetworkSceneBootstrapper
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string NetworkPrefabFolder = "Assets/Prefabs/Network";
    private const string RunnerPrefabPath = "Assets/Prefabs/Network/NetworkRunner.prefab";
    private const string RoomPlayerPrefabPath = "Assets/Prefabs/Network/NetworkRoomPlayer.prefab";
    private const string SettingsFolder = "Assets/ScriptableObjects/Network";
    private const string SettingsAssetPath = "Assets/ScriptableObjects/Network/PhotonFusionSettings.asset";

    [MenuItem("Tools/Snap/Network/Setup Test Scene")]
    public static void SetupTestScene()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(NetworkPrefabFolder);
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder(SettingsFolder);

        PhotonFusionSettingsSO settings = CreateOrLoadSettings();
        NetworkRunner runnerPrefab = CreateOrLoadRunnerPrefab();
        NetworkRoomPlayer roomPlayerPrefab = CreateOrLoadRoomPlayerPrefab();

        CreateOrUpdateGameScene();
        CreateOrUpdateSampleScene(settings, runnerPrefab, roomPlayerPrefab);
        UpdateBuildSettings();
        NetworkProjectConfigUtilities.RebuildPrefabTable();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NetworkSceneBootstrapper] Network test scene setup completed.");
    }

    private static PhotonFusionSettingsSO CreateOrLoadSettings()
    {
        PhotonFusionSettingsSO settings =
            AssetDatabase.LoadAssetAtPath<PhotonFusionSettingsSO>(SettingsAssetPath);

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<PhotonFusionSettingsSO>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
        }

        SerializedObject serializedObject = new SerializedObject(settings);
        serializedObject.FindProperty("fixedRegion").stringValue = "kr";
        serializedObject.FindProperty("maxPlayers").intValue = 6;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static NetworkRunner CreateOrLoadRunnerPrefab()
    {
        NetworkRunner prefab = AssetDatabase.LoadAssetAtPath<NetworkRunner>(RunnerPrefabPath);

        if (prefab != null)
        {
            return prefab;
        }

        GameObject runnerObject = new GameObject("NetworkRunner");
        NetworkRunner runner = runnerObject.AddComponent<NetworkRunner>();

        GameObject prefabObject = PrefabUtility.SaveAsPrefabAsset(runnerObject, RunnerPrefabPath);
        UnityEngine.Object.DestroyImmediate(runnerObject);

        return prefabObject.GetComponent<NetworkRunner>();
    }

    private static NetworkRoomPlayer CreateOrLoadRoomPlayerPrefab()
    {
        NetworkRoomPlayer prefab =
            AssetDatabase.LoadAssetAtPath<NetworkRoomPlayer>(RoomPlayerPrefabPath);

        if (prefab != null)
        {
            return prefab;
        }

        GameObject playerObject = new GameObject("NetworkRoomPlayer");
        NetworkObject networkObject = playerObject.AddComponent<NetworkObject>();
        networkObject.IsSpawnable = true;
        playerObject.AddComponent<NetworkRoomPlayer>();

        GameObject prefabObject = PrefabUtility.SaveAsPrefabAsset(playerObject, RoomPlayerPrefabPath);
        UnityEngine.Object.DestroyImmediate(playerObject);

        return prefabObject.GetComponent<NetworkRoomPlayer>();
    }

    private static void CreateOrUpdateGameScene()
    {
        Scene scene = System.IO.File.Exists(GameScenePath)
            ? EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        scene.name = "GameScene";

        if (GameObject.Find("Game Session Label") == null)
        {
            GameObject canvasObject = CreateCanvas("Game Session Canvas");
            Text label = CreateText(
                canvasObject.transform,
                "Game Session Label",
                "Game Session Running",
                28,
                TextAnchor.MiddleCenter);

            RectTransform rectTransform = label.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        EditorSceneManager.SaveScene(scene, GameScenePath);
    }

    private static void CreateOrUpdateSampleScene(
        PhotonFusionSettingsSO settings,
        NetworkRunner runnerPrefab,
        NetworkRoomPlayer roomPlayerPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        GameObject serviceObject = FindOrCreateRoot("Network Services");
        FusionRoomService roomService = GetOrAddComponent<FusionRoomService>(serviceObject);
        ClipboardInviteService inviteService = GetOrAddComponent<ClipboardInviteService>(serviceObject);

        SerializedObject roomServiceObject = new SerializedObject(roomService);
        roomServiceObject.FindProperty("runnerPrefab").objectReferenceValue = runnerPrefab;
        roomServiceObject.FindProperty("provideInput").boolValue = true;
        roomServiceObject.FindProperty("roomSceneBuildIndex").intValue = 0;
        roomServiceObject.FindProperty("gameSceneBuildIndex").intValue = 1;
        roomServiceObject.FindProperty("roomPlayerPrefab").objectReferenceValue = roomPlayerPrefab;
        roomServiceObject.FindProperty("photonSettings").objectReferenceValue = settings;
        roomServiceObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject uiRoot = FindOrCreateRoot("Network Test UI");
        Canvas canvas = GetOrAddComponent<Canvas>(uiRoot);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GetOrAddComponent<CanvasScaler>(uiRoot);
        GetOrAddComponent<GraphicRaycaster>(uiRoot);

        NetworkTestView view = GetOrAddComponent<NetworkTestView>(uiRoot);
        NetworkTestPresenter presenter = GetOrAddComponent<NetworkTestPresenter>(uiRoot);

        BuildNetworkTestUi(uiRoot.transform, view);

        SerializedObject presenterObject = new SerializedObject(presenter);
        presenterObject.FindProperty("roomService").objectReferenceValue = roomService;
        presenterObject.FindProperty("inviteService").objectReferenceValue = inviteService;
        presenterObject.FindProperty("view").objectReferenceValue = view;
        presenterObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(roomService);
        EditorUtility.SetDirty(view);
        EditorUtility.SetDirty(presenter);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildNetworkTestUi(Transform root, NetworkTestView view)
    {
        ClearGeneratedUi(root);

        Font font = GetBuiltInFont();
        GameObject panel = CreatePanel(root, "Panel", new Vector2(24f, -24f), new Vector2(520f, 680f));

        InputField roomNameInput = CreateInput(panel.transform, "Room Name Input", "Room Name", "TestRoom", -40f, font);
        InputField maxPlayersInput = CreateInput(panel.transform, "Max Players Input", "Max Players", "6", -90f, font);
        InputField regionInput = CreateInput(panel.transform, "Region Input", "Region", "kr", -140f, font);
        Toggle privateToggle = CreateToggle(panel.transform, "Private Room Toggle", "Private Room", -190f, font);
        InputField passwordInput = CreateInput(panel.transform, "Password Input", "Password", "", -240f, font);

        Button createButton = CreateButton(panel.transform, "Create Room Button", "Create Room", -300f, font);
        Button joinButton = CreateButton(panel.transform, "Join Room Button", "Join Room", -350f, font);
        Button quickJoinButton = CreateButton(panel.transform, "Quick Join Button", "Quick Join", -400f, font);
        Button leaveButton = CreateButton(panel.transform, "Leave Room Button", "Leave Room", -450f, font);
        Button inviteButton = CreateButton(panel.transform, "Invite Button", "Invite", -500f, font);
        Button readyButton = CreateButton(panel.transform, "Ready Button", "Ready Toggle", -550f, font);
        Button startButton = CreateButton(panel.transform, "Start Game Button", "Start Game", -600f, font);

        Text statusText = CreateText(panel.transform, "Status Text", "Idle", 18, TextAnchor.MiddleLeft);
        SetRect(statusText.rectTransform, new Vector2(260f, -300f), new Vector2(220f, 40f));

        Text roomListText = CreateText(panel.transform, "Room List Text", "공개방 없음", 16, TextAnchor.UpperLeft);
        SetRect(roomListText.rectTransform, new Vector2(260f, -350f), new Vector2(230f, 120f));

        Text logText = CreateText(panel.transform, "Log Text", "Log", 14, TextAnchor.UpperLeft);
        SetRect(logText.rectTransform, new Vector2(260f, -500f), new Vector2(230f, 160f));

        SerializedObject viewObject = new SerializedObject(view);
        viewObject.FindProperty("roomNameInput").objectReferenceValue = roomNameInput;
        viewObject.FindProperty("maxPlayersInput").objectReferenceValue = maxPlayersInput;
        viewObject.FindProperty("regionInput").objectReferenceValue = regionInput;
        viewObject.FindProperty("privateRoomToggle").objectReferenceValue = privateToggle;
        viewObject.FindProperty("passwordInput").objectReferenceValue = passwordInput;
        viewObject.FindProperty("createRoomButton").objectReferenceValue = createButton;
        viewObject.FindProperty("joinRoomButton").objectReferenceValue = joinButton;
        viewObject.FindProperty("quickJoinButton").objectReferenceValue = quickJoinButton;
        viewObject.FindProperty("leaveRoomButton").objectReferenceValue = leaveButton;
        viewObject.FindProperty("inviteButton").objectReferenceValue = inviteButton;
        viewObject.FindProperty("readyButton").objectReferenceValue = readyButton;
        viewObject.FindProperty("startGameButton").objectReferenceValue = startButton;
        viewObject.FindProperty("statusText").objectReferenceValue = statusText;
        viewObject.FindProperty("roomListText").objectReferenceValue = roomListText;
        viewObject.FindProperty("logText").objectReferenceValue = logText;
        viewObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SampleScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
    }

    private static GameObject CreateCanvas(string name)
    {
        GameObject canvasObject = new GameObject(name);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject;
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.1f, 0.92f);

        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        return panel;
    }

    private static InputField CreateInput(
        Transform parent,
        string name,
        string placeholder,
        string text,
        float y,
        Font font)
    {
        GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = Color.white;

        RectTransform rectTransform = inputObject.GetComponent<RectTransform>();
        SetRect(rectTransform, new Vector2(20f, y), new Vector2(220f, 36f));

        Text textComponent = CreateText(inputObject.transform, "Text", text, 16, TextAnchor.MiddleLeft);
        textComponent.color = Color.black;
        textComponent.font = font;
        Stretch(textComponent.rectTransform, 10f, 6f);

        Text placeholderText = CreateText(inputObject.transform, "Placeholder", placeholder, 16, TextAnchor.MiddleLeft);
        placeholderText.color = new Color(0.35f, 0.35f, 0.35f, 0.75f);
        placeholderText.font = font;
        Stretch(placeholderText.rectTransform, 10f, 6f);

        InputField inputField = inputObject.GetComponent<InputField>();
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholderText;
        inputField.text = text;

        return inputField;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        float y,
        Font font)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.43f, 0.85f, 1f);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        SetRect(rectTransform, new Vector2(20f, y), new Vector2(220f, 38f));

        Text text = CreateText(buttonObject.transform, "Text", label, 16, TextAnchor.MiddleCenter);
        text.font = font;
        Stretch(text.rectTransform, 0f, 0f);

        return buttonObject.GetComponent<Button>();
    }

    private static Toggle CreateToggle(
        Transform parent,
        string name,
        string label,
        float y,
        Font font)
    {
        GameObject toggleObject = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);
        SetRect(toggleObject.GetComponent<RectTransform>(), new Vector2(20f, y), new Vector2(220f, 36f));

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        backgroundObject.GetComponent<Image>().color = Color.white;
        SetRect(backgroundObject.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(24f, 24f));

        GameObject checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkmarkObject.transform.SetParent(backgroundObject.transform, false);
        checkmarkObject.GetComponent<Image>().color = new Color(0.2f, 0.43f, 0.85f, 1f);
        Stretch(checkmarkObject.GetComponent<RectTransform>(), 4f, 4f);

        Text labelText = CreateText(toggleObject.transform, "Label", label, 16, TextAnchor.MiddleLeft);
        labelText.font = font;
        SetRect(labelText.rectTransform, new Vector2(34f, 0f), new Vector2(180f, 30f));

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = backgroundObject.GetComponent<Image>();
        toggle.graphic = checkmarkObject.GetComponent<Image>();
        toggle.isOn = false;

        return toggle;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string text,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text textComponent = textObject.GetComponent<Text>();
        textComponent.text = text;
        textComponent.font = GetBuiltInFont();
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;

        return textComponent;
    }

    private static Font GetBuiltInFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static void Stretch(RectTransform rectTransform, float horizontalPadding, float verticalPadding)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        rectTransform.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
    }

    private static void ClearGeneratedUi(Transform root)
    {
        List<GameObject> children = new List<GameObject>();

        foreach (Transform child in root)
        {
            children.Add(child.gameObject);
        }

        foreach (GameObject child in children)
        {
            UnityEngine.Object.DestroyImmediate(child);
        }
    }

    private static GameObject FindOrCreateRoot(string name)
    {
        GameObject root = GameObject.Find(name);

        if (root != null)
        {
            return root;
        }

        return new GameObject(name);
    }

    private static T GetOrAddComponent<T>(GameObject gameObject)
        where T : Component
    {
        if (gameObject.TryGetComponent(out T component))
        {
            return component;
        }

        return gameObject.AddComponent<T>();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string name = System.IO.Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException($"잘못된 폴더 경로입니다. Path={folderPath}");
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
