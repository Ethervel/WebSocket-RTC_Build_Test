using System;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;

/// <summary>
/// Gestionnaire réseau adapté pour NativeWebSocket (compatible WebGL/VR).
/// Gère la connexion au serveur de signalisation.
/// </summary>
public class VRNetworkManager : MonoBehaviour
{
    public static VRNetworkManager Instance { get; private set; }
    
    [Header("Server Configuration")]
    [Tooltip("URL du serveur WebSocket")]
    public string serverUrl = "ws://localhost:8080/game";
    
    [Tooltip("Tentatives de reconnexion automatique")]
    public bool autoReconnect = true;
    
    [Tooltip("Délai entre les tentatives de reconnexion (secondes)")]
    public float reconnectDelay = 3f;
    
    // État réseau
    public static string LocalId { get; private set; }
    public static bool IsConnected { get; private set; }
    
    // WebSocket client
    private WebSocket _websocket;
    private bool _isReconnecting;
    private float _reconnectTimer;
    
    // Events
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<string> OnPeerConnected;
    public static event Action<string> OnPeerDisconnected;
    public static event Action<NetworkMessage> OnMessageReceived;
    public static event Action<string> OnConnectionError;
    
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
    
    async void Start()
    {
        await Connect();
    }
    
    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
        // Dispatcher les messages WebSocket (nécessaire hors WebGL)
        _websocket?.DispatchMessageQueue();
        #endif
        
        // Gestion de la reconnexion automatique
        if (_isReconnecting && autoReconnect)
        {
            _reconnectTimer -= Time.deltaTime;
            if (_reconnectTimer <= 0)
            {
                _isReconnecting = false;
                _ = Connect();
            }
        }
    }
    
    async void OnDestroy()
    {
        await Disconnect();
    }
    
    async void OnApplicationQuit()
    {
        await Disconnect();
    }
    
    #region Connection Management
    
    /// <summary>
    /// Établit la connexion au serveur WebSocket.
    /// </summary>
    public async System.Threading.Tasks.Task Connect()
    {
        if (_websocket != null && _websocket.State == WebSocketState.Open)
        {
            Debug.Log("[VRNet] Already connected");
            return;
        }
        
        try
        {
            Debug.Log($"[VRNet] Connecting to {serverUrl}...");
            
            _websocket = new WebSocket(serverUrl);
            
            _websocket.OnOpen += OnWebSocketOpen;
            _websocket.OnMessage += OnWebSocketMessage;
            _websocket.OnClose += OnWebSocketClose;
            _websocket.OnError += OnWebSocketError;
            
            await _websocket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"[VRNet] Connection failed: {e.Message}");
            OnConnectionError?.Invoke(e.Message);
            ScheduleReconnect();
        }
    }
    
    /// <summary>
    /// Ferme la connexion WebSocket.
    /// </summary>
    public async System.Threading.Tasks.Task Disconnect()
    {
        autoReconnect = false;
        _isReconnecting = false;
        
        if (_websocket != null && _websocket.State == WebSocketState.Open)
        {
            await _websocket.Close();
        }
        
        _websocket = null;
        IsConnected = false;
    }
    
    void ScheduleReconnect()
    {
        if (autoReconnect && !_isReconnecting)
        {
            _isReconnecting = true;
            _reconnectTimer = reconnectDelay;
            Debug.Log($"[VRNet] Will reconnect in {reconnectDelay}s...");
        }
    }
    
    #endregion
    
    #region WebSocket Callbacks
    
    void OnWebSocketOpen()
    {
        Debug.Log("[VRNet] WebSocket connected");
    }
    
    void OnWebSocketMessage(byte[] data)
    {
        string json = System.Text.Encoding.UTF8.GetString(data);
        HandleMessage(json);
    }
    
    void OnWebSocketClose(WebSocketCloseCode closeCode)
    {
        Debug.Log($"[VRNet] WebSocket closed: {closeCode}");
        IsConnected = false;
        LocalId = null;
        OnDisconnected?.Invoke();
        ScheduleReconnect();
    }
    
    void OnWebSocketError(string errorMsg)
    {
        Debug.LogError($"[VRNet] WebSocket error: {errorMsg}");
        OnConnectionError?.Invoke(errorMsg);
    }
    
    #endregion
    
    #region Message Handling
    
    void HandleMessage(string json)
    {
        try
        {
            var msg = JsonUtility.FromJson<NetworkMessage>(json);
            
            // Message de bienvenue du serveur (attribution de l'ID)
            if (msg.type == "welcome")
            {
                LocalId = msg.senderId;
                IsConnected = true;
                Debug.Log($"[VRNet] Connected with ID: {LocalId}");
                OnConnected?.Invoke();
                return;
            }
            
            // Notification de connexion d'un peer
            if (msg.type == "peer-connected")
            {
                Debug.Log($"[VRNet] Peer connected: {msg.senderId}");
                OnPeerConnected?.Invoke(msg.senderId);
                return;
            }
            
            // Notification de déconnexion d'un peer
            if (msg.type == "peer-disconnected")
            {
                Debug.Log($"[VRNet] Peer disconnected: {msg.senderId}");
                OnPeerDisconnected?.Invoke(msg.senderId);
                return;
            }
            
            // Ignorer nos propres messages
            if (msg.senderId == LocalId)
                return;
            
            // Passer le message aux listeners
            OnMessageReceived?.Invoke(msg);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VRNet] Parse error: {e.Message}\nJSON: {json}");
        }
    }
    
    #endregion
    
    #region Public API - Sending Messages
    
    /// <summary>
    /// Envoie un message texte à tous les clients connectés.
    /// </summary>
    public async void Send(string type, string data = "")
    {
        if (_websocket == null || _websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("[VRNet] Cannot send: not connected");
            return;
        }
        
        var msg = new NetworkMessage
        {
            type = type,
            senderId = LocalId,
            data = data
        };
        
        string json = JsonUtility.ToJson(msg);
        await _websocket.SendText(json);
    }
    
    /// <summary>
    /// Envoie un message avec un payload sérialisé en JSON.
    /// </summary>
    public void Send<T>(string type, T payload)
    {
        Send(type, JsonUtility.ToJson(payload));
    }
    
    /// <summary>
    /// Vérifie si la connexion est active.
    /// </summary>
    public bool IsConnectionOpen()
    {
        return _websocket != null && _websocket.State == WebSocketState.Open;
    }
    
    #endregion
}

/// <summary>
/// Structure de base pour tous les messages réseau.
/// </summary>
[Serializable]
public class NetworkMessage
{
    public string type;
    public string senderId;
    public string data;
}
