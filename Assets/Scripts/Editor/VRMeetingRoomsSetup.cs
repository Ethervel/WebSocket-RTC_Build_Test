using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// Outil d'édition pour créer rapidement la structure de la scène VR Meeting Rooms.
/// </summary>
public class VRMeetingRoomsSetup : EditorWindow
{
    private bool createManagers = true;
    private bool createLobby = true;
    private bool createRoomA = true;
    private bool createRoomB = true;
    private bool createUI = true;
    
    private float roomSpacing = 30f;
    private float floorSize = 10f;
    
    [MenuItem("Tools/VR Meeting Rooms/Scene Setup")]
    public static void ShowWindow()
    {
        GetWindow<VRMeetingRoomsSetup>("VR Meeting Setup");
    }
    
    void OnGUI()
    {
        GUILayout.Label("VR Meeting Rooms - Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        GUILayout.Label("Components to Create:", EditorStyles.boldLabel);
        createManagers = EditorGUILayout.Toggle("Managers (Network, Room, Game)", createManagers);
        createLobby = EditorGUILayout.Toggle("Lobby", createLobby);
        createRoomA = EditorGUILayout.Toggle("Meeting Room A", createRoomA);
        createRoomB = EditorGUILayout.Toggle("Meeting Room B", createRoomB);
        createUI = EditorGUILayout.Toggle("VR UI Canvas", createUI);
        
        EditorGUILayout.Space();
        GUILayout.Label("Settings:", EditorStyles.boldLabel);
        roomSpacing = EditorGUILayout.FloatField("Room Spacing", roomSpacing);
        floorSize = EditorGUILayout.FloatField("Floor Size", floorSize);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Create Scene Structure", GUILayout.Height(40)))
        {
            CreateSceneStructure();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This will create the basic scene structure for VR Meeting Rooms.\n" +
            "Make sure you have the required scripts in your project.",
            MessageType.Info
        );
    }
    
    void CreateSceneStructure()
    {
        // Créer les groupes parents
        var managersParent = CreateParentObject("--- MANAGERS ---");
        var environmentParent = CreateParentObject("--- ENVIRONMENT ---");
        var uiParent = CreateParentObject("--- UI ---");
        var lightingParent = CreateParentObject("--- LIGHTING ---");
        
        if (createManagers)
        {
            CreateManagers(managersParent.transform);
        }
        
        if (createLobby)
        {
            CreateRoom("Lobby", Vector3.zero, environmentParent.transform, true);
        }
        
        if (createRoomA)
        {
            CreateRoom("MeetingRoomA", new Vector3(roomSpacing, 0, 0), environmentParent.transform, false);
        }
        
        if (createRoomB)
        {
            CreateRoom("MeetingRoomB", new Vector3(0, 0, roomSpacing), environmentParent.transform, false);
        }
        
        if (createUI)
        {
            CreateVRUI(uiParent.transform);
        }
        
        // Créer l'éclairage
        CreateLighting(lightingParent.transform);
        
        Debug.Log("[VR Setup] Scene structure created successfully!");
    }
    
    GameObject CreateParentObject(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null) return existing;
        
        var go = new GameObject(name);
        return go;
    }
    
    void CreateManagers(Transform parent)
    {
        // Network Manager
        var networkManager = new GameObject("NetworkManager");
        networkManager.transform.SetParent(parent);
        var netScript = networkManager.AddComponent<VRNetworkManager>();
        
        // Room Manager
        var roomManager = new GameObject("RoomManager");
        roomManager.transform.SetParent(parent);
        roomManager.AddComponent<VRRoomManager>();
        
        // Game Manager
        var gameManager = new GameObject("GameManager");
        gameManager.transform.SetParent(parent);
        gameManager.AddComponent<VRGameManager>();
        
        // Teleportation Manager
        var teleportManager = new GameObject("TeleportationManager");
        teleportManager.transform.SetParent(parent);
        teleportManager.AddComponent<VRTeleportationManager>();
    }
    
    void CreateRoom(string roomName, Vector3 position, Transform parent, bool isLobby)
    {
        var room = new GameObject(roomName);
        room.transform.SetParent(parent);
        room.transform.position = position;
        
        // Sol
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(room.transform);
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localScale = new Vector3(floorSize / 10f, 1, floorSize / 10f);
        
        // Ajouter la zone de téléportation
        floor.AddComponent<VRTeleportArea>();
        floor.layer = LayerMask.NameToLayer("Default"); // Changer en "Teleport" si le layer existe
        
        // Spawn Point
        var spawnPoint = new GameObject($"SpawnPoint_{roomName}");
        spawnPoint.transform.SetParent(room.transform);
        spawnPoint.transform.localPosition = new Vector3(0, 0, 2);
        
        // Ajouter une icône pour visualiser
        var icon = spawnPoint.AddComponent<SpriteRenderer>();
        icon.color = Color.green;
        
        // Si ce n'est pas le lobby, ajouter un portail vers le lobby
        if (!isLobby)
        {
            CreatePortal(room.transform, "Portal_ToLobby", new Vector3(0, 1.5f, -floorSize/2 + 0.5f), RoomType.Lobby);
            
            // Table de réunion
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.SetParent(room.transform);
            table.transform.localPosition = new Vector3(0, 0.4f, 0);
            table.transform.localScale = new Vector3(3, 0.1f, 1.5f);
        }
        else
        {
            // Portails vers les salles depuis le lobby
            CreatePortal(room.transform, "Portal_ToRoomA", new Vector3(floorSize/2 - 1, 1.5f, 0), RoomType.MeetingRoomA);
            CreatePortal(room.transform, "Portal_ToRoomB", new Vector3(0, 1.5f, floorSize/2 - 1), RoomType.MeetingRoomB);
        }
    }
    
    void CreatePortal(Transform parent, string name, Vector3 localPosition, RoomType destination)
    {
        var portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        portal.name = name;
        portal.transform.SetParent(parent);
        portal.transform.localPosition = localPosition;
        portal.transform.localScale = new Vector3(2, 3, 0.2f);
        
        // Configurer comme trigger
        var collider = portal.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        
        // Ajouter le script de portail
        var portalScript = portal.AddComponent<VRTeleportPortal>();
        portalScript.destinationRoomType = destination;
        
        // Matériau semi-transparent
        var renderer = portal.GetComponent<Renderer>();
        var material = new Material(Shader.Find("Standard"));
        material.color = new Color(0, 0.5f, 1f, 0.5f);
        material.SetFloat("_Mode", 3); // Transparent
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        renderer.material = material;
    }
    
    void CreateVRUI(Transform parent)
    {
        // Créer un Canvas World Space
        var canvasGO = new GameObject("VRRoomUI");
        canvasGO.transform.SetParent(parent);
        canvasGO.transform.position = new Vector3(0, 1.5f, 3);
        
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        var canvasScaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = 100;
        
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        var rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(800, 600);
        rect.localScale = new Vector3(0.002f, 0.002f, 0.002f);
        
        // Ajouter le script UI
        canvasGO.AddComponent<VRRoomUI>();
        
        // Créer les panels de base
        CreateUIPanel(canvasGO.transform, "MenuPanel", true);
        CreateUIPanel(canvasGO.transform, "RoomPanel", false);
        CreateUIPanel(canvasGO.transform, "RoomListPanel", false);
        
        Debug.Log("[VR Setup] UI Canvas created. Please configure the VRRoomUI component manually.");
    }
    
    void CreateUIPanel(Transform parent, string name, bool active)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent);
        
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        var image = panel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        
        panel.SetActive(active);
    }
    
    void CreateLighting(Transform parent)
    {
        // Lumière directionnelle principale
        var directionalLight = new GameObject("Directional Light");
        directionalLight.transform.SetParent(parent);
        directionalLight.transform.rotation = Quaternion.Euler(50, -30, 0);
        
        var light = directionalLight.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.shadows = LightShadows.Soft;
        
        // Lumière ambiante dans le lobby
        var ambientLight = new GameObject("Ambient Light");
        ambientLight.transform.SetParent(parent);
        ambientLight.transform.position = new Vector3(0, 5, 0);
        
        var pointLight = ambientLight.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.intensity = 0.5f;
        pointLight.range = 20f;
    }
}
#endif
