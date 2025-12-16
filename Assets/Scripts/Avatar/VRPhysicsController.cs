using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Gère la physique du joueur VR : gravité, collisions, et synchronisation
/// de la position du Character Controller avec le casque VR.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class VRPhysicsController : MonoBehaviour
{
    [Header("Gravity Settings")]
    [Tooltip("Activer la gravité")]
    public bool useGravity = true;
    
    [Tooltip("Force de gravité")]
    public float gravity = -9.81f;
    
    [Tooltip("Vitesse maximale de chute")]
    public float maxFallSpeed = -20f;
    
    [Header("Ground Check")]
    [Tooltip("Distance pour vérifier le sol")]
    public float groundCheckDistance = 0.1f;
    
    [Tooltip("Layers considérés comme sol")]
    public LayerMask groundLayers = ~0; // Tout par défaut
    
    [Header("Character Controller Sync")]
    [Tooltip("Synchroniser le Character Controller avec la position de la tête VR")]
    public bool syncWithHead = true;
    
    [Tooltip("Hauteur minimale du Character Controller")]
    public float minHeight = 0.5f;
    
    [Tooltip("Hauteur maximale du Character Controller")]
    public float maxHeight = 2.5f;
    
    [Header("Collision Settings")]
    [Tooltip("Pousser le joueur hors des murs")]
    public bool pushOutOfWalls = true;
    
    [Tooltip("Force de poussée hors des murs")]
    public float wallPushForce = 0.5f;
    
    [Header("References")]
    [Tooltip("Camera/Tête du joueur (auto-détecté si vide)")]
    public Transform headTransform;
    
    [Header("Debug")]
    public bool showDebugGizmos = false;
    
    // Components
    private CharacterController _characterController;
    private XROrigin _xrOrigin;
    
    // State
    private Vector3 _velocity;
    private bool _isGrounded;
    private Vector3 _lastHeadLocalPosition;
    
    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _xrOrigin = GetComponent<XROrigin>();
        
        // Auto-détecter la tête
        if (headTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                headTransform = cam.transform;
            }
        }
        
        if (headTransform != null)
        {
            _lastHeadLocalPosition = transform.InverseTransformPoint(headTransform.position);
        }
        
        // Configuration initiale du Character Controller
        ConfigureCharacterController();
    }
    
    void Update()
    {
        // Synchroniser avec la position de la tête VR
        if (syncWithHead && headTransform != null)
        {
            SyncCharacterControllerWithHead();
        }
        
        // Vérifier le sol
        CheckGround();
        
        // Appliquer la gravité
        if (useGravity)
        {
            ApplyGravity();
        }
        
        // Appliquer le mouvement vertical
        if (_velocity.y != 0)
        {
            _characterController.Move(new Vector3(0, _velocity.y * Time.deltaTime, 0));
        }
    }
    
    void ConfigureCharacterController()
    {
        if (_characterController == null) return;
        
        // Configuration recommandée pour VR
        _characterController.skinWidth = 0.08f;
        _characterController.minMoveDistance = 0.001f;
        _characterController.stepOffset = 0.3f;
        _characterController.slopeLimit = 45f;
    }
    
    void SyncCharacterControllerWithHead()
    {
        if (headTransform == null || _characterController == null) return;
        
        // Position de la tête en espace local
        Vector3 headLocalPos = transform.InverseTransformPoint(headTransform.position);
        
        // Calculer le mouvement horizontal de la tête
        Vector3 headDelta = headLocalPos - _lastHeadLocalPosition;
        headDelta.y = 0; // Ignorer le mouvement vertical
        
        // Convertir en espace monde
        Vector3 worldDelta = transform.TransformDirection(headDelta);
        
        // Déplacer le Character Controller pour suivre la tête
        if (worldDelta.magnitude > 0.001f)
        {
            _characterController.Move(worldDelta);
            
            // Compenser pour garder la tête au bon endroit
            Vector3 compensation = -worldDelta;
            compensation.y = 0;
            
            // Déplacer le XR Origin pour compenser
            if (_xrOrigin != null)
            {
                // On ajuste la position du rig pour que la caméra reste stable
            }
        }
        
        // Ajuster la hauteur du Character Controller selon la tête
        float headHeight = headLocalPos.y;
        float targetHeight = Mathf.Clamp(headHeight + 0.2f, minHeight, maxHeight);
        
        if (Mathf.Abs(_characterController.height - targetHeight) > 0.05f)
        {
            _characterController.height = targetHeight;
            _characterController.center = new Vector3(0, targetHeight / 2f, 0);
        }
        
        _lastHeadLocalPosition = headLocalPos;
    }
    
    void CheckGround()
    {
        // Utiliser le Character Controller pour vérifier le sol
        _isGrounded = _characterController.isGrounded;
        
        // Double vérification avec raycast pour plus de précision
        if (!_isGrounded)
        {
            Vector3 rayStart = transform.position + Vector3.up * 0.1f;
            _isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance + 0.1f, groundLayers);
        }
    }
    
    void ApplyGravity()
    {
        if (_isGrounded && _velocity.y < 0)
        {
            // Petite force vers le bas pour rester au sol
            _velocity.y = -2f;
        }
        else
        {
            // Appliquer la gravité
            _velocity.y += gravity * Time.deltaTime;
            
            // Limiter la vitesse de chute
            if (_velocity.y < maxFallSpeed)
            {
                _velocity.y = maxFallSpeed;
            }
        }
    }
    
    /// <summary>
    /// Téléporte le joueur à une position donnée.
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        if (_characterController == null) return;
        
        // Désactiver temporairement le Character Controller pour le téléport
        _characterController.enabled = false;
        transform.position = position;
        _characterController.enabled = true;
        
        // Reset la vélocité
        _velocity = Vector3.zero;
        
        Debug.Log($"[VRPhysics] Teleported to {position}");
    }
    
    /// <summary>
    /// Téléporte le joueur à une position avec une rotation.
    /// </summary>
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (_characterController == null) return;
        
        _characterController.enabled = false;
        transform.position = position;
        transform.rotation = rotation;
        _characterController.enabled = true;
        
        _velocity = Vector3.zero;
        
        Debug.Log($"[VRPhysics] Teleported to {position}, rotation {rotation.eulerAngles}");
    }
    
    /// <summary>
    /// Vérifie si le joueur est au sol.
    /// </summary>
    public bool IsGrounded => _isGrounded;
    
    /// <summary>
    /// Retourne la vélocité actuelle.
    /// </summary>
    public Vector3 Velocity => _velocity;
    
    /// <summary>
    /// Applique une force au joueur (pour les sauts, etc.)
    /// </summary>
    public void AddForce(Vector3 force)
    {
        _velocity += force;
    }
    
    /// <summary>
    /// Fait sauter le joueur.
    /// </summary>
    public void Jump(float jumpForce = 5f)
    {
        if (_isGrounded)
        {
            _velocity.y = jumpForce;
        }
    }
    
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Optionnel : Pousser les objets avec Rigidbody
        Rigidbody rb = hit.collider.attachedRigidbody;
        
        if (rb != null && !rb.isKinematic)
        {
            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
            rb.AddForce(pushDir * wallPushForce, ForceMode.Impulse);
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Dessiner le ground check
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 groundCheckStart = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawLine(groundCheckStart, groundCheckStart + Vector3.down * (groundCheckDistance + 0.1f));
        
        // Dessiner le Character Controller
        if (_characterController != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position + _characterController.center;
            Gizmos.DrawWireSphere(center + Vector3.up * (_characterController.height / 2 - _characterController.radius), _characterController.radius);
            Gizmos.DrawWireSphere(center - Vector3.up * (_characterController.height / 2 - _characterController.radius), _characterController.radius);
        }
    }
}