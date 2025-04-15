using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Handles map generation and tile management
public class TileSystem : MonoBehaviour
{
    [Header("Map Settings")]
    public int width = 100;
    public int height = 100;
    public float tileSize = 1f;
    
    [Header("Tile Prefabs")]
    public GameObject tilePrefab;
    public GameObject wallPrefab;
    
    [Header("Tile Visuals")]
    public Sprite[] groundSprites;
    public Sprite[] wallSprites;
    
    [Header("Generation Settings")]
    public float noiseScale = 0.1f;
    public float wallThreshold = 0.7f;
    public int smoothingIterations = 3;
    public int roomDetectionThreshold = 20;
    
    // Tile data
    private TileData[,] tiles;
    
    // Generated map tracking
    public bool IsInitialized { get; private set; } = false;
    
    // Transform for organizing tiles in hierarchy
    private Transform tilesParent;
    
    // Singleton instance
    private static TileSystem _instance;
    
    // Public property to access the singleton
    public static TileSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TileSystem>();
                
                if (_instance == null)
                {
                    Debug.LogError("No TileSystem found in scene!");
                }
            }
            
            return _instance;
        }
    }
    
    // Make sure only one instance exists
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        
        // Rest of your Awake code...
    }

    // Start is called before the first frame update
    void Start()
    {
        // Create parent transform
        tilesParent = new GameObject("Tiles").transform;
        tilesParent.SetParent(transform);
        
        // Generate map
        GenerateMap();
    }
    
    // Generate the map
    public void GenerateMap()
    {
        // Initialize tile array
        tiles = new TileData[width, height];
        
        // Generate initial noise map
        GenerateNoiseMap();
        
        // Smooth the map
        for (int i = 0; i < smoothingIterations; i++)
        {
            SmoothMap();
        }
        
        // Create room connections
        ConnectRooms();
        
        // Create tile visuals
        CreateTileVisuals();
        
        IsInitialized = true;
        Debug.Log("Map generation complete");
    }
    
    // Generate initial noise map
    private void GenerateNoiseMap()
    {
        // Random offset for noise
        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Generate perlin noise
                float noise = Mathf.PerlinNoise((x + offsetX) * noiseScale, (y + offsetY) * noiseScale);
                
                // Determine tile type based on noise
                bool isWall = noise > wallThreshold;
                
                // Create tile data
                tiles[x, y] = new TileData
                {
                    position = new Vector2Int(x, y),
                    isWall = isWall,
                    isWalkable = !isWall,
                    blocksSight = isWall
                };
            }
        }
        
        // Ensure border walls
        CreateBorderWalls();
    }
    
    // Create border walls around the map
    private void CreateBorderWalls()
    {
        for (int x = 0; x < width; x++)
        {
            // Bottom wall
            tiles[x, 0].isWall = true;
            tiles[x, 0].isWalkable = false;
            tiles[x, 0].blocksSight = true;
            
            // Top wall
            tiles[x, height - 1].isWall = true;
            tiles[x, height - 1].isWalkable = false;
            tiles[x, height - 1].blocksSight = true;
        }
        
        for (int y = 0; y < height; y++)
        {
            // Left wall
            tiles[0, y].isWall = true;
            tiles[0, y].isWalkable = false;
            tiles[0, y].blocksSight = true;
            
            // Right wall
            tiles[width - 1, y].isWall = true;
            tiles[width - 1, y].isWalkable = false;
            tiles[width - 1, y].blocksSight = true;
        }
    }
    
    // Smooth the map using cellular automata
    private void SmoothMap()
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                // Count adjacent walls
                int wallCount = CountAdjacentWalls(x, y, 1);
                
                // Apply cellular automata rules
                if (wallCount > 4) // If surrounded by walls, become a wall
                {
                    tiles[x, y].isWall = true;
                    tiles[x, y].isWalkable = false;
                    tiles[x, y].blocksSight = true;
                }
                else if (wallCount < 3) // If few walls nearby, become a floor
                {
                    tiles[x, y].isWall = false;
                    tiles[x, y].isWalkable = true;
                    tiles[x, y].blocksSight = false;
                }
            }
        }
    }
    
    // Count adjacent walls within range
    private int CountAdjacentWalls(int centerX, int centerY, int range)
    {
        int wallCount = 0;
        
        for (int x = centerX - range; x <= centerX + range; x++)
        {
            for (int y = centerY - range; y <= centerY + range; y++)
            {
                // Skip if out of bounds or if it's the center tile
                if (x < 0 || y < 0 || x >= width || y >= height || (x == centerX && y == centerY))
                {
                    continue;
                }
                
                if (tiles[x, y].isWall)
                {
                    wallCount++;
                }
            }
        }
        
        return wallCount;
    }
    
    // Connect separated rooms
    private void ConnectRooms()
    {
        // Find all rooms using flood fill
        List<List<Vector2Int>> rooms = FindRooms();
        
        // Skip if there's only one room
        if (rooms.Count <= 1)
        {
            return;
        }
        
        // Sort rooms by size (largest first)
        rooms.Sort((a, b) => b.Count.CompareTo(a.Count));
        
        // Keep track of connected rooms
        HashSet<int> connectedRooms = new HashSet<int> { 0 }; // Start with the largest room
        List<Vector2Int> mainRoom = rooms[0];
        
        // Connect all rooms to the main room
        for (int i = 1; i < rooms.Count; i++)
        {
            if (rooms[i].Count < roomDetectionThreshold)
            {
                // Fill small rooms with walls
                foreach (var tile in rooms[i])
                {
                    tiles[tile.x, tile.y].isWall = true;
                    tiles[tile.x, tile.y].isWalkable = false;
                    tiles[tile.x, tile.y].blocksSight = true;
                }
                continue;
            }
            
            // Find closest tiles between main room and current room
            Vector2Int bestTileA = Vector2Int.zero;
            Vector2Int bestTileB = Vector2Int.zero;
            int bestDistance = int.MaxValue;
            
            foreach (var tileA in mainRoom)
            {
                foreach (var tileB in rooms[i])
                {
                    int distance = Mathf.Abs(tileA.x - tileB.x) + Mathf.Abs(tileA.y - tileB.y);
                    
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestTileA = tileA;
                        bestTileB = tileB;
                    }
                }
            }
            
            // Create path between the two closest tiles
            CreatePath(bestTileA, bestTileB);
            
            // Add current room to connected rooms
            connectedRooms.Add(i);
            
            // Add current room tiles to main room
            mainRoom.AddRange(rooms[i]);
        }
    }
    
    // Find all rooms using flood fill
    private List<List<Vector2Int>> FindRooms()
    {
        List<List<Vector2Int>> rooms = new List<List<Vector2Int>>();
        bool[,] visited = new bool[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!visited[x, y] && !tiles[x, y].isWall)
                {
                    // Found unvisited floor tile, start a new room
                    List<Vector2Int> room = new List<Vector2Int>();
                    FloodFill(x, y, visited, room);
                    
                    if (room.Count > 0)
                    {
                        rooms.Add(room);
                    }
                }
            }
        }
        
        return rooms;
    }
    
    // Flood fill algorithm to find connected tiles
    private void FloodFill(int startX, int startY, bool[,] visited, List<Vector2Int> room)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        
        while (queue.Count > 0)
        {
            Vector2Int tile = queue.Dequeue();
            
            // Skip if already visited or wall
            if (visited[tile.x, tile.y] || tiles[tile.x, tile.y].isWall)
            {
                continue;
            }
            
            // Mark as visited and add to room
            visited[tile.x, tile.y] = true;
            room.Add(tile);
            
            // Check adjacent tiles
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),  // Up
                new Vector2Int(1, 0),  // Right
                new Vector2Int(0, -1), // Down
                new Vector2Int(-1, 0)  // Left
            };
            
            foreach (var dir in directions)
            {
                int newX = tile.x + dir.x;
                int newY = tile.y + dir.y;
                
                // Skip if out of bounds
                if (newX < 0 || newY < 0 || newX >= width || newY >= height)
                {
                    continue;
                }
                
                // Add to queue if not visited and not a wall
                if (!visited[newX, newY] && !tiles[newX, newY].isWall)
                {
                    queue.Enqueue(new Vector2Int(newX, newY));
                }
            }
        }
    }
    
    // Create a path between two points
    private void CreatePath(Vector2Int start, Vector2Int end)
    {
        // Simple implementation using modified Bresenham's line algorithm
        int x = start.x;
        int y = start.y;
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1;
        int sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;
        
        while (x != end.x || y != end.y)
        {
            // Carve path (make floor)
            tiles[x, y].isWall = false;
            tiles[x, y].isWalkable = true;
            tiles[x, y].blocksSight = false;
            
            // Also carve adjacent tiles to make a wider path
            for (int nx = x - 1; nx <= x + 1; nx++)
            {
                for (int ny = y - 1; ny <= y + 1; ny++)
                {
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                    {
                        tiles[nx, ny].isWall = false;
                        tiles[nx, ny].isWalkable = true;
                        tiles[nx, ny].blocksSight = false;
                    }
                }
            }
            
            // Bresenham's algorithm
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }
    
    // Create visual representation of tiles
    private void CreateTileVisuals()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CreateTileVisual(x, y);
            }
        }
    }
    
    // Create visual for a single tile
    private void CreateTileVisual(int x, int y)
    {
        Vector3 position = GetWorldPosition(new Vector2Int(x, y));
        GameObject tilePrefabToUse = tiles[x, y].isWall ? wallPrefab : tilePrefab;
        
        // Create tile game object
        GameObject tileObj = Instantiate(tilePrefabToUse, position, Quaternion.identity, tilesParent);
        tileObj.name = $"Tile_{x}_{y}";
        
        // Get sprite renderer
        SpriteRenderer spriteRenderer = tileObj.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // Select random sprite based on tile type
            if (tiles[x, y].isWall && wallSprites.Length > 0)
            {
                spriteRenderer.sprite = wallSprites[Random.Range(0, wallSprites.Length)];
            }
            else if (!tiles[x, y].isWall && groundSprites.Length > 0)
            {
                spriteRenderer.sprite = groundSprites[Random.Range(0, groundSprites.Length)];
            }
        }
        
        // Store reference to game object
        tiles[x, y].tileObject = tileObj;
    }
    
    // Get the world position for a tile position
    public Vector3 GetWorldPosition(Vector2Int tilePosition)
    {
        return new Vector3(tilePosition.x * tileSize, tilePosition.y * tileSize, 0f);
    }
    
    // Get the tile position for a world position
    public Vector2Int GetTilePosition(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / tileSize);
        int y = Mathf.FloorToInt(worldPosition.y / tileSize);
        
        // Clamp to map bounds
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        
        return new Vector2Int(x, y);
    }
    
    // Check if a tile exists at position
    public bool IsTileExists(Vector2Int position)
    {
        return position.x >= 0 && position.y >= 0 && position.x < width && position.y < height;
    }
    
    // Check if a tile is walkable
    public bool IsTileWalkable(Vector2Int position)
    {
        if (!IsTileExists(position))
            return false;
        
        return tiles[position.x, position.y].isWalkable;
    }
    
    // Set tile walkable state
    public void SetTileWalkable(Vector2Int position, bool walkable)
    {
        if (!IsTileExists(position))
            return;
        
        tiles[position.x, position.y].isWalkable = walkable;
    }
    
    // Get tile data
    public TileData GetTileData(Vector2Int position)
    {
        if (!IsTileExists(position))
            return null;
        
        return tiles[position.x, position.y];
    }
    
    // Find all tiles of a certain type (e.g. walkable)
    public List<Vector2Int> FindTilesOfType(bool isWalkable)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (tiles[x, y].isWalkable == isWalkable)
                {
                    result.Add(new Vector2Int(x, y));
                }
            }
        }
        
        return result;
    }
    
    // Find random walkable tile
    public Vector2Int GetRandomWalkableTile()
    {
        List<Vector2Int> walkableTiles = FindTilesOfType(true);
        
        if (walkableTiles.Count > 0)
        {
            return walkableTiles[Random.Range(0, walkableTiles.Count)];
        }
        
        // Fallback
        return new Vector2Int(width / 2, height / 2);
    }
}

// Data structure for individual tiles
public class TileData
{
    public Vector2Int position;
    public bool isWall;
    public bool isWalkable;
    public bool blocksSight;
    public GameObject tileObject;
    public TerrainTag terrainTag;
    
    // Additional properties for gameplay
    public Dictionary<string, object> customData = new Dictionary<string, object>();
}