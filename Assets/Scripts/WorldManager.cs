using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Central manager for the game world
public class WorldManager : MonoBehaviour
{
    private static WorldManager _instance;
    public static WorldManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("WorldManager");
                _instance = go.AddComponent<WorldManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    [Header("Prefabs")]
    public GameObject creaturePrefab;
    public GameObject worldItemPrefab;
    public GameObject structurePrefab;
    public GameObject resourceNodePrefab;
    
    [Header("Systems")]
    public TileSystem tileSystem;
    public Pathfinding pathfinding;
    
    [Header("Game Settings")]
    public float dayLength = 600f; // 10 minutes
    public float timeScale = 1f;
    
    // Track all creatures in the world
    private List<Creature> creatures = new List<Creature>();
    
    // Track world items
    private Dictionary<Vector2Int, List<Item>> worldItems = new Dictionary<Vector2Int, List<Item>>();
    
    // Track structures
    private Dictionary<Vector2Int, StructureItem> structures = new Dictionary<Vector2Int, StructureItem>();
    
    // Track resource nodes
    private Dictionary<Vector2Int, ResourceNode> resources = new Dictionary<Vector2Int, ResourceNode>();
    
    // Track designated areas
    private Dictionary<Vector2Int, AreaType> areas = new Dictionary<Vector2Int, AreaType>();
    
    // Job system
    private List<Job> pendingJobs = new List<Job>();
    
    // Game time
    private float gameTime = 0f;
    private int currentDay = 1;
    private float dayTime = 0f; // 0-1 representing time of day
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Find system references if not manually assigned
        if (tileSystem == null) tileSystem = FindObjectOfType<TileSystem>();
        if (pathfinding == null) pathfinding = FindObjectOfType<Pathfinding>();
    }
    
    private void Start()
    {
        // Initialize systems
        StartCoroutine(InitializeWorld());
    }
    
    private void Update()
    {
        // Update game time
        UpdateGameTime();
        
        // Process world updates
        ProcessWorldUpdates();
    }
    
    // Initialize the world
    private IEnumerator InitializeWorld()
    {
        // Wait for TileSystem to initialize
        if (tileSystem != null)
        {
            yield return new WaitUntil(() => tileSystem.IsInitialized == true);
        }
        
        // Wait for Pathfinding to initialize
        if (pathfinding != null)
        {
           yield return new WaitUntil(() => tileSystem.IsInitialized);
        }
        
        // Initialize default designated areas
        InitializeDefaultAreas();
        
        // Initialize resource nodes
        SpawnInitialResources();
        
        Debug.Log("World initialized successfully");
    }
    
    // Initialize default areas
    private void InitializeDefaultAreas()
    {
        // Create some initial designated areas
        // This would be expanded in a real implementation
        
        // Example: Create a stockpile area
        Vector2Int stockpileStart = new Vector2Int(10, 10);
        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                Vector2Int pos = new Vector2Int(stockpileStart.x + x, stockpileStart.y + y);
                DesignateArea(pos, AreaType.Stockpile);
            }
        }
        
        // Example: Create a social area
        Vector2Int socialStart = new Vector2Int(20, 10);
        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                Vector2Int pos = new Vector2Int(socialStart.x + x, socialStart.y + y);
                DesignateArea(pos, AreaType.Social);
            }
        }
    }
    
    // Spawn initial resources
    private void SpawnInitialResources()
    {
        // Create some initial resource nodes
        // This would be expanded in a real implementation
        
        // Example: Create food sources
        for (int i = 0; i < 10; i++)
        {
            Vector2Int randomPos = GetRandomWalkablePosition();
            
            // Create a food resource node
            ResourceNode foodNode = CreateResourceNode(randomPos, "food");
            if (foodNode != null)
            {
                foodNode.resourceAmount = Random.Range(50f, 100f);
                foodNode.resourceType = "food";
            }
        }
        
        // Example: Create water sources
        for (int i = 0; i < 5; i++)
        {
            Vector2Int randomPos = GetRandomWalkablePosition();
            
            // Create a water resource node
            ResourceNode waterNode = CreateResourceNode(randomPos, "water");
            if (waterNode != null)
            {
                waterNode.resourceAmount = Random.Range(80f, 150f);
                waterNode.resourceType = "water";
            }
        }
    }
    
    // Update game time
    private void UpdateGameTime()
    {
        gameTime += Time.deltaTime * timeScale;
        
        // Update day time
        dayTime = (gameTime % dayLength) / dayLength;
        
        // Check for day change
        int newDay = Mathf.FloorToInt(gameTime / dayLength) + 1;
        if (newDay != currentDay)
        {
            currentDay = newDay;
            OnDayChanged();
        }
    }
    
    // Process world updates
    private void ProcessWorldUpdates()
    {
        // Process resource regrowth
        // Regrow resources based on time of day, etc.
        
        // Process job timers
        // Update job priorities, etc.
    }
    
    // Called when day changes
    private void OnDayChanged()
    {
        Debug.Log($"Day {currentDay} has dawned");
        
        // Daily updates
        // Creatures might get tired at night, etc.
    }
    
    // Create a creature
    public Creature CreateCreature(CreatureDefinition definition, Vector3 position)
    {
        if (definition == null || creaturePrefab == null)
        {
            Debug.LogError("Cannot create creature: missing definition or prefab");
            return null;
        }
        
        GameObject creatureObj = Instantiate(creaturePrefab, position, Quaternion.identity);
        Creature creature = creatureObj.GetComponent<Creature>();
        
        if (creature == null)
        {
            Debug.LogError("Creature prefab does not have Creature component");
            Destroy(creatureObj);
            return null;
        }
        
        // Initialize creature
        creature.definition = definition;
        
        // Generate a name if none is set
        if (string.IsNullOrEmpty(creature.creatureName))
        {
            creature.creatureName = GenerateCreatureName(definition);
        }
        
        // Add to tracking list
        creatures.Add(creature);
        
        return creature;
    }
    
    // Generate a random name for a creature
    private string GenerateCreatureName(CreatureDefinition definition)
    {
        // Simple name generation - this could be much more sophisticated
        string[] prefixes = { "Urist", "Kadol", "Gimli", "Durin", "Thror", "Dain", "Balin", "Thorin" };
        string[] suffixes = { "son", "hammer", "beard", "axe", "forge", "iron", "gold", "stone" };
        
        return prefixes[Random.Range(0, prefixes.Length)] + " " + suffixes[Random.Range(0, suffixes.Length)];
    }
    
    // Called when a creature dies
    public void OnCreatureDied(Creature creature)
    {
        if (creature != null && creatures.Contains(creature))
        {
            creatures.Remove(creature);
        }
    }
    
    // Create a world item at position
    public Item CreateWorldItem(Item item, Vector3 position)
    {
        if (item == null)
        {
            Debug.LogError("Cannot create world item: item is null");
            return null;
        }
        
        // Convert to tile position
        Vector2Int tilePos = tileSystem.GetTilePosition(position);
        
        // Add to world items dictionary
        if (!worldItems.ContainsKey(tilePos))
        {
            worldItems[tilePos] = new List<Item>();
        }
        
        worldItems[tilePos].Add(item);
        
        // Create visual representation if needed
        if (worldItemPrefab != null)
        {
            GameObject itemObj = Instantiate(worldItemPrefab, position, Quaternion.identity);
            WorldItemVisual visual = itemObj.GetComponent<WorldItemVisual>();
            
            if (visual != null)
            {
                visual.Initialize(item, tilePos);
            }
        }
        
        return item;
    }
    
    // Remove a world item
    public void RemoveWorldItem(Item item)
    {
        foreach (var kvp in worldItems)
        {
            if (kvp.Value.Contains(item))
            {
                kvp.Value.Remove(item);
                
                // Remove visual representation
                WorldItemVisual visual = FindItemVisual(item);
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
                
                // Clean up empty lists
                if (kvp.Value.Count == 0)
                {
                    worldItems.Remove(kvp.Key);
                }
                
                return;
            }
        }
    }
    
    // Place a world item at a specific position
    public void PlaceWorldItem(Item item, Vector2Int position)
    {
        if (item == null)
        {
            Debug.LogError("Cannot place world item: item is null");
            return;
        }
        
        // Add to world items dictionary
        if (!worldItems.ContainsKey(position))
        {
            worldItems[position] = new List<Item>();
        }
        
        worldItems[position].Add(item);
        
        // Create visual representation if needed
        if (worldItemPrefab != null)
        {
            Vector3 worldPos = tileSystem.GetWorldPosition(position);
            GameObject itemObj = Instantiate(worldItemPrefab, worldPos, Quaternion.identity);
            WorldItemVisual visual = itemObj.GetComponent<WorldItemVisual>();
            
            if (visual != null)
            {
                visual.Initialize(item, position);
            }
        }
    }
    
    // Find visual representation of an item
    private WorldItemVisual FindItemVisual(Item item)
    {
        WorldItemVisual[] visuals = FindObjectsOfType<WorldItemVisual>();
        
        foreach (var visual in visuals)
        {
            if (visual.item == item)
            {
                return visual;
            }
        }
        
        return null;
    }
    
    // Get items at position
    public List<Item> GetItemsAt(Vector2Int position)
    {
        if (worldItems.TryGetValue(position, out List<Item> items))
        {
            return items;
        }
        
        return new List<Item>();
    }
    
    // Place a structure
    public void PlaceStructure(StructureItem structure, Vector2Int position)
    {
        if (structure == null)
        {
            Debug.LogError("Cannot place structure: structure is null");
            return;
        }
        
        // Check if position is valid
        if (!tileSystem.IsTileWalkable(position) || structures.ContainsKey(position))
        {
            Debug.LogWarning($"Cannot place structure at {position}: position is not valid");
            return;
        }
        
        // Add to structures dictionary
        structures[position] = structure;
        structure.worldPosition = position;
        structure.isBuilt = true;
        
        // Update tile walkability if needed
        if (!structure.definition.isWalkable)
        {
            tileSystem.SetTileWalkable(position, false);
            
            // Update pathfinding if available
            if (pathfinding != null)
            {
                pathfinding.UpdateNode(position.x, position.y, false);
            }
        }
        
        // Create visual representation if needed
        if (structurePrefab != null)
        {
            Vector3 worldPos = tileSystem.GetWorldPosition(position);
            GameObject structureObj = Instantiate(structurePrefab, worldPos, Quaternion.identity);
            StructureVisual visual = structureObj.GetComponent<StructureVisual>();
            
            if (visual != null)
            {
                visual.Initialize(structure, position);
            }
        }
    }
    
    // Get structure at position
    public StructureItem GetStructureAt(Vector2Int position)
    {
        if (structures.TryGetValue(position, out StructureItem structure))
        {
            return structure;
        }
        
        return null;
    }
    
    // Remove a structure
    public void RemoveStructure(Vector2Int position)
    {
        if (structures.TryGetValue(position, out StructureItem structure))
        {
            structures.Remove(position);
            
            // Restore tile walkability if needed
            if (!structure.definition.isWalkable)
            {
                tileSystem.SetTileWalkable(position, true);
                
                // Update pathfinding if available
                if (pathfinding != null)
                {
                    pathfinding.UpdateNode(position.x, position.y, false);
                }
            }
            
            // Remove visual representation
            StructureVisual visual = FindStructureVisual(position);
            if (visual != null)
            {
                Destroy(visual.gameObject);
            }
        }
    }
    
    // Find visual representation of a structure
    private StructureVisual FindStructureVisual(Vector2Int position)
    {
        StructureVisual[] visuals = FindObjectsOfType<StructureVisual>();
        
        foreach (var visual in visuals)
        {
            if (visual.position == position)
            {
                return visual;
            }
        }
        
        return null;
    }
    
    // Create a resource node
    public ResourceNode CreateResourceNode(Vector2Int position, string resourceType)
    {
        if (!tileSystem.IsTileWalkable(position) || resources.ContainsKey(position))
        {
            Debug.LogWarning($"Cannot create resource node at {position}: position is not valid");
            return null;
        }
        
        // Create resource node object
        Vector3 worldPos = tileSystem.GetWorldPosition(position);
        GameObject nodeObj = Instantiate(resourceNodePrefab, worldPos, Quaternion.identity);
        ResourceNode node = nodeObj.GetComponent<ResourceNode>();
        
        if (node == null)
        {
            Debug.LogError("Resource node prefab does not have ResourceNode component");
            Destroy(nodeObj);
            return null;
        }
        
        // Initialize node
        node.resourceType = resourceType;
        
        // Set resource tags based on type
        SetResourceTags(node, resourceType);
        
        // Set possible drops based on type
        SetResourceDrops(node, resourceType);
        
        // Add to tracking dictionary
        resources[position] = node;
        
        return node;
    }
    
    // Set resource tags based on type
    private void SetResourceTags(ResourceNode node, string resourceType)
    {
        node.resourceTags.Clear();
        
        // Add appropriate tags from tag registry
        List<ResourceTag> allTags = TagRegistry.Instance.GetAllTags<ResourceTag>();
        
        foreach (var tag in allTags)
        {
            if (tag.tagID.Contains(resourceType.ToLower()))
            {
                node.resourceTags.Add(tag);
            }
        }
    }
    
    // Set possible drops based on resource type
    private void SetResourceDrops(ResourceNode node, string resourceType)
    {
        node.possibleDrops.Clear();
        
        // This would be expanded with a proper loot table system
        if (resourceType.Contains("food"))
        {
            // Get food items from item database
            // This is a simplified example
            ItemDefinition[] items = Resources.LoadAll<ItemDefinition>("Items");
            
            foreach (var item in items)
            {
                if (item.HasTag("food"))
                {
                    node.possibleDrops.Add(item);
                }
            }
        }
        else if (resourceType.Contains("water"))
        {
            // Get water container items
            ItemDefinition[] items = Resources.LoadAll<ItemDefinition>("Items");
            
            foreach (var item in items)
            {
                if (item.HasTag("water") || item.HasTag("drink"))
                {
                    node.possibleDrops.Add(item);
                }
            }
        }
        // Add more resource types as needed
    }
    
    // Get resource node at position
    public ResourceNode GetResourceNodeAt(Vector2Int position)
    {
        if (resources.TryGetValue(position, out ResourceNode node))
        {
            return node;
        }
        
        return null;
    }
    
    // Designate an area
    public void DesignateArea(Vector2Int position, AreaType areaType)
    {
        // Check if position is valid
        if (!tileSystem.IsTileExists(position))
        {
            Debug.LogWarning($"Cannot designate area at {position}: position is not valid");
            return;
        }
        
        // Add to areas dictionary
        areas[position] = areaType;
        
        // Update visuals
        UpdateAreaVisual(position, areaType);
    }
    
    // Undesignate an area
    public void UndesignateArea(Vector2Int position)
    {
        if (areas.ContainsKey(position))
        {
            areas.Remove(position);
            
            // Update visuals
            UpdateAreaVisual(position, AreaType.None);
        }
    }
    
    // Update area visual
    private void UpdateAreaVisual(Vector2Int position, AreaType areaType)
    {
        // Implementation depends on how areas are visualized
        // Could use overlays, special tiles, etc.
    }
    
    // Get area type at position
    public AreaType GetAreaTypeAt(Vector2Int position)
    {
        if (areas.TryGetValue(position, out AreaType areaType))
        {
            return areaType;
        }
        
        return AreaType.None;
    }
    
    // Check if a position is a stockpile
    public bool IsStockpile(Vector2Int position)
    {
        return GetAreaTypeAt(position) == AreaType.Stockpile;
    }
    
    // Job system methods
    
    // Add a job
    public void AddJob(Job job)
    {
        if (job != null)
        {
            pendingJobs.Add(job);
            
            // Sort jobs by priority
            pendingJobs.Sort((a, b) => b.priority.CompareTo(a.priority));
        }
    }
    
    // Remove a job
    public void RemoveJob(Job job)
    {
        if (job != null && pendingJobs.Contains(job))
        {
            pendingJobs.Remove(job);
        }
    }
    
    // Check if there are pending jobs
    public bool HasPendingJobs()
    {
        return pendingJobs.Count > 0;
    }
    
    // Get a suitable job for a creature
    public Job GetSuitableJob(Creature creature)
    {
        if (creature == null) return null;
        
        foreach (var job in pendingJobs)
        {
            if (job.IsCreatureSuitable(creature))
            {
                pendingJobs.Remove(job);
                return job;
            }
        }
        
        return null;
    }
    
    // Utility methods
    
    // Find nearest creature
    public Creature FindNearestCreature(Vector3 position, float maxDistance = float.MaxValue)
    {
        Creature nearest = null;
        float nearestDistance = maxDistance;
        
        foreach (var creature in creatures)
        {
            float distance = Vector3.Distance(position, creature.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = creature;
            }
        }
        
        return nearest;
    }
    
    // Find nearest idle creature
    public Creature FindNearestIdleCreature(Vector3 position, Creature excludeCreature = null)
    {
        Creature nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var creature in creatures)
        {
            if (creature == excludeCreature) continue;
            
            if (creature.currentState == CreatureState.Idle)
            {
                float distance = Vector3.Distance(position, creature.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = creature;
                }
            }
        }
        
        return nearest;
    }
    
    // Find nearest structure that can satisfy a need
    public StructureItem FindNearestStructureForNeed(string needID, Vector2Int position, float maxDistance = float.MaxValue)
    {
        StructureItem nearest = null;
        float nearestDistance = maxDistance;
        
        foreach (var kvp in structures)
        {
            float effectiveness;
            if (kvp.Value.CanSatisfyNeed(needID, out effectiveness))
            {
                float distance = Vector2Int.Distance(position, kvp.Key);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = kvp.Value;
                }
            }
        }
        
        return nearest;
    }
    
    // Find nearest usable structure at position
    public StructureItem GetUsableStructureAt(Vector2Int position, string needID)
    {
        StructureItem structure = GetStructureAt(position);
        
        if (structure != null)
        {
            float effectiveness;
            if (structure.CanSatisfyNeed(needID, out effectiveness))
            {
                return structure;
            }
        }
        
        return null;
    }
    
    // Find nearest resource of a type
    public Vector2Int FindNearestResourceOfType(string resourceType, Vector2Int position, float maxDistance = float.MaxValue)
    {
        Vector2Int nearest = Vector2Int.zero;
        float nearestDistance = maxDistance;
        
        foreach (var kvp in resources)
        {
            if (kvp.Value.resourceType.Contains(resourceType) && kvp.Value.resourceAmount > 0)
            {
                float distance = Vector2Int.Distance(position, kvp.Key);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = kvp.Key;
                }
            }
        }
        
        return nearest;
    }
    
    // Find nearest stockpile with a specific item tag
    public Vector2Int FindNearestStockpileWithTag(string tagID, Vector2Int position, float maxDistance = float.MaxValue)
    {
        Vector2Int nearest = Vector2Int.zero;
        float nearestDistance = maxDistance;
        
        foreach (var kvp in areas)
        {
            if (kvp.Value == AreaType.Stockpile)
            {
                // Check if there are items with this tag
                List<Item> items = GetItemsAt(kvp.Key);
                bool hasMatchingItem = items.Any(item => item.definition.HasTag(tagID));
                
                if (hasMatchingItem)
                {
                    float distance = Vector2Int.Distance(position, kvp.Key);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = kvp.Key;
                    }
                }
            }
        }
        
        return nearest;
    }
    
    // Find nearest area of a specific type
    public Vector2Int FindNearestAreaOfType(string areaType, Vector2Int position, float maxDistance = float.MaxValue)
    {
        Vector2Int nearest = Vector2Int.zero;
        float nearestDistance = maxDistance;
        
        AreaType targetType;
        if (areaType.Contains("stockpile"))
            targetType = AreaType.Stockpile;
        else if (areaType.Contains("social"))
            targetType = AreaType.Social;
        else if (areaType.Contains("sleep"))
            targetType = AreaType.Sleeping;
        else
            return Vector2Int.zero;
        
        foreach (var kvp in areas)
        {
            if (kvp.Value == targetType)
            {
                float distance = Vector2Int.Distance(position, kvp.Key);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = kvp.Key;
                }
            }
        }
        
        return nearest;
    }
    
    // Find a suitable sleep location
    public Vector2Int FindSuitableSleepLocation(Vector2Int position)
    {
        // First try to find a sleeping area
        Vector2Int sleepArea = FindNearestAreaOfType("sleep", position, 30f);
        
        if (sleepArea != Vector2Int.zero)
        {
            return sleepArea;
        }
        
        // If no designated sleeping area, find a quiet corner
        return FindQuietCorner(position);
    }
    
    // Find a quiet corner for sleeping
    private Vector2Int FindQuietCorner(Vector2Int position)
    {
        // Look for a position that has walls on at least two sides
        for (int radius = 1; radius < 20; radius++)
        {
            for (int x = position.x - radius; x <= position.x + radius; x++)
            {
                for (int y = position.y - radius; y <= position.y + radius; y++)
                {
                    Vector2Int testPos = new Vector2Int(x, y);
                    
                    // Skip if not walkable
                    if (!tileSystem.IsTileWalkable(testPos))
                        continue;
                    
                    // Count adjacent walls
                    int wallCount = 0;
                    if (!tileSystem.IsTileWalkable(new Vector2Int(x + 1, y))) wallCount++;
                    if (!tileSystem.IsTileWalkable(new Vector2Int(x - 1, y))) wallCount++;
                    if (!tileSystem.IsTileWalkable(new Vector2Int(x, y + 1))) wallCount++;
                    if (!tileSystem.IsTileWalkable(new Vector2Int(x, y - 1))) wallCount++;
                    
                    if (wallCount >= 2)
                    {
                        return testPos;
                    }
                }
            }
        }
        
        // Fallback to any walkable tile
        return GetRandomWalkablePosition();
    }
    
    // Get a random walkable position
    private Vector2Int GetRandomWalkablePosition()
    {
        for (int attempts = 0; attempts < 100; attempts++)
        {
            int x = Random.Range(0, tileSystem.width);
            int y = Random.Range(0, tileSystem.height);
            
            Vector2Int pos = new Vector2Int(x, y);
            if (tileSystem.IsTileWalkable(pos))
            {
                return pos;
            }
        }
        
        Debug.LogWarning("Could not find a random walkable position");
        return Vector2Int.zero;
    }
}

// Visual representation of a world item
public class WorldItemVisual : MonoBehaviour
{
    public Item item;
    public Vector2Int position;
    public SpriteRenderer spriteRenderer;
    
    public void Initialize(Item item, Vector2Int position)
    {
        this.item = item;
        this.position = position;
        
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        if (spriteRenderer != null && item.definition.sprite != null)
        {
            spriteRenderer.sprite = item.definition.sprite;
        }
    }
}

// Visual representation of a structure
public class StructureVisual : MonoBehaviour
{
    public StructureItem structure;
    public Vector2Int position;
    public SpriteRenderer spriteRenderer;
    
    public void Initialize(StructureItem structure, Vector2Int position)
    {
        this.structure = structure;
        this.position = position;
        
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        if (spriteRenderer != null && structure.definition.sprite != null)
        {
            spriteRenderer.sprite = structure.definition.sprite;
        }
    }
    
    private void Update()
    {
        // Update visual based on structure state
        if (structure != null && spriteRenderer != null)
        {
            // Update color based on durability
            spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, structure.durability));
        }
    }
}

// Area types for designated areas
public enum AreaType
{
    None,
    Stockpile,
    Social,
    Sleeping,
    Forbidden,
    Growing
}