using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VRMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;        // Panel principal "Public Rooms"
    public GameObject createRoomPanel;  // Panel "Create Room"
    
    [Header("Main Panel - Header")]
    public Button refreshButton;
    public TextMeshProUGUI titleText;
    
    [Header("Main Panel - Join")]
    public TMP_InputField roomCodeInput;
    public Button quickJoinButton;
    
    [Header("Main Panel - Room List")]
    public Transform roomListContainer;
    public GameObject roomListItemPrefab;
    
    [Header("Main Panel - Footer")]
    public Button newRoomButton;
    
    [Header("Create Panel - Fields")]
    public TMP_InputField roomNameInput;
    public TMP_InputField maxPlayersInput;
    public TextMeshProUGUI maxPlayersValueText;
    public Slider maxPlayersSlider;
    
    [Header("Create Panel - Buttons")]
    public Button createButton;
    public Button cancelButton;
    
    [Header("Status")]
    public TextMeshProUGUI statusText;
    
    [Header("In Room Panel")]
    public GameObject inRoomPanel;
    public TextMeshProUGUI currentRoomNameText;
    public TextMeshProUGUI currentRoomCodeText;
    public TextMeshProUGUI currentPlayerCountText;
    public Transform playerListContainer;
    public GameObject playerListItemPrefab;
    public Button leaveRoomButton;
    
    // Liste des items de room instanciés
    private List<GameObject> roomListItems = new List<GameObject>();
    private List<GameObject> playerListItems = new List<GameObject>();
    
    void Start()
    {
        // Boutons Main Panel
        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshRooms);
        if (quickJoinButton != null)
            quickJoinButton.onClick.AddListener(OnQuickJoin);
        if (newRoomButton != null)
            newRoomButton.onClick.AddListener(OnNewRoomClicked);
        
        // Boutons Create Panel
        if (createButton != null)
            createButton.onClick.AddListener(OnCreateRoom);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelCreate);
        
        // Slider max players
        if (maxPlayersSlider != null)
        {
            maxPlayersSlider.onValueChanged.AddListener(OnMaxPlayersChanged);
            maxPlayersSlider.minValue = 2;
            maxPlayersSlider.maxValue = 20;
            maxPlayersSlider.value = 10;
            OnMaxPlayersChanged(10);
        }
        
        // Bouton Leave Room
        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(OnLeaveRoom);
        
        // Events réseau
        VRNetworkManager.OnConnected += OnConnected;
        VRNetworkManager.OnDisconnected += OnDisconnected;
        
        // Events room
        VRRoomManager.OnRoomCreated += OnRoomCreated;
        VRRoomManager.OnRoomJoined += OnRoomJoined;
        VRRoomManager.OnRoomLeft += OnRoomLeft;
        VRRoomManager.OnRoomError += OnRoomError;
        VRRoomManager.OnRoomListUpdated += OnRoomListUpdated;
        VRRoomManager.OnPlayerJoined += OnPlayerChanged;
        VRRoomManager.OnPlayerLeft += OnPlayerLeftRoom;
        
        // État initial
        ShowMainPanel();
        SetStatus("Connexion...");
    }
    
    void OnDestroy()
    {
        // Nettoyer les listeners
        if (refreshButton != null) refreshButton.onClick.RemoveAllListeners();
        if (quickJoinButton != null) quickJoinButton.onClick.RemoveAllListeners();
        if (newRoomButton != null) newRoomButton.onClick.RemoveAllListeners();
        if (createButton != null) createButton.onClick.RemoveAllListeners();
        if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
        if (maxPlayersSlider != null) maxPlayersSlider.onValueChanged.RemoveAllListeners();
        if (leaveRoomButton != null) leaveRoomButton.onClick.RemoveAllListeners();
        
        // Events
        VRNetworkManager.OnConnected -= OnConnected;
        VRNetworkManager.OnDisconnected -= OnDisconnected;
        VRRoomManager.OnRoomCreated -= OnRoomCreated;
        VRRoomManager.OnRoomJoined -= OnRoomJoined;
        VRRoomManager.OnRoomLeft -= OnRoomLeft;
        VRRoomManager.OnRoomError -= OnRoomError;
        VRRoomManager.OnRoomListUpdated -= OnRoomListUpdated;
        VRRoomManager.OnPlayerJoined -= OnPlayerChanged;
        VRRoomManager.OnPlayerLeft -= OnPlayerLeftRoom;
    }
    
    #region Button Actions
    
    void OnRefreshRooms()
    {
        SetStatus("Actualisation...");
        VRRoomManager.Instance?.RequestRoomList();
    }
    
    void OnQuickJoin()
    {
        string code = roomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Entrez un code !");
            return;
        }
        
        SetStatus("Connexion...");
        VRRoomManager.Instance?.JoinRoom(code);
    }
    
    void OnNewRoomClicked()
    {
        ShowCreatePanel();
    }
    
    void OnCreateRoom()
    {
        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "Room " + Random.Range(1000, 9999);
        }
        
        int maxPlayers = maxPlayersSlider != null ? (int)maxPlayersSlider.value : 10;
        
        // Mettre à jour le max players dans le RoomManager
        if (VRRoomManager.Instance != null)
        {
            VRRoomManager.Instance.maxPlayersPerRoom = maxPlayers;
        }
        
        SetStatus("Création...");
        VRRoomManager.Instance?.CreateRoom(RoomType.Lobby, roomName);
    }
    
    void OnCancelCreate()
    {
        ShowMainPanel();
    }
    
    void OnMaxPlayersChanged(float value)
    {
        int intValue = (int)value;
        if (maxPlayersValueText != null)
        {
            maxPlayersValueText.text = intValue.ToString();
        }
        if (maxPlayersInput != null)
        {
            maxPlayersInput.text = intValue.ToString();
        }
    }
    
    void OnLeaveRoom()
    {
        VRRoomManager.Instance?.LeaveRoom();
    }
    
    void OnJoinRoomFromList(string roomId)
    {
        SetStatus("Connexion...");
        VRRoomManager.Instance?.JoinRoom(roomId);
    }
    
    #endregion
    
    #region Network Events
    
    void OnConnected()
    {
        SetStatus("Connecté !");
        VRRoomManager.Instance?.RequestRoomList();
    }
    
    void OnDisconnected()
    {
        SetStatus("Déconnecté");
        ShowMainPanel();
    }
    
    #endregion
    
    #region Room Events
    
    void OnRoomCreated(string roomId)
    {
        ShowInRoomPanel();
    }
    
    void OnRoomJoined(string roomId)
    {
        ShowInRoomPanel();
    }
    
    void OnRoomLeft()
    {
        ShowMainPanel();
        SetStatus("Room quittée");
    }
    
    void OnRoomError(string error)
    {
        SetStatus($"Erreur: {error}");
    }
    
    void OnRoomListUpdated(Dictionary<string, RoomInfo> rooms)
    {
        RefreshRoomList(rooms);
        SetStatus($"{rooms.Count} room(s) disponible(s)");
    }
    
    void OnPlayerChanged(VRPlayerData player)
    {
        UpdateInRoomPanel();
    }
    
    void OnPlayerLeftRoom(string playerId)
    {
        UpdateInRoomPanel();
    }
    
    #endregion
    
    #region UI Updates
    
    void ShowMainPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (createRoomPanel != null) createRoomPanel.SetActive(false);
        if (inRoomPanel != null) inRoomPanel.SetActive(false);
        
        // Reset inputs
        if (roomCodeInput != null) roomCodeInput.text = "";
    }
    
    void ShowCreatePanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (createRoomPanel != null) createRoomPanel.SetActive(true);
        if (inRoomPanel != null) inRoomPanel.SetActive(false);
        
        // Reset inputs
        if (roomNameInput != null) roomNameInput.text = "";
        if (maxPlayersSlider != null) maxPlayersSlider.value = 10;
    }
    
    void ShowInRoomPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (createRoomPanel != null) createRoomPanel.SetActive(false);
        if (inRoomPanel != null) inRoomPanel.SetActive(true);
        
        UpdateInRoomPanel();
    }
    
    void UpdateInRoomPanel()
    {
        if (VRRoomManager.Instance == null) return;
        
        // Infos de la room
        if (currentRoomCodeText != null)
            currentRoomCodeText.text = $"Code: {VRRoomManager.Instance.CurrentRoomId}";
        
        if (currentPlayerCountText != null)
            currentPlayerCountText.text = $"Joueurs: {VRRoomManager.Instance.PlayerCount}";
        
        // Liste des joueurs
        RefreshPlayerList();
    }
    
    void RefreshRoomList(Dictionary<string, RoomInfo> rooms)
    {
        // Supprimer les anciens items
        foreach (var item in roomListItems)
        {
            Destroy(item);
        }
        roomListItems.Clear();
        
        if (roomListContainer == null || roomListItemPrefab == null) return;
        
        // Créer les nouveaux items
        foreach (var kvp in rooms)
        {
            RoomInfo room = kvp.Value;
            
            GameObject item = Instantiate(roomListItemPrefab, roomListContainer);
            roomListItems.Add(item);
            
            // Configurer l'item
            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0)
            {
                texts[0].text = $"{room.roomName}";
            }
            if (texts.Length > 1)
            {
                texts[1].text = $"{room.playerCount}/{room.maxPlayers}";
            }
            
            // Bouton pour rejoindre
            var button = item.GetComponent<Button>();
            if (button == null)
            {
                button = item.AddComponent<Button>();
            }
            
            string roomId = room.roomId;
            button.onClick.AddListener(() => OnJoinRoomFromList(roomId));
            
            // Désactiver si plein
            if (room.playerCount >= room.maxPlayers)
            {
                button.interactable = false;
                var colors = button.colors;
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                button.colors = colors;
            }
        }
    }
    
    void RefreshPlayerList()
    {
        // Supprimer les anciens items
        foreach (var item in playerListItems)
        {
            Destroy(item);
        }
        playerListItems.Clear();
        
        if (playerListContainer == null || playerListItemPrefab == null) return;
        if (VRRoomManager.Instance == null) return;
        
        var players = VRRoomManager.Instance.GetPlayers();
        
        foreach (var player in players)
        {
            GameObject item = Instantiate(playerListItemPrefab, playerListContainer);
            playerListItems.Add(item);
            
            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                string prefix = player.isHost ? "★ " : "• ";
                string suffix = player.playerId == VRNetworkManager.LocalId ? " (Vous)" : "";
                text.text = $"{prefix}{player.playerName}{suffix}";
            }
        }
    }
    
    void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[VRMenuUI] {message}");
    }
    
    #endregion
}