using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère les rooms de réunion VR.
/// Adapté pour fonctionner avec VRNetworkManager et NativeWebSocket.
/// </summary>
public class VRRoomManager : MonoBehaviour
{
    public static VRRoomManager Instance { get; private set; }
    
    [Header("Room Settings")]
    [Tooltip("Nombre maximum de joueurs par room")]
    public int maxPlayersPerRoom = 10;
    
    [Tooltip("Durée avant qu'une room inactive soit supprimée (secondes)")]
    public float roomTimeoutDuration = 300f;
    
    // État de la room
    public string CurrentRoomId { get; private set; }
    public bool IsInRoom { get; private set; }
    public bool IsHost { get; private set; }
    public RoomType CurrentRoomType { get; private set; } = RoomType.Lobby;
    
    // Joueurs dans la room (ID -> données)
    private Dictionary<string, VRPlayerData> _players = new Dictionary<string, VRPlayerData>();
    
    // Rooms disponibles (ID -> RoomInfo)
    private Dictionary<string, RoomInfo> _availableRooms = new Dictionary<string, RoomInfo>();
    
    // Events
    public static event Action<string> OnRoomCreated;
    public static event Action<string> OnRoomJoined;
    public static event Action OnRoomLeft;
    public static event Action<string> OnRoomError;
    public static event Action<VRPlayerData> OnPlayerJoined;
    public static event Action<string> OnPlayerLeft;
    public static event Action<Dictionary<string, RoomInfo>> OnRoomListUpdated;
    public static event Action<RoomType> OnRoomTypeChanged;
    
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
    
    void OnEnable()
    {
        VRNetworkManager.OnConnected += OnNetworkConnected;
        VRNetworkManager.OnDisconnected += OnNetworkDisconnected;
        VRNetworkManager.OnPeerDisconnected += OnPeerDisconnected;
        VRNetworkManager.OnMessageReceived += HandleMessage;
    }
    
    void OnDisable()
    {
        VRNetworkManager.OnConnected -= OnNetworkConnected;
        VRNetworkManager.OnDisconnected -= OnNetworkDisconnected;
        VRNetworkManager.OnPeerDisconnected -= OnPeerDisconnected;
        VRNetworkManager.OnMessageReceived -= HandleMessage;
    }
    
    void OnNetworkConnected()
    {
        Debug.Log("[VRRoom] Network connected, ready to create/join rooms");
        // Demander la liste des rooms disponibles
        RequestRoomList();
    }
    
    void OnNetworkDisconnected()
    {
        // Reset l'état si déconnecté
        if (IsInRoom)
        {
            CurrentRoomId = null;
            IsInRoom = false;
            IsHost = false;
            _players.Clear();
            OnRoomLeft?.Invoke();
        }
    }
    
    void OnPeerDisconnected(string peerId)
    {
        if (_players.ContainsKey(peerId))
        {
            _players.Remove(peerId);
            OnPlayerLeft?.Invoke(peerId);
            Debug.Log($"[VRRoom] Player disconnected: {peerId}");
        }
    }
    
    #region Public API
    
    /// <summary>
    /// Crée une nouvelle room de réunion.
    /// </summary>
    /// <param name="roomType">Type de salle (MeetingRoomA, MeetingRoomB)</param>
    /// <param name="roomName">Nom optionnel de la room</param>
    public void CreateRoom(RoomType roomType = RoomType.MeetingRoomA, string roomName = "")
    {
        if (IsInRoom)
        {
            OnRoomError?.Invoke("Vous êtes déjà dans une room. Quittez d'abord.");
            return;
        }
        
        if (!VRNetworkManager.IsConnected)
        {
            OnRoomError?.Invoke("Non connecté au serveur");
            return;
        }
        
        CurrentRoomId = GenerateRoomId();
        IsInRoom = true;
        IsHost = true;
        CurrentRoomType = roomType;
        
        _players.Clear();
        var localPlayer = new VRPlayerData
        {
            playerId = VRNetworkManager.LocalId,
            playerName = PlayerPrefs.GetString("PlayerName", "Player"),
            isHost = true,
            roomType = roomType
        };
        _players[VRNetworkManager.LocalId] = localPlayer;
        
        // Annoncer la nouvelle room
        VRNetworkManager.Instance.Send("room-available", new RoomInfo
        {
            roomId = CurrentRoomId,
            hostId = VRNetworkManager.LocalId,
            roomName = string.IsNullOrEmpty(roomName) ? $"Room {CurrentRoomId}" : roomName,
            roomType = roomType,
            playerCount = 1,
            maxPlayers = maxPlayersPerRoom
        });
        
        Debug.Log($"[VRRoom] Created room: {CurrentRoomId} (Type: {roomType})");
        OnRoomCreated?.Invoke(CurrentRoomId);
        OnRoomTypeChanged?.Invoke(roomType);
    }
    
    /// <summary>
    /// Rejoint une room existante.
    /// </summary>
    /// <param name="roomId">Code de la room à rejoindre</param>
    public void JoinRoom(string roomId)
    {
        if (IsInRoom)
        {
            OnRoomError?.Invoke("Vous êtes déjà dans une room");
            return;
        }
        
        if (string.IsNullOrEmpty(roomId))
        {
            OnRoomError?.Invoke("Code de room invalide");
            return;
        }
        
        roomId = roomId.ToUpper().Trim();
        
        if (!_availableRooms.ContainsKey(roomId))
        {
            OnRoomError?.Invoke($"Room '{roomId}' introuvable");
            return;
        }
        
        var roomInfo = _availableRooms[roomId];
        
        if (roomInfo.playerCount >= roomInfo.maxPlayers)
        {
            OnRoomError?.Invoke("Cette room est pleine");
            return;
        }
        
        CurrentRoomId = roomId;
        IsInRoom = true;
        IsHost = false;
        CurrentRoomType = roomInfo.roomType;
        
        _players.Clear();
        var localPlayer = new VRPlayerData
        {
            playerId = VRNetworkManager.LocalId,
            playerName = PlayerPrefs.GetString("PlayerName", "Player"),
            isHost = false,
            roomType = roomInfo.roomType
        };
        _players[VRNetworkManager.LocalId] = localPlayer;
        
        // Demander à rejoindre
        VRNetworkManager.Instance.Send("room-join", new RoomJoinRequest
        {
            roomId = roomId,
            playerId = VRNetworkManager.LocalId,
            playerName = localPlayer.playerName
        });
        
        Debug.Log($"[VRRoom] Joining room: {roomId}");
        OnRoomJoined?.Invoke(roomId);
        OnRoomTypeChanged?.Invoke(CurrentRoomType);
    }
    
    /// <summary>
    /// Quitte la room actuelle.
    /// </summary>
    public void LeaveRoom()
    {
        if (!IsInRoom)
            return;
        
        // Notifier les autres
        VRNetworkManager.Instance.Send("room-leave", new RoomLeaveData
        {
            roomId = CurrentRoomId,
            playerId = VRNetworkManager.LocalId
        });
        
        if (IsHost)
        {
            // Fermer la room si on est l'hôte
            VRNetworkManager.Instance.Send("room-closed", new RoomInfo
            {
                roomId = CurrentRoomId,
                hostId = VRNetworkManager.LocalId
            });
        }
        
        Debug.Log($"[VRRoom] Left room: {CurrentRoomId}");
        
        var previousRoomType = CurrentRoomType;
        CurrentRoomId = null;
        IsInRoom = false;
        IsHost = false;
        CurrentRoomType = RoomType.Lobby;
        _players.Clear();
        
        OnRoomLeft?.Invoke();
        OnRoomTypeChanged?.Invoke(RoomType.Lobby);
    }
    
    /// <summary>
    /// Téléporte le joueur vers une salle spécifique (sans changer de room réseau).
    /// </summary>
    public void TeleportToRoomType(RoomType roomType)
    {
        if (!IsInRoom)
        {
            OnRoomError?.Invoke("Vous devez être dans une room pour vous téléporter");
            return;
        }
        
        CurrentRoomType = roomType;
        
        // Notifier le changement de zone
        VRNetworkManager.Instance.Send("room-teleport", new RoomTeleportData
        {
            roomId = CurrentRoomId,
            playerId = VRNetworkManager.LocalId,
            targetRoomType = roomType
        });
        
        OnRoomTypeChanged?.Invoke(roomType);
        Debug.Log($"[VRRoom] Teleported to: {roomType}");
    }
    
    /// <summary>
    /// Demande la liste des rooms disponibles.
    /// </summary>
    public void RequestRoomList()
    {
        VRNetworkManager.Instance.Send("room-list-request", "");
    }
    
    /// <summary>
    /// Retourne la liste des joueurs dans la room actuelle.
    /// </summary>
    public List<VRPlayerData> GetPlayers()
    {
        return new List<VRPlayerData>(_players.Values);
    }
    
    /// <summary>
    /// Retourne le nombre de joueurs dans la room.
    /// </summary>
    public int PlayerCount => _players.Count;
    
    /// <summary>
    /// Retourne la liste des rooms disponibles.
    /// </summary>
    public Dictionary<string, RoomInfo> GetAvailableRooms()
    {
        return new Dictionary<string, RoomInfo>(_availableRooms);
    }
    
    /// <summary>
    /// Définit le nom du joueur local.
    /// </summary>
    public void SetPlayerName(string name)
    {
        PlayerPrefs.SetString("PlayerName", name);
        PlayerPrefs.Save();
        
        if (IsInRoom && _players.ContainsKey(VRNetworkManager.LocalId))
        {
            _players[VRNetworkManager.LocalId].playerName = name;
            
            // Notifier les autres du changement de nom
            VRNetworkManager.Instance.Send("player-name-update", new PlayerNameUpdate
            {
                roomId = CurrentRoomId,
                playerId = VRNetworkManager.LocalId,
                playerName = name
            });
        }
    }
    
    #endregion
    
    #region Message Handling
    
    void HandleMessage(NetworkMessage msg)
    {
        switch (msg.type)
        {
            case "room-available":
                HandleRoomAvailable(msg);
                break;
            case "room-closed":
                HandleRoomClosed(msg);
                break;
            case "room-join":
                HandleRoomJoin(msg);
                break;
            case "room-welcome":
                HandleRoomWelcome(msg);
                break;
            case "room-leave":
                HandleRoomLeave(msg);
                break;
            case "room-list":
                HandleRoomList(msg);
                break;
            case "room-teleport":
                HandleRoomTeleport(msg);
                break;
            case "player-name-update":
                HandlePlayerNameUpdate(msg);
                break;
        }
    }
    
    void HandleRoomAvailable(NetworkMessage msg)
    {
        var data = JsonUtility.FromJson<RoomInfo>(msg.data);
        
        _availableRooms[data.roomId] = data;
        Debug.Log($"[VRRoom] Room available: {data.roomId} ({data.roomName})");
        OnRoomListUpdated?.Invoke(_availableRooms);
    }
    
    void HandleRoomClosed(NetworkMessage msg)
    {
        var data = JsonUtility.FromJson<RoomInfo>(msg.data);
        
        _availableRooms.Remove(data.roomId);
        Debug.Log($"[VRRoom] Room closed: {data.roomId}");
        OnRoomListUpdated?.Invoke(_availableRooms);
        
        // Si on était dans cette room
        if (IsInRoom && CurrentRoomId == data.roomId && !IsHost)
        {
            CurrentRoomId = null;
            IsInRoom = false;
            CurrentRoomType = RoomType.Lobby;
            _players.Clear();
            OnRoomLeft?.Invoke();
            OnRoomTypeChanged?.Invoke(RoomType.Lobby);
            OnRoomError?.Invoke("La room a été fermée par l'hôte");
        }
    }
    
    void HandleRoomJoin(NetworkMessage msg)
    {
        var request = JsonUtility.FromJson<RoomJoinRequest>(msg.data);
        
        // Seul l'host traite les demandes de join
        if (!IsHost || request.roomId != CurrentRoomId)
            return;
        
        Debug.Log($"[VRRoom] Player joining: {request.playerId} ({request.playerName})");
        
        var newPlayer = new VRPlayerData
        {
            playerId = request.playerId,
            playerName = request.playerName,
            isHost = false,
            roomType = CurrentRoomType
        };
        _players[request.playerId] = newPlayer;
        
        // Envoyer un message de bienvenue avec la liste des joueurs
        var welcome = new RoomWelcomeData
        {
            roomId = CurrentRoomId,
            roomType = CurrentRoomType,
            players = new List<VRPlayerData>(_players.Values).ToArray()
        };
        VRNetworkManager.Instance.Send("room-welcome", welcome);
        
        // Mettre à jour le compteur de la room
        UpdateRoomPlayerCount();
        
        OnPlayerJoined?.Invoke(newPlayer);
    }
    
    void HandleRoomWelcome(NetworkMessage msg)
    {
        var data = JsonUtility.FromJson<RoomWelcomeData>(msg.data);
        
        if (!IsInRoom || data.roomId != CurrentRoomId)
            return;
        
        CurrentRoomType = data.roomType;
        
        foreach (var player in data.players)
        {
            if (!_players.ContainsKey(player.playerId))
            {
                _players[player.playerId] = player;
                
                if (player.playerId != VRNetworkManager.LocalId)
                {
                    OnPlayerJoined?.Invoke(player);
                }
            }
        }
        
        Debug.Log($"[VRRoom] Welcome received, {_players.Count} players in room");
    }
    
    void HandleRoomLeave(NetworkMessage msg)
    {
        var data = JsonUtility.FromJson<RoomLeaveData>(msg.data);
        
        if (!IsInRoom || data.roomId != CurrentRoomId)
            return;
        
        if (_players.ContainsKey(data.playerId))
        {
            _players.Remove(data.playerId);
            OnPlayerLeft?.Invoke(data.playerId);
            Debug.Log($"[VRRoom] Player left: {data.playerId}");
            
            if (IsHost)
            {
                UpdateRoomPlayerCount();
            }
        }
    }
    
    void HandleRoomList(NetworkMessage msg)
    {
        var data = JsonUtility.FromJson<RoomListData>(msg.data);
        
        _availableRooms.Clear();
        foreach (var room in data.rooms)
        {
            _availableRooms[room.roomId] = room;
        }
        
        OnRoomListUpdated?.Invoke(_availableRooms);
        Debug.Log($"[VRRoom] Room list updated: {_availableRooms.Count} rooms");
    }
    
    void HandleRoomTeleport(NetworkMessage msg)
    {
        var data = JsonUtility.FromJson<RoomTeleportData>(msg.data);
        
        if (!IsInRoom || data.roomId != CurrentRoomId)
            return;
        
        if (_players.ContainsKey(data.playerId))
        {
            _players[data.playerId].roomType = data.targetRoomType;
            Debug.Log($"[VRRoom] Player {data.playerId} teleported to {data.targetRoomType}");
        }
    }
    
    void HandlePlayerNameUpdate(NetworkMessage msg)
    {
        var data = JsonUtility.FromJson<PlayerNameUpdate>(msg.data);
        
        if (!IsInRoom || data.roomId != CurrentRoomId)
            return;
        
        if (_players.ContainsKey(data.playerId))
        {
            _players[data.playerId].playerName = data.playerName;
            Debug.Log($"[VRRoom] Player name updated: {data.playerId} -> {data.playerName}");
        }
    }
    
    void UpdateRoomPlayerCount()
    {
        VRNetworkManager.Instance.Send("room-update", new RoomInfo
        {
            roomId = CurrentRoomId,
            hostId = VRNetworkManager.LocalId,
            playerCount = _players.Count,
            maxPlayers = maxPlayersPerRoom,
            roomType = CurrentRoomType
        });
    }
    
    #endregion
    
    #region Helpers
    
    string GenerateRoomId()
    {
        // Caractères sans ambiguïté (pas de O/0, I/1, etc.)
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] id = new char[6];
        var random = new System.Random();
        
        for (int i = 0; i < 6; i++)
        {
            id[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(id);
    }
    
    #endregion
}

#region Enums

/// <summary>
/// Types de salles disponibles dans l'application.
/// </summary>
public enum RoomType
{
    Lobby,
    MeetingRoomA,
    MeetingRoomB
}

#endregion

#region Data Classes

[Serializable]
public class VRPlayerData
{
    public string playerId;
    public string playerName;
    public bool isHost;
    public RoomType roomType;
    
    // Position et rotation pour la synchronisation
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
    
    // Données VR spécifiques
    public float headPosX, headPosY, headPosZ;
    public float headRotX, headRotY, headRotZ, headRotW;
    public float leftHandPosX, leftHandPosY, leftHandPosZ;
    public float leftHandRotX, leftHandRotY, leftHandRotZ, leftHandRotW;
    public float rightHandPosX, rightHandPosY, rightHandPosZ;
    public float rightHandRotX, rightHandRotY, rightHandRotZ, rightHandRotW;
}

[Serializable]
public class RoomInfo
{
    public string roomId;
    public string hostId;
    public string roomName;
    public RoomType roomType;
    public int playerCount;
    public int maxPlayers;
}

[Serializable]
public class RoomJoinRequest
{
    public string roomId;
    public string playerId;
    public string playerName;
}

[Serializable]
public class RoomLeaveData
{
    public string roomId;
    public string playerId;
}

[Serializable]
public class RoomWelcomeData
{
    public string roomId;
    public RoomType roomType;
    public VRPlayerData[] players;
}

[Serializable]
public class RoomListData
{
    public RoomInfo[] rooms;
}

[Serializable]
public class RoomTeleportData
{
    public string roomId;
    public string playerId;
    public RoomType targetRoomType;
}

[Serializable]
public class PlayerNameUpdate
{
    public string roomId;
    public string playerId;
    public string playerName;
}

#endregion
