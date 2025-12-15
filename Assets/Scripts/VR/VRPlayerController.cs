using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

// Alias pour éviter l'ambiguïté avec UnityEngine.InputSystem
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

/// <summary>
/// Contrôleur de mouvement VR avec support pour :
/// - Locomotion continue (joystick)
/// - Snap turn / Smooth turn
/// - Téléportation
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class VRPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Vitesse de déplacement")]
    public float moveSpeed = 2f;
    
    [Tooltip("Multiplicateur de sprint")]
    public float sprintMultiplier = 1.5f;
    
    [Tooltip("Appliquer la gravité")]
    public bool useGravity = true;
    
    [Tooltip("Force de gravité")]
    public float gravity = -9.81f;
    
    [Header("Rotation Settings")]
    [Tooltip("Utiliser Snap Turn (sinon Smooth Turn)")]
    public bool useSnapTurn = true;
    
    [Tooltip("Angle de Snap Turn (degrés)")]
    public float snapTurnAngle = 45f;
    
    [Tooltip("Vitesse de Smooth Turn (degrés/seconde)")]
    public float smoothTurnSpeed = 90f;
    
    [Tooltip("Seuil du joystick pour le turn")]
    public float turnThreshold = 0.5f;
    
    [Header("Input Settings")]
    [Tooltip("Main utilisée pour le mouvement (gauche recommandée)")]
    public XRNode moveHand = XRNode.LeftHand;
    
    [Tooltip("Main utilisée pour la rotation (droite recommandée)")]
    public XRNode turnHand = XRNode.RightHand;
    
    [Header("References")]
    public Transform headTransform;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // Composants
    private CharacterController _characterController;
    
    // État
    private Vector3 _velocity;
    private bool _canSnapTurn = true;
    private XRInputDevice _moveDevice;
    private XRInputDevice _turnDevice;
    
    // Input values
    private Vector2 _moveInput;
    private Vector2 _turnInput;
    
    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        
        // Trouver la caméra/tête si pas assignée
        if (headTransform == null)
        {
            var camera = GetComponentInChildren<Camera>();
            if (camera != null)
            {
                headTransform = camera.transform;
            }
        }
        
        // Initialiser les devices
        UpdateInputDevices();
    }
    
    void Update()
    {
        // Mettre à jour les devices si nécessaire
        if (!_moveDevice.isValid || !_turnDevice.isValid)
        {
            UpdateInputDevices();
        }
        
        // Lire les inputs
        ReadInputs();
        
        // Appliquer les mouvements
        HandleMovement();
        HandleRotation();
        HandleGravity();
    }
    
    void UpdateInputDevices()
    {
        var devices = new List<XRInputDevice>();
        
        InputDevices.GetDevicesAtXRNode(moveHand, devices);
        if (devices.Count > 0) _moveDevice = devices[0];
        
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(turnHand, devices);
        if (devices.Count > 0) _turnDevice = devices[0];
    }
    
    void ReadInputs()
    {
        // Lire le joystick de mouvement
        if (_moveDevice.isValid)
        {
            _moveDevice.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out _moveInput);
        }
        else
        {
            // Fallback clavier pour le debug
            _moveInput = new Vector2(
                Input.GetAxis("Horizontal"),
                Input.GetAxis("Vertical")
            );
        }
        
        // Lire le joystick de rotation
        if (_turnDevice.isValid)
        {
            _turnDevice.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out _turnInput);
        }
        else
        {
            // Fallback clavier
            float turnKeyboard = 0f;
            if (Input.GetKey(KeyCode.Q)) turnKeyboard -= 1f;
            if (Input.GetKey(KeyCode.E)) turnKeyboard += 1f;
            _turnInput = new Vector2(turnKeyboard, 0);
        }
    }
    
    void HandleMovement()
    {
        if (_moveInput.magnitude < 0.1f) return;
        
        // Calculer la direction basée sur l'orientation de la tête
        Vector3 forward = headTransform != null ? headTransform.forward : transform.forward;
        Vector3 right = headTransform != null ? headTransform.right : transform.right;
        
        // Projeter sur le plan horizontal
        forward.y = 0;
        forward.Normalize();
        right.y = 0;
        right.Normalize();
        
        // Calculer le vecteur de mouvement
        Vector3 moveDirection = forward * _moveInput.y + right * _moveInput.x;
        
        // Vérifier le sprint (bouton grip ou shift)
        bool isSprinting = false;
        if (_moveDevice.isValid)
        {
            _moveDevice.TryGetFeatureValue(XRCommonUsages.gripButton, out isSprinting);
        }
        isSprinting = isSprinting || Input.GetKey(KeyCode.LeftShift);
        
        float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        
        // Appliquer le mouvement
        Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;
        _characterController.Move(movement);
    }
    
    void HandleRotation()
    {
        float turnValue = _turnInput.x;
        
        if (Mathf.Abs(turnValue) < turnThreshold)
        {
            _canSnapTurn = true;
            return;
        }
        
        if (useSnapTurn)
        {
            // Snap Turn
            if (_canSnapTurn)
            {
                float snapDirection = turnValue > 0 ? snapTurnAngle : -snapTurnAngle;
                transform.Rotate(0, snapDirection, 0);
                _canSnapTurn = false;
            }
        }
        else
        {
            // Smooth Turn
            float turnAmount = turnValue * smoothTurnSpeed * Time.deltaTime;
            transform.Rotate(0, turnAmount, 0);
        }
    }
    
    void HandleGravity()
    {
        if (!useGravity) return;
        
        if (_characterController.isGrounded)
        {
            _velocity.y = -0.5f; // Petite force vers le bas pour rester au sol
        }
        else
        {
            _velocity.y += gravity * Time.deltaTime;
        }
        
        _characterController.Move(_velocity * Time.deltaTime);
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Move Input: {_moveInput}");
        GUILayout.Label($"Turn Input: {_turnInput}");
        GUILayout.Label($"Grounded: {_characterController.isGrounded}");
        GUILayout.Label($"Velocity: {_velocity}");
        GUILayout.EndArea();
    }
}

