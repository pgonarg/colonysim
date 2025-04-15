using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 20f;
    public float zoomSpeed = 2f;
    public float minZoom = 2f;
    public float maxZoom = 20f;
    public float zoomSpeedScaling = 0.5f; // Lower values make zoom speed more consistent at different zoom levels

    [Header("Edge Scrolling")]
    public bool useEdgeScrolling = true;
    public float edgeScrollThreshold = 20f;

    [Header("Smoothing")]
    public float moveSmoothTime = 0.2f;
    public float zoomSmoothTime = 0.2f;
    
    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private float zoomVelocity = 0f;
    private float targetZoom;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
    }

    private void Start()
    {
        // Make sure camera is within map bounds at start
        AdjustPositionToBounds();
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
    }

    private void HandleMovement()
    {
        Vector3 moveDir = Vector3.zero;

        // Keyboard controls
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            moveDir.y += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            moveDir.y -= 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            moveDir.x -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            moveDir.x += 1;

        // Edge scrolling
        if (useEdgeScrolling)
        {
            Vector3 mousePos = Input.mousePosition;

            if (mousePos.x < edgeScrollThreshold)
                moveDir.x -= 1;
            else if (mousePos.x > Screen.width - edgeScrollThreshold)
                moveDir.x += 1;

            if (mousePos.y < edgeScrollThreshold)
                moveDir.y -= 1;
            else if (mousePos.y > Screen.height - edgeScrollThreshold)
                moveDir.y += 1;
        }

        // Apply movement with zoom-based speed adjustment
        if (moveDir != Vector3.zero)
        {
            moveDir.Normalize();
            
            // Target position with smooth dampening
            Vector3 targetPosition = transform.position + moveDir * moveSpeed * Time.deltaTime * (cam.orthographicSize / 5f);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, moveSmoothTime);
        }

        // Ensure camera stays within map bounds
        AdjustPositionToBounds();
    }

    private void HandleZoom()
    {
        float scrollDelta = Input.mouseScrollDelta.y;

        if (scrollDelta != 0)
        {
            // Scale zoom speed based on current zoom level for more consistent feel
            float adjustedZoomSpeed = zoomSpeed * (1 + (cam.orthographicSize - minZoom) * zoomSpeedScaling / (maxZoom - minZoom));
            
            // Update target zoom with smooth dampening
            targetZoom = Mathf.Clamp(targetZoom - scrollDelta * adjustedZoomSpeed, minZoom, maxZoom);
        }

        // Apply zoom with smooth dampening
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
        
        // Ensure position is adjusted for new zoom level
        AdjustPositionToBounds();
    }

    private void AdjustPositionToBounds()
    {
        TileSystem tileSystem = FindObjectOfType<TileSystem>();
        if (tileSystem == null)
            return;
    
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
    
        float mapWidth = tileSystem.width * tileSystem.tileSize;
        float mapHeight = tileSystem.height * tileSystem.tileSize;
    
        // Add small padding to prevent seeing beyond map edges
        float padding = 0.01f;
        float minX = halfWidth + padding;
        float maxX = mapWidth - halfWidth - padding;
        float minY = halfHeight + padding;
        float maxY = mapHeight - halfHeight - padding;
    
        // Handle edge case where camera view is larger than map
        if (minX > maxX)
        {
            minX = maxX = mapWidth / 2;
        }
        if (minY > maxY)
        {
            minY = maxY = mapHeight / 2;
        }
    
        Vector3 clampedPos = transform.position;
        clampedPos.x = Mathf.Clamp(clampedPos.x, minX, maxX);
        clampedPos.y = Mathf.Clamp(clampedPos.y, minY, maxY);
        transform.position = clampedPos;
    }
}