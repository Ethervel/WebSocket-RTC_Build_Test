using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Système de téléportation VR avec support des zones de téléportation
/// et des portails vers les différentes salles.
/// </summary>
public class VRTeleportationManager : MonoBehaviour
{
    public static VRTeleportationManager Instance { get; private set; }
    
    [Header("Teleportation Settings")]
    [Tooltip("Vitesse de transition lors de la téléportation")]
    public float teleportFadeDuration = 0.3f;
    
    [Tooltip("Couleur du fade")]
    public Color fadeColor = Color.black;
    
    [Header("Line Visual")]
    [Tooltip("Prefab de la ligne de téléportation")]
    public GameObject teleportLinePrefab;
    
    [Tooltip("Couleur de la ligne valide")]
    public Color validLineColor = Color.green;
    
    [Tooltip("Couleur de la ligne invalide")]
    public Color invalidLineColor = Color.red;
    
    [Header("Portal Settings")]
    [Tooltip("Activer les portails de téléportation")]
    public bool enablePortals = true;
    
    [Tooltip("Délai minimum entre deux téléportations (secondes)")]
    public float teleportCooldown = 1f;
    
    [Header("References")]
    public Transform xrOrigin;
    public Camera vrCamera;
    
    // État interne
    private float _lastTeleportTime;
    private bool _isTeleporting;
    
    // Fade overlay
    private GameObject _fadeOverlay;
    private Material _fadeMaterial;
    
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    void Start()
    {
        // Créer l'overlay de fade
        CreateFadeOverlay();
        
        // S'abonner aux événements de room
        VRRoomManager.OnRoomTypeChanged += OnRoomTypeChanged;
    }
    
    void OnDestroy()
    {
        VRRoomManager.OnRoomTypeChanged -= OnRoomTypeChanged;
    }
    
    #region Teleportation Methods
    
    /// <summary>
    /// Téléporte le joueur vers une position spécifique.
    /// </summary>
    public void TeleportTo(Vector3 position, Quaternion rotation, bool instant = false)
    {
        if (_isTeleporting) return;
        if (Time.time - _lastTeleportTime < teleportCooldown) return;
        
        if (instant)
        {
            PerformTeleport(position, rotation);
        }
        else
        {
            StartCoroutine(TeleportWithFade(position, rotation));
        }
    }
    
    /// <summary>
    /// Téléporte le joueur vers un Transform.
    /// </summary>
    public void TeleportTo(Transform target, bool instant = false)
    {
        if (target == null) return;
        TeleportTo(target.position, target.rotation, instant);
    }
    
    /// <summary>
    /// Téléporte le joueur vers une zone spécifique de la room.
    /// </summary>
    public void TeleportToRoomArea(RoomType roomType)
    {
        Transform spawnPoint = GetSpawnPointForRoom(roomType);
        if (spawnPoint != null)
        {
            TeleportTo(spawnPoint);
        }
    }
    
    System.Collections.IEnumerator TeleportWithFade(Vector3 position, Quaternion rotation)
    {
        _isTeleporting = true;
        _lastTeleportTime = Time.time;
        
        // Fade out
        yield return FadeOut();
        
        // Téléporter
        PerformTeleport(position, rotation);
        
        // Petite pause
        yield return new WaitForSeconds(0.1f);
        
        // Fade in
        yield return FadeIn();
        
        _isTeleporting = false;
    }
    
    void PerformTeleport(Vector3 position, Quaternion rotation)
    {
        if (xrOrigin == null)
        {
            xrOrigin = FindXROrigin();
        }
        
        if (xrOrigin == null)
        {
            Debug.LogError("[VRTeleport] XR Origin not found!");
            return;
        }
        
        // Calculer l'offset de la caméra
        Vector3 cameraOffset = Vector3.zero;
        if (vrCamera != null)
        {
            cameraOffset = vrCamera.transform.position - xrOrigin.position;
            cameraOffset.y = 0; // Garder uniquement l'offset horizontal
        }
        
        // Désactiver le CharacterController temporairement
        var characterController = xrOrigin.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        
        // Appliquer la nouvelle position (en compensant l'offset de la caméra)
        xrOrigin.position = position - cameraOffset;
        
        // Appliquer la rotation (seulement Y)
        Vector3 currentEuler = xrOrigin.eulerAngles;
        xrOrigin.rotation = Quaternion.Euler(currentEuler.x, rotation.eulerAngles.y, currentEuler.z);
        
        // Réactiver le CharacterController
        if (characterController != null)
        {
            characterController.enabled = true;
        }
        
        Debug.Log($"[VRTeleport] Teleported to {position}");
    }
    
    #endregion
    
    #region Fade Effects
    
    void CreateFadeOverlay()
    {
        if (vrCamera == null)
        {
            vrCamera = Camera.main;
        }
        
        if (vrCamera == null) return;
        
        // Créer un quad devant la caméra
        _fadeOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fadeOverlay.name = "TeleportFadeOverlay";
        _fadeOverlay.transform.SetParent(vrCamera.transform);
        _fadeOverlay.transform.localPosition = new Vector3(0, 0, 0.1f);
        _fadeOverlay.transform.localRotation = Quaternion.identity;
        _fadeOverlay.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        
        // Supprimer le collider
        var collider = _fadeOverlay.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        
        // Créer le matériau transparent
        _fadeMaterial = new Material(Shader.Find("UI/Default"));
        if (_fadeMaterial == null)
        {
            _fadeMaterial = new Material(Shader.Find("Unlit/Color"));
        }
        
        _fadeMaterial.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0);
        _fadeOverlay.GetComponent<MeshRenderer>().material = _fadeMaterial;
        
        // Rendre invisible au début
        _fadeOverlay.SetActive(false);
    }
    
    System.Collections.IEnumerator FadeOut()
    {
        if (_fadeOverlay == null || _fadeMaterial == null) yield break;
        
        _fadeOverlay.SetActive(true);
        
        float elapsed = 0f;
        while (elapsed < teleportFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0, 1, elapsed / teleportFadeDuration);
            _fadeMaterial.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        
        _fadeMaterial.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1);
    }
    
    System.Collections.IEnumerator FadeIn()
    {
        if (_fadeOverlay == null || _fadeMaterial == null) yield break;
        
        float elapsed = 0f;
        while (elapsed < teleportFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1, 0, elapsed / teleportFadeDuration);
            _fadeMaterial.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        
        _fadeMaterial.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0);
        _fadeOverlay.SetActive(false);
    }
    
    #endregion
    
    #region Event Handlers
    
    void OnRoomTypeChanged(RoomType roomType)
    {
        TeleportToRoomArea(roomType);
    }
    
    #endregion
    
    #region Helpers
    
    Transform FindXROrigin()
    {
        // Chercher par composant
        var xrOriginComponent = FindFirstObjectByType<XROrigin>();
        if (xrOriginComponent != null)
        {
            return xrOriginComponent.transform;
        }
        
        // Chercher par nom
        var byName = GameObject.Find("XR Origin");
        if (byName != null) return byName.transform;
        
        byName = GameObject.Find("XR Rig");
        if (byName != null) return byName.transform;
        
        // Chercher le joueur local
        if (VRGameManager.Instance != null)
        {
            var localPlayer = VRGameManager.Instance.GetLocalPlayer();
            if (localPlayer != null)
            {
                return localPlayer.transform;
            }
        }
        
        return null;
    }
    
    Transform GetSpawnPointForRoom(RoomType roomType)
    {
        if (VRGameManager.Instance == null) return null;
        
        // Accéder aux spawn points via le GameManager
        switch (roomType)
        {
            case RoomType.Lobby:
                return VRGameManager.Instance.lobbySpawnPoint;
            case RoomType.MeetingRoomA:
                return VRGameManager.Instance.roomASpawnPoint;
            case RoomType.MeetingRoomB:
                return VRGameManager.Instance.roomBSpawnPoint;
            default:
                return null;
        }
    }
    
    #endregion
}

/// <summary>
/// Zone de téléportation personnalisée.
/// Attachez ce script aux sols où la téléportation est autorisée.
/// </summary>
public class VRTeleportArea : MonoBehaviour
{
    [Tooltip("Autoriser la téléportation sur cette zone")]
    public bool isActive = true;
    
    [Tooltip("Type de room associé à cette zone")]
    public RoomType associatedRoomType = RoomType.Lobby;
    
    void Start()
    {
        // S'assurer qu'il y a un collider
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            var meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.convex = false;
        }
        
        // Ajouter le tag de téléportation si nécessaire
        gameObject.layer = LayerMask.NameToLayer("Teleport");
    }
}

/// <summary>
/// Portail de téléportation entre les salles.
/// Déclenche automatiquement une téléportation quand le joueur entre.
/// </summary>
public class VRTeleportPortal : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Type de salle vers laquelle téléporter")]
    public RoomType destinationRoomType;
    
    [Tooltip("Point de destination spécifique (optionnel)")]
    public Transform customDestination;
    
    [Header("Visual")]
    [Tooltip("Effet visuel du portail")]
    public ParticleSystem portalEffect;
    
    [Tooltip("Couleur du portail")]
    public Color portalColor = Color.cyan;
    
    [Header("Settings")]
    [Tooltip("Délai avant de pouvoir réutiliser le portail (secondes)")]
    public float cooldown = 2f;
    
    [Tooltip("Afficher un message de confirmation")]
    public bool requireConfirmation = false;
    
    private float _lastUseTime;
    private bool _playerInTrigger;
    
    void Start()
    {
        // S'assurer qu'il y a un trigger collider
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            var boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(2f, 3f, 0.5f);
        }
        else
        {
            collider.isTrigger = true;
        }
        
        // Configurer l'effet visuel
        if (portalEffect != null)
        {
            var main = portalEffect.main;
            main.startColor = portalColor;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Vérifier si c'est le joueur local
        if (!IsLocalPlayer(other)) return;
        
        _playerInTrigger = true;
        
        if (!requireConfirmation)
        {
            TryTeleport();
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (!IsLocalPlayer(other)) return;
        _playerInTrigger = false;
    }
    
    /// <summary>
    /// Tente de téléporter le joueur.
    /// </summary>
    public void TryTeleport()
    {
        if (Time.time - _lastUseTime < cooldown) return;
        
        _lastUseTime = Time.time;
        
        if (customDestination != null)
        {
            VRTeleportationManager.Instance?.TeleportTo(customDestination);
        }
        else
        {
            // Utiliser le RoomManager pour changer de zone
            VRRoomManager.Instance?.TeleportToRoomType(destinationRoomType);
        }
        
        Debug.Log($"[Portal] Teleporting to {destinationRoomType}");
    }
    
    bool IsLocalPlayer(Collider other)
    {
        // Vérifier par tag
        if (other.CompareTag("Player")) return true;
        
        // Vérifier par hiérarchie
        var localPlayer = VRGameManager.Instance?.GetLocalPlayer();
        if (localPlayer != null)
        {
            return other.transform.IsChildOf(localPlayer.transform) || 
                   other.transform == localPlayer.transform;
        }
        
        return false;
    }
}