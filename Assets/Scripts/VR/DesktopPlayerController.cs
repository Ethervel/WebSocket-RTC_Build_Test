using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

public class DesktopPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 720f;
    public float mouseSensitivity = 2f;
    
    
    private CharacterController _controller;
    private Transform _cameraTransform;
    private float _pitch;
    private Vector3 _velocity;
    
    void Start()
    {
        _controller = GetComponent<CharacterController>();
        if (_controller == null)
        {
            _controller = gameObject.AddComponent<CharacterController>();
        }
        
        _cameraTransform = GetComponentInChildren<Camera>()?.transform;
        
        
    }
    
    void Update()
    {
        
        HandleMovement();
        HandleGravity();
        
        
    }
    
    
    
    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            speed *= 1.5f;
        }
        
        _controller.Move(move * speed * Time.deltaTime);
    }
    
    void HandleGravity()
    {
        if (_controller.isGrounded)
        {
            _velocity.y = -0.5f;
        }
        else
        {
            _velocity.y += -9.81f * Time.deltaTime;
        }
        
        _controller.Move(_velocity * Time.deltaTime);
    }
}