using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewNeed", menuName = "Colony/Needs/Need Definition")]
public class NeedDefinition : ScriptableObject
{
    [Tooltip("Unique identifier for this need")]
    public string needID;
    
    [Tooltip("Display name for the need")]
    public string displayName;
    
    [Tooltip("Description of the need")]
    [TextArea(3, 6)]
    public string description;
    
    [Tooltip("Icon representing this need")]
    public Sprite icon;
    
    [Header("Need Properties")]
    [Tooltip("How quickly this need depletes over time (units per second)")]
    public float decayRate = 0.2f;
    
    [Tooltip("Threshold below which the need is considered critical")]
    [Range(0, 100)]
    public float criticalThreshold = 20f;
    
    [Tooltip("Maximum time (in seconds) a creature can survive with this need below critical threshold")]
    public float survivalTimeAtCritical = 300f;
    
    [Tooltip("Base rate at which this need is fulfilled when the appropriate action is taken")]
    public float fulfillmentRate = 10f;
}

// Instance of a need attached to a creature
[System.Serializable]
public class Need
{
    public NeedDefinition definition;
    public float currentValue = 100f;
    public float criticalTimer = 0f;
    
    // These can be used for advanced need behavior
    public object targetObject;
    public Vector2Int targetLocation;
    
    public Need(NeedDefinition definition, float initialValue = 100f)
    {
        this.definition = definition;
        this.currentValue = initialValue;
    }
    
    public void Update(float deltaTime)
    {
        // Apply decay
        currentValue = Mathf.Max(0f, currentValue - (definition.decayRate * deltaTime));
        
        // Handle critical state
        if (currentValue < definition.criticalThreshold)
        {
            criticalTimer += deltaTime;
        }
        else
        {
            // Slowly recover from critical state
            criticalTimer = Mathf.Max(0f, criticalTimer - (deltaTime * 0.5f));
        }
    }
    
    public void Fulfill(float amount)
    {
        currentValue = Mathf.Min(100f, currentValue + amount);
    }
    
    public bool IsCritical() => currentValue < definition.criticalThreshold;
    
    public bool IsFatal() => criticalTimer >= definition.survivalTimeAtCritical;
    
    public float GetUrgency()
    {
        // Base urgency is inverse of current value
        float baseUrgency = 1f - (currentValue / 100f);
        
        // Add extra urgency when critical timer is accumulating
        if (criticalTimer > 0)
        {
            baseUrgency += (criticalTimer / definition.survivalTimeAtCritical) * 0.5f;
        }
        
        return Mathf.Clamp01(baseUrgency);
    }
}