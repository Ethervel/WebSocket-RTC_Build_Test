using UnityEngine;
using TMPro;

/// <summary>
/// Script pour l'avatar VR distant.
/// Gère l'affichage du nom et les animations de l'avatar.
/// </summary>
public class VRRemoteAvatar : MonoBehaviour
{
    [Header("Avatar Parts")]
    [Tooltip("Transform de la tête de l'avatar")]
    public Transform head;
    
    [Tooltip("Transform de la main gauche")]
    public Transform leftHand;
    
    [Tooltip("Transform de la main droite")]
    public Transform rightHand;
    
    [Tooltip("Corps/Torso de l'avatar")]
    public Transform body;
    
    [Header("Name Tag")]
    [Tooltip("TextMeshPro pour afficher le nom")]
    public TextMeshPro nameTag;
    
    [Tooltip("Offset du nom par rapport à la tête")]
    public Vector3 nameTagOffset = new Vector3(0, 0.3f, 0);
    
    [Tooltip("Le nom fait face à la caméra")]
    public bool nameFacesCamera = true;
    
    [Header("Visual Settings")]
    [Tooltip("Couleur de l'avatar")]
    public Color avatarColor = Color.blue;
    
    [Tooltip("Renderer principal pour la couleur")]
    public Renderer mainRenderer;
    
    [Header("Animation")]
    [Tooltip("Animer le corps vers la direction du regard")]
    public bool rotateBodyToHead = true;
    
    [Tooltip("Vitesse de rotation du corps")]
    public float bodyRotationSpeed = 5f;
    
    // Cache
    private Transform _mainCameraTransform;
    private Material _avatarMaterial;
    private string _playerName;
    
    void Start()
    {
        // Trouver la caméra principale
        if (Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }
        
        // Configurer la couleur
        ApplyColor();
        
        // S'assurer que les références sont correctes
        ValidateReferences();
    }
    
    void Update()
    {
        // Faire pointer le nom vers la caméra
        if (nameFacesCamera && nameTag != null && _mainCameraTransform != null)
        {
            UpdateNameTagOrientation();
        }
        
        // Animer le corps
        if (rotateBodyToHead && body != null && head != null)
        {
            UpdateBodyRotation();
        }
    }
    
    void ValidateReferences()
    {
        // Auto-trouver les références si pas assignées
        if (head == null)
        {
            head = transform.Find("Head");
            if (head == null)
            {
                // Créer une tête basique
                head = CreateBasicPart("Head", new Vector3(0, 1.6f, 0), 0.2f);
            }
        }
        
        if (leftHand == null)
        {
            leftHand = transform.Find("LeftHand");
            if (leftHand == null)
            {
                leftHand = CreateBasicPart("LeftHand", new Vector3(-0.3f, 1.2f, 0.2f), 0.1f);
            }
        }
        
        if (rightHand == null)
        {
            rightHand = transform.Find("RightHand");
            if (rightHand == null)
            {
                rightHand = CreateBasicPart("RightHand", new Vector3(0.3f, 1.2f, 0.2f), 0.1f);
            }
        }
        
        if (body == null)
        {
            body = transform.Find("Body");
            if (body == null)
            {
                body = CreateBasicBody("Body", new Vector3(0, 1.0f, 0));
            }
        }
        
        if (nameTag == null)
        {
            CreateNameTag();
        }
    }
    
    Transform CreateBasicPart(string partName, Vector3 localPosition, float scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = partName;
        go.transform.SetParent(transform);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one * scale;
        
        // Supprimer le collider
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        
        // Appliquer la couleur
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && _avatarMaterial != null)
        {
            renderer.material = _avatarMaterial;
        }
        
        return go.transform;
    }
    
    Transform CreateBasicBody(string bodyName, Vector3 localPosition)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = bodyName;
        go.transform.SetParent(transform);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(0.3f, 0.5f, 0.15f);
        
        // Supprimer le collider
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        
        // Stocker le renderer principal
        mainRenderer = go.GetComponent<Renderer>();
        
        return go.transform;
    }
    
    void CreateNameTag()
    {
        var nameTagGO = new GameObject("NameTag");
        nameTagGO.transform.SetParent(head != null ? head : transform);
        nameTagGO.transform.localPosition = nameTagOffset;
        
        nameTag = nameTagGO.AddComponent<TextMeshPro>();
        nameTag.text = _playerName ?? "Player";
        nameTag.fontSize = 2;
        nameTag.alignment = TextAlignmentOptions.Center;
        nameTag.color = Color.white;
        
        // Configurer le rect transform
        var rect = nameTag.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2, 0.5f);
    }
    
    void UpdateNameTagOrientation()
    {
        if (nameTag == null || _mainCameraTransform == null) return;
        
        // Positionner au-dessus de la tête
        if (head != null)
        {
            nameTag.transform.position = head.position + nameTagOffset;
        }
        
        // Faire face à la caméra
        Vector3 lookDirection = _mainCameraTransform.position - nameTag.transform.position;
        lookDirection.y = 0; // Garder horizontal
        
        if (lookDirection != Vector3.zero)
        {
            nameTag.transform.rotation = Quaternion.LookRotation(-lookDirection);
        }
    }
    
    void UpdateBodyRotation()
    {
        if (body == null || head == null) return;
        
        // Calculer la direction du regard (horizontale)
        Vector3 headForward = head.forward;
        headForward.y = 0;
        headForward.Normalize();
        
        if (headForward != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(headForward);
            body.rotation = Quaternion.Slerp(
                body.rotation,
                targetRotation,
                Time.deltaTime * bodyRotationSpeed
            );
        }
    }
    
    void ApplyColor()
    {
        // Créer un nouveau matériau
        _avatarMaterial = new Material(Shader.Find("Standard"));
        _avatarMaterial.color = avatarColor;
        
        // Appliquer à tous les renderers
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            // Ne pas changer le matériau du texte
            if (r.GetComponent<TextMeshPro>() == null)
            {
                r.material = _avatarMaterial;
            }
        }
    }
    
    #region Public Methods
    
    /// <summary>
    /// Définit le nom affiché au-dessus de l'avatar.
    /// </summary>
    public void SetPlayerName(string name)
    {
        _playerName = name;
        
        if (nameTag != null)
        {
            nameTag.text = name;
        }
    }
    
    /// <summary>
    /// Définit la couleur de l'avatar.
    /// </summary>
    public void SetColor(Color color)
    {
        avatarColor = color;
        ApplyColor();
    }
    
    /// <summary>
    /// Met à jour la position de la tête.
    /// </summary>
    public void SetHeadPose(Vector3 position, Quaternion rotation)
    {
        if (head != null)
        {
            head.position = position;
            head.rotation = rotation;
        }
    }
    
    /// <summary>
    /// Met à jour la position de la main gauche.
    /// </summary>
    public void SetLeftHandPose(Vector3 position, Quaternion rotation)
    {
        if (leftHand != null)
        {
            leftHand.position = position;
            leftHand.rotation = rotation;
        }
    }
    
    /// <summary>
    /// Met à jour la position de la main droite.
    /// </summary>
    public void SetRightHandPose(Vector3 position, Quaternion rotation)
    {
        if (rightHand != null)
        {
            rightHand.position = position;
            rightHand.rotation = rotation;
        }
    }
    
    /// <summary>
    /// Cache ou affiche l'avatar.
    /// </summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
    
    #endregion
}
