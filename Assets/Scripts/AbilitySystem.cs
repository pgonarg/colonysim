using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAbility", menuName = "Colony/Creatures/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    [Tooltip("Unique identifier for this ability")]
    public string abilityID;
    
    [Tooltip("Display name for the ability")]
    public string displayName;
    
    [Tooltip("Description of the ability")]
    [TextArea(3, 6)]
    public string description;
    
    [Tooltip("Icon representing this ability")]
    public Sprite icon;
    
    [Header("Ability Properties")]
    [Tooltip("Effectiveness factor - higher values mean the creature performs this ability better")]
    [Range(0.1f, 3.0f)]
    public float effectivenessFactor = 1.0f;
    
    [Tooltip("Time the creature needs to perform this ability (in seconds)")]
    public float baseActionTime = 5.0f;
    
    [Tooltip("Range at which this ability can be used")]
    public float actionRange = 1.0f;
    
    [Header("Resource Interaction")]
    [Tooltip("Types of resources this ability can interact with")]
    public List<ResourceTag> validResourceTags = new List<ResourceTag>();
    
    [Tooltip("Maximum amount of resources that can be affected in one action")]
    public float resourceImpactAmount = 10f;
}

// Instance of an ability attached to a creature
[System.Serializable]
public class Ability 
{
    public AbilityDefinition definition;
    public float cooldownTime = 0f;
    public bool isActive = false;
    
    // These can be used for advanced ability usage
    public object targetObject;
    public Vector2Int targetLocation;
    
    public Ability(AbilityDefinition definition)
    {
        this.definition = definition;
    }
    
    public void Update(float deltaTime)
    {
        // Handle cooldown if needed
        if (cooldownTime > 0)
        {
            cooldownTime = Mathf.Max(0f, cooldownTime - deltaTime);
        }
    }
    
    public bool CanUse() => cooldownTime <= 0 && !isActive;
    
    public float GetEffectiveness() => definition.effectivenessFactor;
    
    public float GetCompletionTime() => definition.baseActionTime / definition.effectivenessFactor;
    
    public void StartUsing()
    {
        isActive = true;
    }
    
    public void FinishUsing(float cooldown = 0f)
    {
        isActive = false;
        cooldownTime = cooldown;
    }
}

// Example built-in abilities (can be extended with custom abilities)
[CreateAssetMenu(fileName = "BuildAbility", menuName = "Colony/Creatures/Abilities/Build")]
public class BuildAbility : AbilityDefinition
{
    [Header("Building Properties")]
    [Tooltip("Speed multiplier for construction")]
    public float buildSpeedMultiplier = 1.0f;
    
    [Tooltip("Quality modifier for built structures")]
    public float qualityModifier = 0.0f;
    
    [Tooltip("Types of structures this ability can build")]
    public List<ItemTag> validBuildingTags = new List<ItemTag>();
}

[CreateAssetMenu(fileName = "HaulAbility", menuName = "Colony/Creatures/Abilities/Haul")]
public class HaulAbility : AbilityDefinition
{
    [Header("Hauling Properties")]
    [Tooltip("Maximum weight the creature can carry")]
    public float weightCapacity = 50f;
    
    [Tooltip("Speed multiplier when carrying items")]
    public float carrySpeedMultiplier = 0.8f;
    
    [Tooltip("Types of items this ability can haul")]
    public List<ItemTag> validCarryTags = new List<ItemTag>();
}

[CreateAssetMenu(fileName = "GatherAbility", menuName = "Colony/Creatures/Abilities/Gather")]
public class GatherAbility : AbilityDefinition
{
    [Header("Gathering Properties")]
    [Tooltip("Efficiency of resource extraction")]
    public float gatherEfficiency = 1.0f;
    
    [Tooltip("Chance to get bonus resources")]
    [Range(0f, 1f)]
    public float bonusResourceChance = 0.1f;
    
    [Tooltip("Types of resources this ability can gather")]
    public List<ResourceTag> validResourceTags = new List<ResourceTag>();
}