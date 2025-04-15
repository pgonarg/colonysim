using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// Base class for all tags
[Serializable]
public abstract class Tag : ScriptableObject
{
    [Tooltip("Unique identifier for this tag")]
    public string tagID;
    
    [Tooltip("Display name for UI")]
    public string displayName;
    
    [Tooltip("Description of what this tag means")]
    [TextArea(2, 5)]
    public string description;
}

// Tag for items
[CreateAssetMenu(fileName = "NewItemTag", menuName = "Colony/Tags/Item Tag")]
public class ItemTag : Tag
{
    [Tooltip("Category of items this tag represents")]
    public string category;
    
    [Tooltip("Icon representing this tag")]
    public Sprite icon;
}

// Tag for resources
[CreateAssetMenu(fileName = "NewResourceTag", menuName = "Colony/Tags/Resource Tag")]
public class ResourceTag : Tag
{
    [Tooltip("Base value of this resource")]
    public float baseValue = 1.0f;
    
    [Tooltip("Rarity of this resource (higher = more rare)")]
    [Range(1, 10)]
    public int rarityTier = 1;
    
    [Tooltip("Icon representing this resource")]
    public Sprite icon;
}

// Tag for creature traits
[CreateAssetMenu(fileName = "NewCreatureTag", menuName = "Colony/Tags/Creature Tag")]
public class CreatureTag : Tag
{
    [Tooltip("Is this tag beneficial or harmful?")]
    public bool isBeneficial = true;
    
    [Tooltip("Strength of the effect")]
    [Range(0.1f, 3.0f)]
    public float effectStrength = 1.0f;
    
    [Tooltip("Icon representing this trait")]
    public Sprite icon;
}

// Tag for terrain/tile properties
[CreateAssetMenu(fileName = "NewTerrainTag", menuName = "Colony/Tags/Terrain Tag")]
public class TerrainTag : Tag
{
    [Tooltip("Movement speed multiplier on this terrain")]
    public float movementMultiplier = 1.0f;
    
    [Tooltip("Can entities walk on this terrain?")]
    public bool walkable = true;
    
    [Tooltip("Does this terrain block line of sight?")]
    public bool blocksVision = false;
}

// Tag registry - central repository for all tags
public class TagRegistry : MonoBehaviour
{
    private static TagRegistry _instance;
    public static TagRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("TagRegistry");
                _instance = go.AddComponent<TagRegistry>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Cache of all loaded tags by type and ID
    private Dictionary<Type, Dictionary<string, Tag>> tagCache = new Dictionary<Type, Dictionary<string, Tag>>();

    // Initialize in Awake to load all tags
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Load all tags
        LoadAllTags<ItemTag>();
        LoadAllTags<ResourceTag>();
        LoadAllTags<CreatureTag>();
        LoadAllTags<TerrainTag>();
        
        Debug.Log("Tag Registry initialized");
    }
    
    // Load all tags of a given type from Resources
    private void LoadAllTags<T>() where T : Tag
    {
        tagCache[typeof(T)] = new Dictionary<string, Tag>();
        
        T[] tags = Resources.LoadAll<T>("Tags");
        foreach (T tag in tags)
        {
            tagCache[typeof(T)][tag.tagID] = tag;
        }
        
        Debug.Log($"Loaded {tagCache[typeof(T)].Count} {typeof(T).Name}s");
    }
    
    // Get a tag by ID and type
    public T GetTag<T>(string tagID) where T : Tag
    {
        if (!tagCache.TryGetValue(typeof(T), out var typeTags))
        {
            Debug.LogWarning($"No tags of type {typeof(T).Name} have been loaded");
            return null;
        }
        
        if (!typeTags.TryGetValue(tagID, out var tag))
        {
            Debug.LogWarning($"Tag with ID {tagID} of type {typeof(T).Name} not found");
            return null;
        }
        
        return tag as T;
    }
    
    // Get all tags of a type
    public List<T> GetAllTags<T>() where T : Tag
    {
        if (!tagCache.TryGetValue(typeof(T), out var typeTags))
        {
            Debug.LogWarning($"No tags of type {typeof(T).Name} have been loaded");
            return new List<T>();
        }
        
        List<T> result = new List<T>();
        foreach (var tag in typeTags.Values)
        {
            result.Add(tag as T);
        }
        
        return result;
    }
}

// Extension methods for tag lists
public static class TagListExtensions
{
    // Check if a list of tags contains a specific tag ID
    public static bool HasTag<T>(this List<T> tags, string tagID) where T : Tag
    {
        return tags.Exists(t => t.tagID == tagID);
    }
    
    // Get a specific tag from a list by ID
    public static T GetTag<T>(this List<T> tags, string tagID) where T : Tag
    {
        return tags.Find(t => t.tagID == tagID);
    }
}