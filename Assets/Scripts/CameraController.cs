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

    [Header("Edge Scrolling")]
    public bool useEdgeScrolling = true;
    public float edgeScrollThreshold = 20f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
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

        // Apply movement
        if (moveDir != Vector3.zero)
        {
            moveDir.Normalize();
            transform.position += moveDir * moveSpeed * Time.deltaTime * (cam.orthographicSize / 5f); // Scale speed with zoom level
        }

        // Keep camera within map bounds if TileSystem exists
        if (TileSystem.Instance != null)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            float mapWidth = TileSystem.Instance.mapWidth * TileSystem.Instance.tileSize;
            float mapHeight = TileSystem.Instance.mapHeight * TileSystem.Instance.tileSize;

            float minX = halfWidth;
            float maxX = mapWidth - halfWidth;
            float minY = halfHeight;
            float maxY = mapHeight - halfHeight;

            Vector3 clampedPos = transform.position;
            clampedPos.x = Mathf.Clamp(clampedPos.x, minX, maxX);
            clampedPos.y = Mathf.Clamp(clampedPos.y, minY, maxY);
            transform.position = clampedPos;
        }
    }

    private void HandleZoom()
    {
        float scrollDelta = Input.mouseScrollDelta.y;

        if (scrollDelta != 0)
        {
            float newZoom = cam.orthographicSize - scrollDelta * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(newZoom, minZoom, maxZoom);
        }
    }
}