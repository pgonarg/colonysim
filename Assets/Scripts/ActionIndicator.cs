using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ActionIndicator : MonoBehaviour
{
    [Header("Settings")]
    public float floatHeight = 0.7f;
    public float bobAmount = 0.2f;
    public float bobSpeed = 2f;
    public Color textColor = Color.white;
    public Color textOutlineColor = Color.black;

    private TextMeshPro textMesh;
    private Transform targetTransform;
    private float initialY;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMeshPro>();
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontSize = 3;
            textMesh.color = textColor;

            // Add outline
            //textMesh.enableOutline = true;
            textMesh.outlineColor = textOutlineColor;
            textMesh.outlineWidth = 0.2f;
        }

        // Set sorting to be above creatures
        //textMesh.sortingLayerName = "UI";
        textMesh.sortingOrder = 10;
    }

    public void Initialize(Transform target)
    {
        targetTransform = target;
        initialY = target.position.y + floatHeight;
        UpdatePosition();
    }

    private void Update()
    {
        if (targetTransform != null)
        {
            UpdatePosition();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void UpdatePosition()
    {
        // Calculate position with bobbing effect
        float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        // Position above target with bob effect
        transform.position = new Vector3(
            targetTransform.position.x,
            initialY + yOffset,
            targetTransform.position.z - 0.1f  // Slightly in front of creature
        );
    }

    public void SetText(string text)
    {
        if (textMesh != null)
        {
            textMesh.text = text;
        }
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}