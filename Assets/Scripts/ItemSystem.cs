using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// Base class for all item definitions
[CreateAssetMenu(fileName = "NewItem", menuName = "Colony/Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique identifier for this item")]
    public string itemID;
    
    [Tooltip("Display name of the item")]
    public string displayName;
    
    [Tooltip("Description of the item")]
    [TextArea(3, 6)]
    public string description;
    
    [Tooltip("Visual representation of the item")]
    public Sprite sprite;
    
    [Header("Properties")]
    [Tooltip("Tags that describe this item's properties")]
    public List<ItemTag> tags = new List<ItemTag>();
    
    [Tooltip("Can multiple instances of this item stack in inventory?")]
    public bool isStackable = true;
    
    [Tooltip("Maximum stack size if stackable")]
    public int maxStackSize = 20;
    
    [Tooltip("Weight of a single item (in arbitrary units)")]
    public float weight = 1f;
    
    [Tooltip("Base value/worth of this item")]
    public float value = 1f;
    
    [Header("Placement")]
    [Tooltip("Can this item be placed in the world?")]
    public bool isPlaceable = false;
    
    [Tooltip("Can creatures walk on this item when placed?")]
    public bool isWalkable = true;
    
    [Tooltip("Does this item block line of sight when placed?")]
    public bool blocksVision = false;
    
    [Header("Needs Satisfaction")]
    [Tooltip("Needs this item can satisfy")]
    public List<NeedSatisfier> needSatisfiers = new List<NeedSatisfier>();
    
    [Header("Crafting")]
    [Tooltip("Resources required to craft this item")]
    public List<ItemIngredient> craftingIngredients = new List<ItemIngredient>();
    
    [Tooltip("Time to craft (in seconds)")]
    public float craftingTime = 5f;
    
    [Tooltip("Required abilities to craft this item")]
    public List<string> requiredAbilities = new List<string>();
    
    // Method to check if this item has a specific tag
    public bool HasTag(string tagID)
    {
        return tags.HasTag(tagID);
    }
    
    // Method to check if this item can satisfy a specific need
    public bool CanSatisfyNeed(string needID, out float effectiveness)
    {
        effectiveness = 0f;
        
        foreach (var satisfier in needSatisfiers)
        {
            if (satisfier.needID == needID)
            {
                effectiveness = satisfier.effectiveness;
                return true;
            }
        }
        
        return false;
    }
    
    // Factory method to create an item instance
    public virtual Item CreateInstance(int quantity = 1)
    {
        return new Item(this, quantity);
    }
}

// Special types of items
[CreateAssetMenu(fileName = "NewFurniture", menuName = "Colony/Items/Furniture")]
public class FurnitureDefinition : ItemDefinition
{
    [Header("Furniture Properties")]
    [Tooltip("Does this furniture need to be against a wall?")]
    public bool requiresWall = false;
    
    [Tooltip("Number of creatures that can use this simultaneously")]
    public int maxUsers = 1;
    
    [Tooltip("Quality level of this furniture (affects need satisfaction)")]
    [Range(0.5f, 2.0f)]
    public float qualityLevel = 1.0f;
    
    // Override create instance to return a furniture item
    public override Item CreateInstance(int quantity = 1)
    {
        FurnitureItem furniture = new FurnitureItem(this, quantity);
        furniture.qualityLevel = qualityLevel;
        return furniture;
    }
}

[CreateAssetMenu(fileName = "NewConsumable", menuName = "Colony/Items/Consumable")]
public class ConsumableDefinition : ItemDefinition
{
    [Header("Consumable Properties")]
    [Tooltip("Number of uses before the item is depleted")]
    public int usesRemaining = 1;
    
    [Tooltip("Effects applied when consumed")]
    public List<ConsumableEffect> consumptionEffects = new List<ConsumableEffect>();
    
    // Override create instance to return a consumable item
    public override Item CreateInstance(int quantity = 1)
    {
        ConsumableItem consumable = new ConsumableItem(this, quantity);
        consumable.usesRemaining = usesRemaining;
        return consumable;
    }
}

[CreateAssetMenu(fileName = "NewStructure", menuName = "Colony/Items/Structure")]
public class StructureDefinition : ItemDefinition
{
    [Header("Structure Properties")]
    [Tooltip("Health/durability of the structure")]
    public float hitPoints = 100f;
    
    [Tooltip("Can this structure be deconstructed?")]
    public bool canDeconstruct = true;
    
    [Tooltip("Resources recovered when deconstructed (percentage)")]
    [Range(0f, 1f)]
    public float recoveryPercent = 0.5f;
    
    // Override create instance to return a structure item
    public override Item CreateInstance(int quantity = 1)
    {
        StructureItem structure = new StructureItem(this, quantity);
        structure.currentHitPoints = hitPoints;
        return structure;
    }
}

// Item instance classes
[Serializable]
public class Item
{
    public ItemDefinition definition;
    public int quantity;
    public float durability = 1f;
    public Dictionary<string, object> customData = new Dictionary<string, object>();
    
    public Item(ItemDefinition definition, int quantity = 1)
    {
        this.definition = definition;
        this.quantity = Mathf.Clamp(quantity, 1, definition.isStackable ? definition.maxStackSize : 1);
    }
    
    public virtual bool CanStack(Item other)
    {
        if (!definition.isStackable) return false;
        if (definition.itemID != other.definition.itemID) return false;
        if (quantity >= definition.maxStackSize) return false;
        return true;
    }
    
    public virtual bool Stack(Item other)
    {
        if (!CanStack(other)) return false;
        
        int spaceRemaining = definition.maxStackSize - quantity;
        int amountToAdd = Mathf.Min(spaceRemaining, other.quantity);
        
        quantity += amountToAdd;
        other.quantity -= amountToAdd;
        
        return true;
    }
    
    public virtual Item Split(int amount)
    {
        if (amount <= 0 || amount >= quantity) return null;
        
        quantity -= amount;
        return definition.CreateInstance(amount);
    }
    
    public virtual void Use(Creature user)
    {
        // Base implementation does nothing
    }
    
    public virtual bool CanSatisfyNeed(string needID, out float effectiveness)
    {
        return definition.CanSatisfyNeed(needID, out effectiveness);
    }
}

[Serializable]
public class FurnitureItem : Item
{
    public float qualityLevel = 1.0f;
    public List<Creature> currentUsers = new List<Creature>();
    
    public FurnitureItem(ItemDefinition definition, int quantity = 1) : base(definition, quantity)
    {
        if (definition is FurnitureDefinition furnitureDefinition)
        {
            qualityLevel = furnitureDefinition.qualityLevel;
        }
    }
    
    public override bool CanSatisfyNeed(string needID, out float effectiveness)
    {
        bool canSatisfy = base.CanSatisfyNeed(needID, out effectiveness);
        
        if (canSatisfy)
        {
            // Apply quality modifier
            effectiveness *= qualityLevel;
            
            // Check if furniture is already fully occupied
            FurnitureDefinition furnitureDef = definition as FurnitureDefinition;
            if (furnitureDef != null && currentUsers.Count >= furnitureDef.maxUsers)
            {
                return false;
            }
        }
        
        return canSatisfy;
    }
    
    public bool AddUser(Creature creature)
    {
        FurnitureDefinition furnitureDef = definition as FurnitureDefinition;
        if (furnitureDef == null || currentUsers.Count >= furnitureDef.maxUsers)
        {
            return false;
        }
        
        currentUsers.Add(creature);
        return true;
    }
    
    public void RemoveUser(Creature creature)
    {
        currentUsers.Remove(creature);
    }
}

[Serializable]
public class ConsumableItem : Item
{
    public int usesRemaining;
    
    public ConsumableItem(ItemDefinition definition, int quantity = 1) : base(definition, quantity)
    {
        if (definition is ConsumableDefinition consumableDefinition)
        {
            usesRemaining = consumableDefinition.usesRemaining;
        }
        else
        {
            usesRemaining = 1;
        }
    }
    
    public override void Use(Creature user)
    {
        if (usesRemaining <= 0) return;
        
        ConsumableDefinition consumableDef = definition as ConsumableDefinition;
        if (consumableDef == null) return;
        
        // Apply consumption effects
        foreach (var effect in consumableDef.consumptionEffects)
        {
            // Handle need satisfaction
            if (effect.needID != null && !string.IsNullOrEmpty(effect.needID))
            {
                Need need = user.GetNeed(effect.needID);
                if (need != null)
                {
                    need.Fulfill(effect.effectAmount);
                }
            }
            
            // Other effects could be implemented here
        }
        
        // Reduce uses remaining
        usesRemaining--;
        
        // If depleted, reduce quantity
        if (usesRemaining <= 0)
        {
            quantity--;
            
            // Reset uses if we still have more of this item
            if (quantity > 0 && consumableDef != null)
            {
                usesRemaining = consumableDef.usesRemaining;
            }
        }
    }
}

[Serializable]
public class StructureItem : Item
{
    public float currentHitPoints;
    public bool isBuilt = false;
    public Vector2Int worldPosition;
    
    public StructureItem(ItemDefinition definition, int quantity = 1) : base(definition, quantity)
    {
        if (definition is StructureDefinition structureDefinition)
        {
            currentHitPoints = structureDefinition.hitPoints;
        }
        else
        {
            currentHitPoints = 100f;
        }
    }
    
    public void TakeDamage(float amount)
    {
        currentHitPoints = Mathf.Max(0, currentHitPoints - amount);
        
        // Update durability percentage
        StructureDefinition structureDef = definition as StructureDefinition;
        if (structureDef != null)
        {
            durability = currentHitPoints / structureDef.hitPoints;
        }
    }
    
    public bool IsDestroyed()
    {
        return currentHitPoints <= 0;
    }
}

// Helper classes for item properties
[Serializable]
public class NeedSatisfier
{
    [Tooltip("ID of the need this item satisfies")]
    public string needID;
    
    [Tooltip("How effectively this item satisfies the need (multiplier)")]
    [Range(0.1f, 3.0f)]
    public float effectiveness = 1.0f;
    
    [Tooltip("Can this be used to fully satisfy the need?")]
    public bool canFullySatisfy = true;
    
    [Tooltip("Time required to use this item to satisfy the need")]
    public float useTime = 5.0f;
}

[Serializable]
public class ItemIngredient
{
    [Tooltip("Item tag for the required ingredient")]
    public ItemTag itemTag;
    
    [Tooltip("Specific item ID (if needed, otherwise leave empty)")]
    public string specificItemID;
    
    [Tooltip("Quantity required")]
    public int quantity = 1;
    
    [Tooltip("Is this ingredient consumed in crafting?")]
    public bool isConsumed = true;
}

[Serializable]
public class ConsumableEffect
{
    [Tooltip("Need this consumable affects (if any)")]
    public string needID;
    
    [Tooltip("Amount this affects the need")]
    public float effectAmount = 10f;
    
    [Tooltip("Other effect type (for future expansion)")]
    public string effectType;
    
    [Tooltip("Duration of effect (if applicable)")]
    public float duration = 0f;
}