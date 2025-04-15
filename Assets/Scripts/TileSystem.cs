using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TileType
{
    Ground,
    Wall,
    FoodSource,
    Stockpile,
    SocialArea,
    Structure
}

public class Tile
{
    public Vector2Int position;
    public TileType type;
    public bool walkable;
    public float foodAmount;
    public GameObject tileObject;
    public SpriteRenderer spriteRenderer;

    // Item and structure properties
    public Item placedItem;
    public Structure structure;
    public bool isInUse = false;
    public Creature userCreature;

    // For tracking tile occupation by actions
    private Dictionary<CreatureState, Creature> occupyingCreatures = new Dictionary<CreatureState, Creature>();

    public Tile(Vector2Int pos, TileType tileType, bool isWalkable)
    {
        position = pos;
        type = tileType;
        walkable = isWalkable;
        foodAmount = type == TileType.FoodSource ? Random.Range(50f, 100f) : 0f;
    }

    public bool HasFood()
    {
        return type == TileType.FoodSource && foodAmount > 0f;
    }

    public float GatherFood(float amount)
    {
        float gathered = Mathf.Min(amount, foodAmount);
        foodAmount -= gathered;

        // If depleted, regrow after some time
        if (foodAmount <= 0f && type == TileType.FoodSource)
        {
            foodAmount = 0f;
            // Food source will regrow in WorldManager
        }

        return gathered;
    }

    // Methods for tile occupation
    public bool CanOccupyForState(CreatureState state, Creature creature)
    {
        // Moving states can share tiles
        if (state == CreatureState.MovingToFood || state == CreatureState.MovingToSleep ||
            state == CreatureState.MovingToDrink || state == CreatureState.MovingToSocial ||
            state == CreatureState.MovingToStockpile)
        {
            return walkable;
        }

        // Action states require exclusive tile use
        return walkable && !occupyingCreatures.ContainsKey(state);
    }

    public bool OccupyForState(CreatureState state, Creature creature)
    {
        // Moving states don't actually occupy
        if (state == CreatureState.MovingToFood || state == CreatureState.MovingToSleep ||
            state == CreatureState.MovingToDrink || state == CreatureState.MovingToSocial ||
            state == CreatureState.MovingToStockpile)
        {
            return true;
        }

        // Check if already occupied
        if (occupyingCreatures.ContainsKey(state))
        {
            return false;
        }

        // Occupy the tile
        occupyingCreatures[state] = creature;
        return true;
    }

    public void ReleaseFromState(CreatureState state, Creature creature)
    {
        if (occupyingCreatures.TryGetValue(state, out Creature occupier))
        {
            if (occupier == creature)
            {
                occupyingCreatures.Remove(state);
            }
        }
    }

    // Structure and item methods
    public bool CanPlaceStructure()
    {
        return type == TileType.Ground && structure == null;
    }

    public bool PlaceStructure(Structure newStructure)
    {
        if (CanPlaceStructure())
        {
            structure = newStructure;
            type = TileType.Structure;

            // Update walkability based on the structure
            walkable = structure.walkable;

            return true;
        }
        return false;
    }

    public bool RemoveStructure()
    {
        if (structure != null)
        {
            structure = null;
            type = TileType.Ground;
            walkable = true;
            return true;
        }
        return false;
    }

    public bool CanPlaceItem()
    {
        return (type == TileType.Ground || type == TileType.Structure) && placedItem == null;
    }

    public bool PlaceItem(Item newItem)
    {
        if (CanPlaceItem())
        {
            // If it's a structure, check if it can hold this item
            if (type == TileType.Structure && structure != null)
            {
                if (!structure.allowedItems.Contains(newItem.tags))
                {
                    return false;
                }
            }

            placedItem = newItem;
            return true;
        }
        return false;
    }

    public Item RemoveItem()
    {
        Item removedItem = placedItem;
        placedItem = null;
        return removedItem;
    }

    public bool CanBeUsedForNeed(NeedType needType, Creature creature)
    {
        // Check if tile is already in use
        if (isInUse && userCreature != creature)
        {
            return false;
        }

        // Check if there's a placed item that satisfies this need
        if (placedItem != null && placedItem.CanSatisfyNeed(needType))
        {
            return true;
        }

        // Check if there's a structure that satisfies this need
        if (structure != null)
        {
            switch (needType)
            {
                case NeedType.Sleep:
                    return structure.providesFor.HasFlag(NeedTags.Sleep);
                case NeedType.Food:
                    return structure.providesFor.HasFlag(NeedTags.Food);
                case NeedType.Drink:
                    return structure.providesFor.HasFlag(NeedTags.Drink);
                case NeedType.Socialization:
                    return structure.providesFor.HasFlag(NeedTags.Socialization);
            }
        }

        // Default checks based on tile type
        switch (needType)
        {
            case NeedType.Food:
                return type == TileType.FoodSource && HasFood();
            case NeedType.Socialization:
                return type == TileType.SocialArea;
            case NeedType.Sleep:
            case NeedType.Drink:
                return type == TileType.Ground;
        }

        return false;
    }

    public void SetInUse(Creature creature)
    {
        isInUse = true;
        userCreature = creature;
    }

    public void SetAvailable()
    {
        isInUse = false;
        userCreature = null;
    }
}

public class TileSystem : MonoBehaviour
{
    public static TileSystem Instance { get; private set; }

    [Header("Tile Settings")]
    public int mapWidth = 50;
    public int mapHeight = 50;
    public float tileSize = 1f;

    [Header("Tile Sprites")]
    public Sprite groundSprite;
    public Sprite wallSprite;
    public Sprite foodSourceSprite;
    public Sprite stockpileSprite;
    public Sprite socialAreaSprite;

    [Header("Generation Settings")]
    public float perlinScale = 0.1f;
    public float wallThreshold = 0.6f;
    public float foodSourceChance = 0.05f;

    private Tile[,] tiles;
    private Transform tilesParent;
    private bool mapGenerated = false;

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

        tilesParent = new GameObject("Tiles").transform;
        tilesParent.SetParent(transform);
    }

    private void Start()
    {
        GenerateMap();
    }

    public bool IsMapGenerated()
    {
        return mapGenerated;
    }

    public void GenerateMap()
    {
        tiles = new Tile[mapWidth, mapHeight];

        // Generate perlin noise map
        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                // Generate perlin noise value
                float perlinValue = Mathf.PerlinNoise((x + offsetX) * perlinScale, (y + offsetY) * perlinScale);

                // Determine tile type based on perlin value
                TileType tileType;
                bool walkable;

                if (perlinValue > wallThreshold)
                {
                    tileType = TileType.Wall;
                    walkable = false;
                }
                else
                {
                    // Check for food sources
                    if (Random.value < foodSourceChance && perlinValue < wallThreshold - 0.1f)
                    {
                        tileType = TileType.FoodSource;
                    }
                    else
                    {
                        tileType = TileType.Ground;
                    }
                    walkable = true;
                }

                // Create tile
                tiles[x, y] = new Tile(new Vector2Int(x, y), tileType, walkable);
                CreateTileVisual(tiles[x, y]);
            }
        }

        // Create some larger connected areas of walkable space
        SmoothMap(3);

        // Update visuals after smoothing
        UpdateTileVisuals();

        mapGenerated = true;
        Debug.Log("Map generation complete");
    }

    private void SmoothMap(int iterations)
    {
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    // Count adjacent walls
                    int adjacentWalls = CountAdjacentWalls(x, y, 1);

                    if (adjacentWalls > 4) // If surrounded by walls
                    {
                        tiles[x, y].type = TileType.Wall;
                        tiles[x, y].walkable = false;
                    }
                    else if (adjacentWalls < 3) // If open space
                    {
                        // Keep food sources if they already exist
                        if (tiles[x, y].type != TileType.FoodSource)
                        {
                            tiles[x, y].type = TileType.Ground;
                            tiles[x, y].walkable = true;
                        }
                    }
                }
            }
        }
    }

    private int CountAdjacentWalls(int x, int y, int range)
    {
        int count = 0;

        for (int i = -range; i <= range; i++)
        {
            for (int j = -range; j <= range; j++)
            {
                // Skip the center tile
                if (i == 0 && j == 0)
                    continue;

                int nx = x + i;
                int ny = y + j;

                // Check if out of bounds or wall
                if (nx < 0 || ny < 0 || nx >= mapWidth || ny >= mapHeight)
                {
                    count++; // Count out of bounds as walls
                }
                else if (tiles[nx, ny].type == TileType.Wall)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void CreateTileVisual(Tile tile)
    {
        GameObject tileObj = new GameObject($"Tile_{tile.position.x}_{tile.position.y}");
        tileObj.transform.SetParent(tilesParent);
        tileObj.transform.position = new Vector3(tile.position.x * tileSize, tile.position.y * tileSize, 0f);

        SpriteRenderer spriteRenderer = tileObj.AddComponent<SpriteRenderer>();

        // Set the sorting order to ensure proper layering
        spriteRenderer.sortingOrder = 0; // Base layer for tiles

        tile.tileObject = tileObj;
        tile.spriteRenderer = spriteRenderer;

        // Set sprite based on tile type
        UpdateTileSprite(tile);
    }

    private void UpdateTileVisuals()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                UpdateTileSprite(tiles[x, y]);
            }
        }
    }

    private void UpdateTileSprite(Tile tile)
    {
        if (tile.spriteRenderer == null) return;

        switch (tile.type)
        {
            case TileType.Ground:
                tile.spriteRenderer.sprite = groundSprite;
                break;
            case TileType.Wall:
                tile.spriteRenderer.sprite = wallSprite;
                break;
            case TileType.FoodSource:
                tile.spriteRenderer.sprite = foodSourceSprite;
                // Optionally adjust transparency based on food amount
                Color foodColor = tile.spriteRenderer.color;
                foodColor.a = Mathf.Lerp(0.5f, 1f, tile.foodAmount / 100f);
                tile.spriteRenderer.color = foodColor;
                break;
            case TileType.Stockpile:
                tile.spriteRenderer.sprite = stockpileSprite;
                break;
            case TileType.SocialArea:
                tile.spriteRenderer.sprite = socialAreaSprite;
                break;
        }
    }

    public void SetTileType(int x, int y, TileType newType)
    {
        if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight)
            return;

        // Don't change wall types
        if (tiles[x, y].type == TileType.Wall)
            return;

        // Handle special case for stockpile
        if (newType == TileType.Stockpile)
        {
            // Register with stockpile system
            if (StockpileSystem.Instance != null)
            {
                StockpileSystem.Instance.RegisterStockpileTile(new Vector2Int(x, y));
            }
        }
        else if (tiles[x, y].type == TileType.Stockpile)
        {
            // Unregister from stockpile system
            if (StockpileSystem.Instance != null)
            {
                StockpileSystem.Instance.UnregisterStockpileTile(new Vector2Int(x, y));
            }
        }

        tiles[x, y].type = newType;

        // Special handling for different tile types
        if (newType == TileType.FoodSource && tiles[x, y].foodAmount <= 0)
        {
            tiles[x, y].foodAmount = Random.Range(50f, 100f);
        }

        UpdateTileSprite(tiles[x, y]);
    }

    public Tile GetTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight)
            return null;

        return tiles[x, y];
    }

    public Tile GetTile(Vector2Int position)
    {
        return GetTile(position.x, position.y);
    }

    public List<Tile> GetAllTilesOfType(TileType type)
    {
        List<Tile> result = new List<Tile>();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (tiles[x, y].type == type)
                {
                    result.Add(tiles[x, y]);
                }
            }
        }

        return result;
    }

    public Tile GetClosestTileOfType(Vector2Int fromPosition, TileType type, bool mustHaveFood = false)
    {
        Tile closest = null;
        float closestDistance = float.MaxValue;

        List<Tile> tilesOfType = GetAllTilesOfType(type);

        foreach (Tile tile in tilesOfType)
        {
            if (mustHaveFood && !tile.HasFood())
                continue;

            float distance = Vector2Int.Distance(fromPosition, tile.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = tile;
            }
        }

        return closest;
    }

    public void RegrowFood()
    {
        List<Tile> foodSources = GetAllTilesOfType(TileType.FoodSource);

        foreach (Tile tile in foodSources)
        {
            // Slowly regrow food
            if (tile.foodAmount < 100f)
            {
                tile.foodAmount += Random.Range(0.5f, 2f);
                tile.foodAmount = Mathf.Min(tile.foodAmount, 100f);

                // Update visual
                if (tile.spriteRenderer != null)
                {
                    Color foodColor = tile.spriteRenderer.color;
                    foodColor.a = Mathf.Lerp(0.5f, 1f, tile.foodAmount / 100f);
                    tile.spriteRenderer.color = foodColor;
                }
            }
        }
    }

    public Vector3 TileToWorldPosition(Vector2Int tilePos)
    {
        return new Vector3(tilePos.x * tileSize, tilePos.y * tileSize, 0f);
    }

    [Header("Mouse Settings")]
    public Vector2 mousePositionOffset = new Vector2(0.5f, 0.5f);

    public Vector2Int WorldToTilePosition(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - mousePositionOffset.x) / tileSize);
        int y = Mathf.FloorToInt((worldPos.y - mousePositionOffset.y) / tileSize);
        return new Vector2Int(x, y);
    }
}