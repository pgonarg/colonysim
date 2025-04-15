using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// Tags for items and structures
[Flags]
public enum ItemTags
{
    None = 0,
    Food = 1 << 0,
    Drink = 1 << 1,
    Bed = 1 << 2,
    Chair = 1 << 3,
    Table = 1 << 4,
    BuildingMaterial = 1 << 5,
    Tool = 1 << 6,
    Weapon = 1 << 7,
    Clothing = 1 << 8,
    Decoration = 1 << 9,
    Storage = 1 << 10,
    Medicine = 1 << 11,
    Crafting = 1 << 12
}

// Tags for need satisfaction
[Flags]
public enum NeedTags
{
    None = 0,
    Food = 1 << 0,
    Sleep = 1 << 1,
    Drink = 1 << 2,
    Socialization = 1 << 3,
    Shelter = 1 << 4,
    Entertainment = 1 << 5,
    Health = 1 << 6,
    Hygiene = 1 << 7,
    Comfort = 1 << 8
}

// Tags for creature types/professions
[Flags]
public enum CreatureTags
{
    None = 0,
    Worker = 1 << 0,
    Farmer = 1 << 1,
    Miner = 1 << 2,
    Crafter = 1 << 3,
    Soldier = 1 << 4,
    Noble = 1 << 5,
    Child = 1 << 6,
    Elder = 1 << 7,
    Healer = 1 << 8,
    Cook = 1 << 9
}

// Base item class
[System.Serializable]
public class Item
{
    public string itemName;
    public string description;
    public Sprite itemSprite;
    public ItemTags tags;
    public NeedTags satisfiesNeeds;
    public float quality = 1f; // Multiplier for effectiveness
    public bool isCountable = true; // Whether the item stacks
    public int maxStack = 50; // Max stack size if countable
    public float weight = 1f;
    public Dictionary<string, object> customProperties = new Dictionary<string, object>();

    // For non-countable items (like furniture), track if it's being used
    public bool isInUse = false;
    public Creature userCreature = null;

    public Item(string name, ItemTags itemTags, NeedTags needTags)
    {
        itemName = name;
        tags = itemTags;
        satisfiesNeeds = needTags;
    }

    public virtual bool CanSatisfyNeed(NeedType needType)
    {
        switch (needType)
        {
            case NeedType.Food:
                return satisfiesNeeds.HasFlag(NeedTags.Food);
            case NeedType.Sleep:
                return satisfiesNeeds.HasFlag(NeedTags.Sleep);
            case NeedType.Drink:
                return satisfiesNeeds.HasFlag(NeedTags.Drink);
            case NeedType.Socialization:
                return satisfiesNeeds.HasFlag(NeedTags.Socialization);
            default:
                return false;
        }
    }

    public virtual float GetNeedFulfillmentRate(NeedType needType)
    {
        // Base rate modified by quality
        return 1f * quality;
    }

    public virtual bool IsAvailable()
    {
        return isCountable || !isInUse;
    }

    public virtual void SetInUse(Creature creature)
    {
        if (!isCountable)
        {
            isInUse = true;
            userCreature = creature;
        }
    }

    public virtual void SetAvailable()
    {
        isInUse = false;
        userCreature = null;
    }
}

// Structure template class
[System.Serializable]
public class Structure
{
    public string structureName;
    public string description;
    public Sprite structureSprite;
    public ItemTags requiredMaterials;
    public Dictionary<ItemTags, int> materialCosts = new Dictionary<ItemTags, int>();
    public NeedTags providesFor;
    public List<ItemTags> allowedItems = new List<ItemTags>();
    public bool walkable = true;
    public bool blocksLight = false;

    public Structure(string name, Dictionary<ItemTags, int> costs, NeedTags needs)
    {
        structureName = name;
        materialCosts = costs;
        providesFor = needs;

        // Combine all required materials into a single flag
        requiredMaterials = ItemTags.None;
        foreach (var material in materialCosts.Keys)
        {
            requiredMaterials |= material;
        }
    }
}

// Item database
public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [Header("Item Definitions")]
    public List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

    [Header("Structure Definitions")]
    public List<StructureDefinition> structureDefinitions = new List<StructureDefinition>();

    private Dictionary<string, ItemDefinition> itemLookup = new Dictionary<string, ItemDefinition>();
    private Dictionary<string, StructureDefinition> structureLookup = new Dictionary<string, StructureDefinition>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize lookups
        foreach (var itemDef in itemDefinitions)
        {
            itemLookup[itemDef.itemName] = itemDef;
        }

        foreach (var structureDef in structureDefinitions)
        {
            structureLookup[structureDef.structureName] = structureDef;
        }
    }

    public Item CreateItem(string itemName)
    {
        if (itemLookup.TryGetValue(itemName, out ItemDefinition def))
        {
            Item newItem = new Item(def.itemName, def.tags, def.satisfiesNeeds)
            {
                description = def.description,
                itemSprite = def.itemSprite,
                quality = def.quality,
                isCountable = def.isCountable,
                maxStack = def.maxStack,
                weight = def.weight
            };

            return newItem;
        }

        Debug.LogWarning($"Item '{itemName}' not found in database");
        return null;
    }

    public Structure CreateStructure(string structureName)
    {
        if (structureLookup.TryGetValue(structureName, out StructureDefinition def))
        {
            Structure newStructure = new Structure(def.structureName,
                def.materialCosts, def.providesFor)
            {
                description = def.description,
                structureSprite = def.structureSprite,
                allowedItems = def.allowedItems,
                walkable = def.walkable,
                blocksLight = def.blocksLight
            };

            return newStructure;
        }

        Debug.LogWarning($"Structure '{structureName}' not found in database");
        return null;
    }
}

// Item definition for inspector
[System.Serializable]
public class ItemDefinition
{
    public string itemName;
    public string description;
    public Sprite itemSprite;
    public ItemTags tags;
    public NeedTags satisfiesNeeds;
    public float quality = 1f;
    public bool isCountable = true;
    public int maxStack = 50;
    public float weight = 1f;
}

// Structure definition for inspector
[System.Serializable]
public class StructureDefinition
{
    public string structureName;
    public string description;
    public Sprite structureSprite;
    [HideInInspector] public Dictionary<ItemTags, int> materialCosts = new Dictionary<ItemTags, int>();
    public NeedTags providesFor;
    public List<ItemTags> allowedItems = new List<ItemTags>();
    public bool walkable = true;
    public bool blocksLight = false;

    // For inspector
    public List<MaterialCost> materialCostList = new List<MaterialCost>();

    // Convert the list to dictionary
    public void UpdateMaterialCosts()
    {
        materialCosts.Clear();
        foreach (var cost in materialCostList)
        {
            materialCosts[cost.materialType] = cost.amount;
        }
    }
}

// Helper class for inspector
[System.Serializable]
public class MaterialCost
{
    public ItemTags materialType;
    public int amount = 1;
}