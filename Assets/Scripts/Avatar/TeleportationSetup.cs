using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Connecte automatiquement toutes les Teleportation Areas au Teleportation Provider
/// et assigne l'XR Interaction Manager aux Interactors quand le joueur VR spawn au runtime.
/// </summary>
public class TeleportationSetup : MonoBehaviour
{
    public static TeleportationSetup Instance { get; private set; }
    
    [Header("References")]
    [Tooltip("XR Interaction Manager dans la scène (auto-créé si vide)")]
    public XRInteractionManager interactionManager;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    private TeleportationProvider _teleportationProvider;
    
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Trouver ou créer l'XR Interaction Manager
        if (interactionManager == null)
        {
            interactionManager = FindFirstObjectByType<XRInteractionManager>();
            
            if (interactionManager == null)
            {
                // Créer un XR Interaction Manager
                GameObject managerObj = new GameObject("XR Interaction Manager");
                interactionManager = managerObj.AddComponent<XRInteractionManager>();
                
                if (showDebugLogs)
                    Debug.Log("[TeleportSetup] Created XR Interaction Manager");
            }
        }
    }
    
    void OnEnable()
    {
        // Écouter quand le joueur local spawn
        VRGameManager.OnLocalPlayerSpawned += OnLocalPlayerSpawned;
    }
    
    void OnDisable()
    {
        VRGameManager.OnLocalPlayerSpawned -= OnLocalPlayerSpawned;
    }
    
    void OnLocalPlayerSpawned(GameObject localPlayer)
    {
        if (showDebugLogs)
            Debug.Log("[TeleportSetup] Local player spawned, setting up teleportation...");
        
        // Assigner l'Interaction Manager à tous les Interactors du joueur
        AssignInteractionManager(localPlayer);
        
        // Trouver le Teleportation Provider dans le joueur
        _teleportationProvider = localPlayer.GetComponentInChildren<TeleportationProvider>();
        
        if (_teleportationProvider == null)
        {
            Debug.LogError("[TeleportSetup] No TeleportationProvider found in local player!");
            return;
        }
        
        if (showDebugLogs)
            Debug.Log($"[TeleportSetup] Found TeleportationProvider: {_teleportationProvider.gameObject.name}");
        
        // Connecter toutes les Teleportation Areas
        ConnectAllTeleportationAreas();
    }
    
    void AssignInteractionManager(GameObject localPlayer)
    {
        if (interactionManager == null)
        {
            Debug.LogError("[TeleportSetup] No XR Interaction Manager available!");
            return;
        }
        
        // Trouver tous les XRBaseInteractor (inclut Ray, Direct, Poke, etc.)
        var interactors = localPlayer.GetComponentsInChildren<XRBaseInteractor>(true);
        
        foreach (var interactor in interactors)
        {
            interactor.interactionManager = interactionManager;
            
            if (showDebugLogs)
                Debug.Log($"[TeleportSetup] Assigned InteractionManager to: {interactor.gameObject.name}");
        }
        
        // Aussi pour les Interactables dans le joueur (si il y en a)
        var interactables = localPlayer.GetComponentsInChildren<XRBaseInteractable>(true);
        
        foreach (var interactable in interactables)
        {
            interactable.interactionManager = interactionManager;
        }
        
        if (showDebugLogs)
            Debug.Log($"[TeleportSetup] Assigned InteractionManager to {interactors.Length} interactors");
    }
    
    void ConnectAllTeleportationAreas()
    {
        // Trouver toutes les Teleportation Areas dans la scène
        TeleportationArea[] areas = FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None);
        
        foreach (var area in areas)
        {
            area.teleportationProvider = _teleportationProvider;
            
            // Aussi assigner l'Interaction Manager
            if (interactionManager != null)
            {
                area.interactionManager = interactionManager;
            }
            
            if (showDebugLogs)
                Debug.Log($"[TeleportSetup] Connected TeleportationArea: {area.gameObject.name}");
        }
        
        // Trouver aussi les Teleportation Anchors
        TeleportationAnchor[] anchors = FindObjectsByType<TeleportationAnchor>(FindObjectsSortMode.None);
        
        foreach (var anchor in anchors)
        {
            anchor.teleportationProvider = _teleportationProvider;
            
            if (interactionManager != null)
            {
                anchor.interactionManager = interactionManager;
            }
            
            if (showDebugLogs)
                Debug.Log($"[TeleportSetup] Connected TeleportationAnchor: {anchor.gameObject.name}");
        }
        
        if (showDebugLogs)
            Debug.Log($"[TeleportSetup] Setup complete! Connected {areas.Length} areas and {anchors.Length} anchors");
    }
    
    /// <summary>
    /// Appelé pour reconnecter les Teleportation Areas (utile si de nouvelles zones sont ajoutées)
    /// </summary>
    public void RefreshTeleportationAreas()
    {
        if (_teleportationProvider != null)
        {
            ConnectAllTeleportationAreas();
        }
    }
    
    /// <summary>
    /// Retourne le Teleportation Provider actuel
    /// </summary>
    public TeleportationProvider GetTeleportationProvider()
    {
        return _teleportationProvider;
    }
    
    /// <summary>
    /// Retourne l'XR Interaction Manager
    /// </summary>
    public XRInteractionManager GetInteractionManager()
    {
        return interactionManager;
    }
}