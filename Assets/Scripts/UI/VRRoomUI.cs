using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Interface utilisateur VR pour gérer les rooms.
/// Compatible avec les Canvas World Space pour VR.
/// </summary>
public class VRRoomUI : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Panel principal du menu (lobby)")]
    public GameObject menuPanel;
    
    [Tooltip("Panel affiché quand dans une room")]
    public GameObject roomPanel;
    
    [Tooltip("Panel de la liste des rooms disponibles")]
    public GameObject roomListPanel;
    
    [Header("Menu Elements")]
    public Button createRoomAButton;
    public Button createRoomBButton;
    public Button joinRoomButton;
    public Button showRoomListButton;
    public TMP_InputField roomCodeInput;
    public TMP_InputField playerNameInput;
    public TextMeshProUGUI statusText;
    
    [Header("Room Panel Elements")]
    public TextMeshProUGUI roomCodeText;
    public TextMeshProUGUI roomTypeText;
    public TextMeshProUGUI playerCountText;
    public TextMeshProUGUI playerListText;
    public Button leaveRoomButton;
    public Button teleportToLobbyButton;
    public Button teleportToRoomAButton;
    public Button teleportToRoomBButton;
    
    [Header("Room List Elements")]
    public Transform roomListContent;
    public GameObject roomListItemPrefab;
    public Button closeRoomListButton;
    public Button refreshRoomListButton;
    
    [Header("Connection Status")]
    public TextMeshProUGUI connectionStatusText;
    public Image connectionIndicator;
    public Color connectedColor = Color.green;
    public Color disconnectedColor = Color.red;
    public Color connectingColor = Color.yellow;
    
    [Header("VR Settings")]
    [Tooltip("Distance du Canvas par rapport au joueur")]
    public float canvasDistance = 2f;
    
    [Tooltip("Suivre la tête du joueur")]
    public bool followHead = true;
    
    [Tooltip("Hauteur du Canvas")]
    public float canvasHeight = 1.5f;
    
    private Transform _playerHead;
    private List<GameObject> _roomListItems = new List<GameObject>();
    
    void Start()
    {
        // Setup des boutons
        SetupButtonListeners();
        
        // Events réseau
        VRNetworkManager.OnConnected += OnNetworkConnected;
        VRNetworkManager.OnDisconnected += OnNetworkDisconnected;
        VRNetworkManager.OnConnectionError += OnConnectionError;
        
        // Events room
        VRRoomManager.OnRoomCreated += OnRoomCreated;
        VRRoomManager.OnRoomJoined += OnRoomJoined;
        VRRoomManager.OnRoomLeft += OnRoomLeft;
        VRRoomManager.OnRoomError += OnRoomError;
        VRRoomManager.OnPlayerJoined += OnPlayerCountChanged;
        VRRoomManager.OnPlayerLeft += OnPlayerCountChangedLeft;
        VRRoomManager.OnRoomListUpdated += OnRoomListUpdated;
        VRRoomManager.OnRoomTypeChanged += OnRoomTypeChanged;
        
        // Events game
        VRGameManager.OnLocalPlayerSpawned += OnLocalPlayerSpawned;
        
        // Charger le nom du joueur sauvegardé
        if (playerNameInput != null)
        {
            playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Player");
        }
        
        // État initial
        ShowMenu();
        UpdateConnectionStatus(false, "Connecting...");
    }
    
    void OnDestroy()
    {
        RemoveButtonListeners();
        
        VRNetworkManager.OnConnected -= OnNetworkConnected;
        VRNetworkManager.OnDisconnected -= OnNetworkDisconnected;
        VRNetworkManager.OnConnectionError -= OnConnectionError;
        
        VRRoomManager.OnRoomCreated -= OnRoomCreated;
        VRRoomManager.OnRoomJoined -= OnRoomJoined;
        VRRoomManager.OnRoomLeft -= OnRoomLeft;
        VRRoomManager.OnRoomError -= OnRoomError;
        VRRoomManager.OnPlayerJoined -= OnPlayerCountChanged;
        VRRoomManager.OnPlayerLeft -= OnPlayerCountChangedLeft;
        VRRoomManager.OnRoomListUpdated -= OnRoomListUpdated;
        VRRoomManager.OnRoomTypeChanged -= OnRoomTypeChanged;
        
        VRGameManager.OnLocalPlayerSpawned -= OnLocalPlayerSpawned;
    }
    
    void Update()
    {
        // Faire suivre le Canvas à la tête du joueur si configuré
        if (followHead && _playerHead != null && menuPanel.activeSelf)
        {
            UpdateCanvasPosition();
        }
    }
    
    void SetupButtonListeners()
    {
        if (createRoomAButton != null)
            createRoomAButton.onClick.AddListener(OnCreateRoomA);
        
        if (createRoomBButton != null)
            createRoomBButton.onClick.AddListener(OnCreateRoomB);
        
        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(OnJoinRoom);
        
        if (showRoomListButton != null)
            showRoomListButton.onClick.AddListener(OnShowRoomList);
        
        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(OnLeaveRoom);
        
        if (teleportToLobbyButton != null)
            teleportToLobbyButton.onClick.AddListener(OnTeleportToLobby);
        
        if (teleportToRoomAButton != null)
            teleportToRoomAButton.onClick.AddListener(OnTeleportToRoomA);
        
        if (teleportToRoomBButton != null)
            teleportToRoomBButton.onClick.AddListener(OnTeleportToRoomB);
        
        if (closeRoomListButton != null)
            closeRoomListButton.onClick.AddListener(OnCloseRoomList);
        
        if (refreshRoomListButton != null)
            refreshRoomListButton.onClick.AddListener(OnRefreshRoomList);
        
        if (playerNameInput != null)
            playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);
    }
    
    void RemoveButtonListeners()
    {
        if (createRoomAButton != null)
            createRoomAButton.onClick.RemoveListener(OnCreateRoomA);
        
        if (createRoomBButton != null)
            createRoomBButton.onClick.RemoveListener(OnCreateRoomB);
        
        if (joinRoomButton != null)
            joinRoomButton.onClick.RemoveListener(OnJoinRoom);
        
        if (showRoomListButton != null)
            showRoomListButton.onClick.RemoveListener(OnShowRoomList);
        
        if (leaveRoomButton != null)
            leaveRoomButton.onClick.RemoveListener(OnLeaveRoom);
        
        if (teleportToLobbyButton != null)
            teleportToLobbyButton.onClick.RemoveListener(OnTeleportToLobby);
        
        if (teleportToRoomAButton != null)
            teleportToRoomAButton.onClick.RemoveListener(OnTeleportToRoomA);
        
        if (teleportToRoomBButton != null)
            teleportToRoomBButton.onClick.RemoveListener(OnTeleportToRoomB);
        
        if (closeRoomListButton != null)
            closeRoomListButton.onClick.RemoveListener(OnCloseRoomList);
        
        if (refreshRoomListButton != null)
            refreshRoomListButton.onClick.RemoveListener(OnRefreshRoomList);
        
        if (playerNameInput != null)
            playerNameInput.onEndEdit.RemoveListener(OnPlayerNameChanged);
    }
    
    #region UI Actions
    
    void OnCreateRoomA()
    {
        SavePlayerName();
        SetStatus("Creating Meeting Room A...");
        VRRoomManager.Instance.CreateRoom(RoomType.MeetingRoomA);
    }
    
    void OnCreateRoomB()
    {
        SavePlayerName();
        SetStatus("Creating Meeting Room B...");
        VRRoomManager.Instance.CreateRoom(RoomType.MeetingRoomB);
    }
    
    void OnJoinRoom()
    {
        if (roomCodeInput == null) return;
        
        string code = roomCodeInput.text.Trim().ToUpper();
        
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Please enter a room code");
            return;
        }
        
        SavePlayerName();
        SetStatus("Joining...");
        VRRoomManager.Instance.JoinRoom(code);
    }
    
    void OnLeaveRoom()
    {
        VRRoomManager.Instance.LeaveRoom();
    }
    
    void OnShowRoomList()
    {
        if (roomListPanel != null)
        {
            roomListPanel.SetActive(true);
            VRRoomManager.Instance.RequestRoomList();
        }
    }
    
    void OnCloseRoomList()
    {
        if (roomListPanel != null)
        {
            roomListPanel.SetActive(false);
        }
    }
    
    void OnRefreshRoomList()
    {
        VRRoomManager.Instance.RequestRoomList();
    }
    
    void OnTeleportToLobby()
    {
        VRRoomManager.Instance.TeleportToRoomType(RoomType.Lobby);
    }
    
    void OnTeleportToRoomA()
    {
        VRRoomManager.Instance.TeleportToRoomType(RoomType.MeetingRoomA);
    }
    
    void OnTeleportToRoomB()
    {
        VRRoomManager.Instance.TeleportToRoomType(RoomType.MeetingRoomB);
    }
    
    void OnPlayerNameChanged(string newName)
    {
        SavePlayerName();
    }
    
    void SavePlayerName()
    {
        if (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text))
        {
            VRRoomManager.Instance.SetPlayerName(playerNameInput.text.Trim());
        }
    }
    
    #endregion
    
    #region Network Events
    
    void OnNetworkConnected()
    {
        UpdateConnectionStatus(true, "Connected");
        SetStatus("Connected! Create or join a room.");
    }
    
    void OnNetworkDisconnected()
    {
        UpdateConnectionStatus(false, "Disconnected");
        SetStatus("Disconnected from server");
        ShowMenu();
    }
    
    void OnConnectionError(string error)
    {
        UpdateConnectionStatus(false, "Connection Error");
        SetStatus($"Error: {error}");
    }
    
    #endregion
    
    #region Room Events
    
    void OnRoomCreated(string roomId)
    {
        ShowRoom(roomId);
        SetStatus($"Room created! Code: {roomId}");
    }
    
    void OnRoomJoined(string roomId)
    {
        ShowRoom(roomId);
        SetStatus($"Joined room: {roomId}");
    }
    
    void OnRoomLeft()
    {
        ShowMenu();
        SetStatus("Left room");
    }
    
    void OnRoomError(string error)
    {
        SetStatus($"Error: {error}");
    }
    
    void OnPlayerCountChanged(VRPlayerData player)
    {
        UpdatePlayerCount();
        UpdatePlayerList();
    }
    
    void OnPlayerCountChangedLeft(string playerId)
    {
        UpdatePlayerCount();
        UpdatePlayerList();
    }
    
    void OnRoomListUpdated(Dictionary<string, RoomInfo> rooms)
    {
        RefreshRoomListUI(rooms);
    }
    
    void OnRoomTypeChanged(RoomType roomType)
    {
        if (roomTypeText != null)
        {
            roomTypeText.text = $"Location: {GetRoomTypeName(roomType)}";
        }
        
        UpdateTeleportButtons(roomType);
    }
    
    #endregion
    
    #region Game Events
    
    void OnLocalPlayerSpawned(GameObject player)
    {
        // Trouver la tête du joueur pour le suivi du Canvas
        var cameras = player.GetComponentsInChildren<Camera>();
        foreach (var cam in cameras)
        {
            if (cam.CompareTag("MainCamera"))
            {
                _playerHead = cam.transform;
                break;
            }
        }
    }
    
    #endregion
    
    #region UI Updates
    
    void ShowMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (roomListPanel != null) roomListPanel.SetActive(false);
        
        if (roomCodeInput != null) roomCodeInput.text = "";
    }
    
    void ShowRoom(string roomId)
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(true);
        if (roomListPanel != null) roomListPanel.SetActive(false);
        
        if (roomCodeText != null)
            roomCodeText.text = $"Room Code: {roomId}";
        
        if (roomTypeText != null)
            roomTypeText.text = $"Location: {GetRoomTypeName(VRRoomManager.Instance.CurrentRoomType)}";
        
        UpdatePlayerCount();
        UpdatePlayerList();
        UpdateTeleportButtons(VRRoomManager.Instance.CurrentRoomType);
    }
    
    void UpdatePlayerCount()
    {
        if (playerCountText != null && VRRoomManager.Instance != null)
        {
            playerCountText.text = $"Players: {VRRoomManager.Instance.PlayerCount}";
        }
    }
    
    void UpdatePlayerList()
    {
        if (playerListText != null && VRRoomManager.Instance != null)
        {
            var players = VRRoomManager.Instance.GetPlayers();
            var playerNames = new System.Text.StringBuilder();
            
            foreach (var player in players)
            {
                string prefix = player.isHost ? "★ " : "• ";
                string suffix = player.playerId == VRNetworkManager.LocalId ? " (You)" : "";
                playerNames.AppendLine($"{prefix}{player.playerName}{suffix}");
            }
            
            playerListText.text = playerNames.ToString();
        }
    }
    
    void UpdateConnectionStatus(bool connected, string statusMessage)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = statusMessage;
        }
        
        if (connectionIndicator != null)
        {
            connectionIndicator.color = connected ? connectedColor : disconnectedColor;
        }
    }
    
    void UpdateTeleportButtons(RoomType currentType)
    {
        // Désactiver le bouton de téléportation vers la salle actuelle
        if (teleportToLobbyButton != null)
            teleportToLobbyButton.interactable = currentType != RoomType.Lobby;
        
        if (teleportToRoomAButton != null)
            teleportToRoomAButton.interactable = currentType != RoomType.MeetingRoomA;
        
        if (teleportToRoomBButton != null)
            teleportToRoomBButton.interactable = currentType != RoomType.MeetingRoomB;
    }
    
    void RefreshRoomListUI(Dictionary<string, RoomInfo> rooms)
    {
        // Nettoyer la liste existante
        foreach (var item in _roomListItems)
        {
            Destroy(item);
        }
        _roomListItems.Clear();
        
        if (roomListContent == null || roomListItemPrefab == null)
            return;
        
        // Créer les éléments de liste
        foreach (var kvp in rooms)
        {
            var roomInfo = kvp.Value;
            var item = Instantiate(roomListItemPrefab, roomListContent);
            
            // Configurer l'affichage
            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0)
            {
                texts[0].text = $"{roomInfo.roomName} ({roomInfo.roomId})";
                if (texts.Length > 1)
                {
                    texts[1].text = $"{GetRoomTypeName(roomInfo.roomType)} - {roomInfo.playerCount}/{roomInfo.maxPlayers}";
                }
            }
            
            // Configurer le bouton pour rejoindre
            var button = item.GetComponentInChildren<Button>();
            if (button != null)
            {
                string roomId = roomInfo.roomId;
                button.onClick.AddListener(() => JoinRoomFromList(roomId));
                button.interactable = roomInfo.playerCount < roomInfo.maxPlayers;
            }
            
            _roomListItems.Add(item);
        }
    }
    
    void JoinRoomFromList(string roomId)
    {
        OnCloseRoomList();
        SavePlayerName();
        SetStatus("Joining...");
        VRRoomManager.Instance.JoinRoom(roomId);
    }
    
    void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
    
    void UpdateCanvasPosition()
    {
        if (_playerHead == null) return;
        
        // Positionner le Canvas devant le joueur
        Vector3 forward = _playerHead.forward;
        forward.y = 0;
        forward.Normalize();
        
        Vector3 targetPosition = _playerHead.position + forward * canvasDistance;
        targetPosition.y = canvasHeight;
        
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 2f);
        
        // Faire face au joueur
        Vector3 lookDirection = _playerHead.position - transform.position;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(-lookDirection);
        }
    }
    
    string GetRoomTypeName(RoomType type)
    {
        switch (type)
        {
            case RoomType.Lobby: return "Lobby";
            case RoomType.MeetingRoomA: return "Meeting Room A";
            case RoomType.MeetingRoomB: return "Meeting Room B";
            default: return type.ToString();
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Affiche ou masque l'UI.
    /// </summary>
    public void ToggleUI()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
    
    /// <summary>
    /// Force l'affichage du menu principal.
    /// </summary>
    public void ForceShowMenu()
    {
        ShowMenu();
        gameObject.SetActive(true);
    }
    
    #endregion
}
