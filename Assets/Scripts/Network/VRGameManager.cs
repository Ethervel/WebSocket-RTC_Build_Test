using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère le spawning des joueurs VR et la synchronisation de leurs positions/rotations.
/// Inclut la synchronisation de la tête et des mains pour les avatars VR.
/// </summary>
public class VRGameManager : MonoBehaviour
{
    public static VRGameManager Instance { get; private set; }
    
    [Header("Player Prefabs")]
    [Tooltip("Prefab du joueur local (XR Rig)")]
    public GameObject localPlayerPrefab;
    
    [Tooltip("Prefab des joueurs distants (avatar VR)")]
    public GameObject remotePlayerPrefab;
    
    [Header("Spawn Points - Lobby")]
    public Transform lobbySpawnPoint;
    
    [Header("Spawn Points - Meeting Room A")]
    public Transform roomASpawnPoint;
    public Transform[] roomAAdditionalSpawns;
    
    [Header("Spawn Points - Meeting Room B")]
    public Transform roomBSpawnPoint;
    public Transform[] roomBAdditionalSpawns;
    
    [Header("Sync Settings")]
    [Tooltip("Fréquence de synchronisation (updates par seconde)")]
    public float syncRate = 30f;
    
    [Tooltip("Vitesse d'interpolation des positions distantes")]
    public float interpolationSpeed = 15f;
    
    [Tooltip("Synchroniser les mains des avatars")]
    public bool syncHands = true;
    
    [Header("Spawn Settings")]
    [Tooltip("Spawner le joueur local au démarrage")]
    public bool spawnPlayerOnStart = true;
    
    // Références locales
    private GameObject _localPlayer;
    private Transform _localHead;
    private Transform _localLeftHand;
    private Transform _localRightHand;
    
    // Joueurs distants
    private Dictionary<string, VRRemotePlayer> _remotePlayers = new Dictionary<string, VRRemotePlayer>();
    
    // Synchronisation
    private float _syncTimer;
    
    // Events
    public static event Action<GameObject> OnLocalPlayerSpawned;
    public static event Action<string, GameObject> OnRemotePlayerSpawned;
    public static event Action<string> OnRemotePlayerDespawned;
    
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        // Spawner le joueur local au démarrage (dans le lobby)
        if (spawnPlayerOnStart)
        {
            SpawnLocalPlayer(RoomType.Lobby);
        }
    }
    
    void OnEnable()
    {
        VRRoomManager.OnRoomCreated += OnRoomEntered;
        VRRoomManager.OnRoomJoined += OnRoomEntered;
        VRRoomManager.OnRoomLeft += OnRoomLeft;
        VRRoomManager.OnPlayerJoined += OnPlayerJoined;
        VRRoomManager.OnPlayerLeft += OnPlayerLeft;
        VRRoomManager.OnRoomTypeChanged += OnRoomTypeChanged;
        VRNetworkManager.OnMessageReceived += HandleNetworkMessage;
    }
    
    void OnDisable()
    {
        VRRoomManager.OnRoomCreated -= OnRoomEntered;
        VRRoomManager.OnRoomJoined -= OnRoomEntered;
        VRRoomManager.OnRoomLeft -= OnRoomLeft;
        VRRoomManager.OnPlayerJoined -= OnPlayerJoined;
        VRRoomManager.OnPlayerLeft -= OnPlayerLeft;
        VRRoomManager.OnRoomTypeChanged -= OnRoomTypeChanged;
        VRNetworkManager.OnMessageReceived -= HandleNetworkMessage;
    }
    
    void Update()
    {
        // Envoyer notre position seulement si dans une room
        if (_localPlayer != null && VRRoomManager.Instance != null && VRRoomManager.Instance.IsInRoom)
        {
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= 1f / syncRate)
            {
                _syncTimer = 0f;
                SendPositionUpdate();
            }
        }
        
        // Interpoler les positions des joueurs distants
        InterpolateRemotePlayers();
    }
    
    #region Room Events
    
    void OnRoomEntered(string roomId)
    {
        Debug.Log($"[VRGame] Entered room: {roomId}");
        
        // S'assurer que le joueur existe (au cas où spawnPlayerOnStart est false)
        if (_localPlayer == null)
        {
            SpawnLocalPlayer(RoomType.Lobby);
        }
        
        // Ne PAS téléporter automatiquement - le joueur reste où il est
        // La téléportation se fait via les pads de téléportation
    }
    
    void OnRoomLeft()
    {
        Debug.Log("[VRGame] Left room");
        
        // Despawn seulement les joueurs distants, PAS le joueur local
        DespawnAllRemotePlayers();
    }
    
    void OnPlayerJoined(VRPlayerData player)
    {
        Debug.Log($"[VRGame] Player joined: {player.playerId} ({player.playerName})");
        
        // Ne pas spawner notre propre avatar comme remote
        if (player.playerId == VRNetworkManager.LocalId)
            return;
        
        SpawnRemotePlayer(player);
    }
    
    void OnPlayerLeft(string playerId)
    {
        Debug.Log($"[VRGame] Player left: {playerId}");
        DespawnRemotePlayer(playerId);
    }
    
    void OnRoomTypeChanged(RoomType roomType)
    {
        Debug.Log($"[VRGame] Room type changed to: {roomType}");
        // La téléportation est gérée par TeleportOnGrab, pas ici
    }
    
    #endregion
    
    #region Local Player Management
    
    void SpawnLocalPlayer(RoomType roomType)
    {
        if (_localPlayer != null)
        {
            Debug.Log("[VRGame] Local player already exists");
            return;
        }
        
        if (localPlayerPrefab == null)
        {
            Debug.LogError("[VRGame] localPlayerPrefab not assigned!");
            return;
        }
        
        Vector3 position;
        Quaternion rotation;
        GetSpawnPoint(roomType, true, out position, out rotation);
        
        _localPlayer = Instantiate(localPlayerPrefab, position, rotation);
        _localPlayer.name = "LocalVRPlayer";
        
        // Trouver les références VR
        FindVRReferences();
        
        Debug.Log($"[VRGame] Local VR player spawned at {position}");
        OnLocalPlayerSpawned?.Invoke(_localPlayer);
    }
    
    void FindVRReferences()
    {
        if (_localPlayer == null) return;
        
        // Chercher les composants XR standard
        // Camera (Head)
        var cameras = _localPlayer.GetComponentsInChildren<Camera>();
        foreach (var cam in cameras)
        {
            if (cam.CompareTag("MainCamera") || cam.name.Contains("Head") || cam.name.Contains("Camera"))
            {
                _localHead = cam.transform;
                break;
            }
        }
        
        // Si pas trouvé, chercher par nom
        if (_localHead == null)
        {
            var headTransform = FindChildRecursive(_localPlayer.transform, "Head");
            if (headTransform == null)
                headTransform = FindChildRecursive(_localPlayer.transform, "Camera");
            _localHead = headTransform;
        }
        
        // Mains
        _localLeftHand = FindChildRecursive(_localPlayer.transform, "LeftHand");
        if (_localLeftHand == null)
            _localLeftHand = FindChildRecursive(_localPlayer.transform, "Left Controller");
        
        _localRightHand = FindChildRecursive(_localPlayer.transform, "RightHand");
        if (_localRightHand == null)
            _localRightHand = FindChildRecursive(_localPlayer.transform, "Right Controller");
        
        Debug.Log($"[VRGame] VR References found - Head: {_localHead != null}, LeftHand: {_localLeftHand != null}, RightHand: {_localRightHand != null}");
    }
    
    Transform FindChildRecursive(Transform parent, string nameContains)
    {
        foreach (Transform child in parent)
        {
            if (child.name.ToLower().Contains(nameContains.ToLower()))
                return child;
            
            var result = FindChildRecursive(child, nameContains);
            if (result != null)
                return result;
        }
        return null;
    }
    
    public void TeleportLocalPlayer(RoomType roomType)
    {
        if (_localPlayer == null) return;
        
        Vector3 position;
        Quaternion rotation;
        GetSpawnPoint(roomType, true, out position, out rotation);
        
        // Téléporter le joueur VR
        var characterController = _localPlayer.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
            _localPlayer.transform.position = position;
            _localPlayer.transform.rotation = rotation;
            characterController.enabled = true;
        }
        else
        {
            _localPlayer.transform.position = position;
            _localPlayer.transform.rotation = rotation;
        }
        
        Debug.Log($"[VRGame] Local player teleported to {roomType} at {position}");
    }
    
    #endregion
    
    #region Remote Player Management
    
    void SpawnRemotePlayer(VRPlayerData playerData)
    {
        if (_remotePlayers.ContainsKey(playerData.playerId))
            return;
        
        if (remotePlayerPrefab == null)
        {
            Debug.LogWarning("[VRGame] remotePlayerPrefab not assigned!");
            return;
        }
        
        Vector3 position;
        Quaternion rotation;
        GetSpawnPoint(playerData.roomType, false, out position, out rotation);
        
        var go = Instantiate(remotePlayerPrefab, position, rotation);
        go.name = $"RemotePlayer_{playerData.playerName}_{playerData.playerId.Substring(0, 6)}";
        
        // IMPORTANT: Désactiver la caméra sur le joueur distant
        var cameras = go.GetComponentsInChildren<Camera>();
        foreach (var cam in cameras)
        {
            cam.enabled = false;
        }
        
        // Désactiver l'AudioListener si présent
        var audioListeners = go.GetComponentsInChildren<AudioListener>();
        foreach (var listener in audioListeners)
        {
            listener.enabled = false;
        }
        
        // Désactiver les scripts de contrôle
        var desktopController = go.GetComponent<DesktopPlayerController>();
        if (desktopController != null)
        {
            Destroy(desktopController);
        }
        
        var vrController = go.GetComponent<VRPlayerController>();
        if (vrController != null)
        {
            Destroy(vrController);
        }
        
        var charController = go.GetComponent<CharacterController>();
        if (charController != null)
        {
            Destroy(charController);
        }
        
        // Configurer l'avatar distant
        var remote = new VRRemotePlayer
        {
            playerId = playerData.playerId,
            playerName = playerData.playerName,
            gameObject = go,
            targetPosition = position,
            targetRotation = rotation,
            hasReceivedData = false
        };
        
        // Trouver les références de l'avatar
        remote.head = FindChildRecursive(go.transform, "Head");
        remote.leftHand = FindChildRecursive(go.transform, "LeftHand");
        remote.rightHand = FindChildRecursive(go.transform, "RightHand");
        
        // Configurer le nom au-dessus de l'avatar
        var nameTag = go.GetComponentInChildren<TMPro.TextMeshPro>();
        if (nameTag != null)
        {
            nameTag.text = playerData.playerName;
        }
        
        _remotePlayers[playerData.playerId] = remote;
        
        Debug.Log($"[VRGame] Remote player spawned: {playerData.playerName} ({playerData.playerId})");
        OnRemotePlayerSpawned?.Invoke(playerData.playerId, go);
    }
    
    void DespawnRemotePlayer(string playerId)
    {
        if (_remotePlayers.TryGetValue(playerId, out var remote))
        {
            if (remote.gameObject != null)
            {
                Destroy(remote.gameObject);
            }
            _remotePlayers.Remove(playerId);
            Debug.Log($"[VRGame] Remote player despawned: {playerId}");
            OnRemotePlayerDespawned?.Invoke(playerId);
        }
    }
    
    void DespawnAllRemotePlayers()
    {
        foreach (var remote in _remotePlayers.Values)
        {
            if (remote.gameObject != null)
            {
                Destroy(remote.gameObject);
            }
        }
        _remotePlayers.Clear();
        Debug.Log("[VRGame] All remote players despawned");
    }
    
    void DespawnAll()
    {
        // Despawn le joueur local
        if (_localPlayer != null)
        {
            Destroy(_localPlayer);
            _localPlayer = null;
            _localHead = null;
            _localLeftHand = null;
            _localRightHand = null;
        }
        
        // Despawn tous les joueurs distants
        DespawnAllRemotePlayers();
        
        Debug.Log("[VRGame] All players despawned");
    }
    
    #endregion
    
    #region Network Sync
    
    void SendPositionUpdate()
    {
        if (_localPlayer == null || VRNetworkManager.Instance == null)
            return;
        
        if (VRRoomManager.Instance == null || !VRRoomManager.Instance.IsInRoom)
            return;
        
        var data = new VRPositionData
        {
            roomId = VRRoomManager.Instance.CurrentRoomId,
            roomType = VRRoomManager.Instance.CurrentRoomType,
            
            // Position et rotation du rig
            posX = _localPlayer.transform.position.x,
            posY = _localPlayer.transform.position.y,
            posZ = _localPlayer.transform.position.z,
            rotY = _localPlayer.transform.eulerAngles.y
        };
        
        // Données de la tête
        if (_localHead != null)
        {
            data.headPosX = _localHead.position.x;
            data.headPosY = _localHead.position.y;
            data.headPosZ = _localHead.position.z;
            data.headRotX = _localHead.rotation.x;
            data.headRotY = _localHead.rotation.y;
            data.headRotZ = _localHead.rotation.z;
            data.headRotW = _localHead.rotation.w;
        }
        
        // Données des mains
        if (syncHands)
        {
            if (_localLeftHand != null)
            {
                data.leftHandPosX = _localLeftHand.position.x;
                data.leftHandPosY = _localLeftHand.position.y;
                data.leftHandPosZ = _localLeftHand.position.z;
                data.leftHandRotX = _localLeftHand.rotation.x;
                data.leftHandRotY = _localLeftHand.rotation.y;
                data.leftHandRotZ = _localLeftHand.rotation.z;
                data.leftHandRotW = _localLeftHand.rotation.w;
            }
            
            if (_localRightHand != null)
            {
                data.rightHandPosX = _localRightHand.position.x;
                data.rightHandPosY = _localRightHand.position.y;
                data.rightHandPosZ = _localRightHand.position.z;
                data.rightHandRotX = _localRightHand.rotation.x;
                data.rightHandRotY = _localRightHand.rotation.y;
                data.rightHandRotZ = _localRightHand.rotation.z;
                data.rightHandRotW = _localRightHand.rotation.w;
            }
        }
        
        VRNetworkManager.Instance.Send("vr-position", data);
    }
    
    void HandleNetworkMessage(NetworkMessage msg)
    {
        if (msg.type != "vr-position")
            return;
        
        var data = JsonUtility.FromJson<VRPositionData>(msg.data);
        
        // Vérifier que c'est pour notre room
        if (VRRoomManager.Instance == null || data.roomId != VRRoomManager.Instance.CurrentRoomId)
            return;
        
        // Trouver le joueur distant correspondant
        if (_remotePlayers.TryGetValue(msg.senderId, out var remote))
        {
            // Mettre à jour les positions cibles
            remote.targetPosition = new Vector3(data.posX, data.posY, data.posZ);
            remote.targetRotation = Quaternion.Euler(0, data.rotY, 0);
            
            // Tête
            remote.targetHeadPosition = new Vector3(data.headPosX, data.headPosY, data.headPosZ);
            remote.targetHeadRotation = new Quaternion(data.headRotX, data.headRotY, data.headRotZ, data.headRotW);
            
            // Mains
            if (syncHands)
            {
                remote.targetLeftHandPosition = new Vector3(data.leftHandPosX, data.leftHandPosY, data.leftHandPosZ);
                remote.targetLeftHandRotation = new Quaternion(data.leftHandRotX, data.leftHandRotY, data.leftHandRotZ, data.leftHandRotW);
                
                remote.targetRightHandPosition = new Vector3(data.rightHandPosX, data.rightHandPosY, data.rightHandPosZ);
                remote.targetRightHandRotation = new Quaternion(data.rightHandRotX, data.rightHandRotY, data.rightHandRotZ, data.rightHandRotW);
            }
            
            remote.hasReceivedData = true;
            remote.currentRoomType = data.roomType;
        }
    }
    
    void InterpolateRemotePlayers()
    {
        float t = Time.deltaTime * interpolationSpeed;
        
        foreach (var remote in _remotePlayers.Values)
        {
            if (remote.gameObject == null || !remote.hasReceivedData)
                continue;
            
            // Interpoler la position/rotation du corps
            remote.gameObject.transform.position = Vector3.Lerp(
                remote.gameObject.transform.position,
                remote.targetPosition,
                t
            );
            
            remote.gameObject.transform.rotation = Quaternion.Slerp(
                remote.gameObject.transform.rotation,
                remote.targetRotation,
                t
            );
            
            // Interpoler la tête
            if (remote.head != null)
            {
                remote.head.position = Vector3.Lerp(
                    remote.head.position,
                    remote.targetHeadPosition,
                    t
                );
                remote.head.rotation = Quaternion.Slerp(
                    remote.head.rotation,
                    remote.targetHeadRotation,
                    t
                );
            }
            
            // Interpoler les mains
            if (syncHands)
            {
                if (remote.leftHand != null)
                {
                    remote.leftHand.position = Vector3.Lerp(
                        remote.leftHand.position,
                        remote.targetLeftHandPosition,
                        t
                    );
                    remote.leftHand.rotation = Quaternion.Slerp(
                        remote.leftHand.rotation,
                        remote.targetLeftHandRotation,
                        t
                    );
                }
                
                if (remote.rightHand != null)
                {
                    remote.rightHand.position = Vector3.Lerp(
                        remote.rightHand.position,
                        remote.targetRightHandPosition,
                        t
                    );
                    remote.rightHand.rotation = Quaternion.Slerp(
                        remote.rightHand.rotation,
                        remote.targetRightHandRotation,
                        t
                    );
                }
            }
        }
    }
    
    #endregion
    
    #region Spawn Point Management
    
    void GetSpawnPoint(RoomType roomType, bool isLocalPlayer, out Vector3 position, out Quaternion rotation)
    {
        Transform spawnPoint = null;
        
        switch (roomType)
        {
            case RoomType.Lobby:
                spawnPoint = lobbySpawnPoint;
                break;
                
            case RoomType.MeetingRoomA:
                if (isLocalPlayer || roomAAdditionalSpawns == null || roomAAdditionalSpawns.Length == 0)
                {
                    spawnPoint = roomASpawnPoint;
                }
                else
                {
                    int index = UnityEngine.Random.Range(0, roomAAdditionalSpawns.Length);
                    spawnPoint = roomAAdditionalSpawns[index];
                }
                break;
                
            case RoomType.MeetingRoomB:
                if (isLocalPlayer || roomBAdditionalSpawns == null || roomBAdditionalSpawns.Length == 0)
                {
                    spawnPoint = roomBSpawnPoint;
                }
                else
                {
                    int index = UnityEngine.Random.Range(0, roomBAdditionalSpawns.Length);
                    spawnPoint = roomBAdditionalSpawns[index];
                }
                break;
        }
        
        if (spawnPoint != null)
        {
            position = spawnPoint.position;
            rotation = spawnPoint.rotation;
        }
        else
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Debug.LogWarning($"[VRGame] No spawn point found for {roomType}");
        }
    }
    
    #endregion
    
    #region Public Utilities
    
    public GameObject GetLocalPlayer()
    {
        return _localPlayer;
    }
    
    public GameObject GetRemotePlayer(string playerId)
    {
        if (_remotePlayers.TryGetValue(playerId, out var remote))
        {
            return remote.gameObject;
        }
        return null;
    }
    
    public Dictionary<string, GameObject> GetAllRemotePlayers()
    {
        var result = new Dictionary<string, GameObject>();
        foreach (var kvp in _remotePlayers)
        {
            if (kvp.Value.gameObject != null)
            {
                result[kvp.Key] = kvp.Value.gameObject;
            }
        }
        return result;
    }
    
    #endregion
}

#region Helper Classes

public class VRRemotePlayer
{
    public string playerId;
    public string playerName;
    public GameObject gameObject;
    
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;
    
    public Vector3 targetPosition;
    public Quaternion targetRotation;
    
    public Vector3 targetHeadPosition;
    public Quaternion targetHeadRotation;
    
    public Vector3 targetLeftHandPosition;
    public Quaternion targetLeftHandRotation;
    
    public Vector3 targetRightHandPosition;
    public Quaternion targetRightHandRotation;
    
    public bool hasReceivedData;
    public RoomType currentRoomType;
}

[Serializable]
public class VRPositionData
{
    public string roomId;
    public RoomType roomType;
    
    public float posX, posY, posZ;
    public float rotY;
    
    public float headPosX, headPosY, headPosZ;
    public float headRotX, headRotY, headRotZ, headRotW;
    
    public float leftHandPosX, leftHandPosY, leftHandPosZ;
    public float leftHandRotX, leftHandRotY, leftHandRotZ, leftHandRotW;
    
    public float rightHandPosX, rightHandPosY, rightHandPosZ;
    public float rightHandRotX, rightHandRotY, rightHandRotZ, rightHandRotW;
}

#endregion