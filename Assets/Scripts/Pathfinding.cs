using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PathNode
{
    public Vector2Int position;
    public bool walkable;

    public int gCost; // Cost from start
    public int hCost; // Heuristic cost to goal
    public int fCost => gCost + hCost;

    public PathNode parent;

    public PathNode(Vector2Int pos, bool isWalkable)
    {
        position = pos;
        walkable = isWalkable;
    }
}

public class Pathfinding : MonoBehaviour
{
    public static Pathfinding Instance { get; private set; }

    private PathNode[,] grid;
    private int gridWidth, gridHeight;
    private bool isInitialized = false;

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
    }

    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        yield return new WaitUntil(() => TileSystem.Instance != null &&
                                         TileSystem.Instance.IsInitialized);
        InitializeGrid();
        isInitialized = true;
        Debug.Log("Pathfinding system initialized");
    }

    public bool IsInitialized()
    {
        return isInitialized;
    }

    public void InitializeGrid()
    {
        if (TileSystem.Instance == null)
        {
            Debug.LogError("Cannot initialize pathfinding grid: TileSystem is null");
            return;
        }

        gridWidth = TileSystem.Instance.mapWidth;
        gridHeight = TileSystem.Instance.mapHeight;

        grid = new PathNode[gridWidth, gridHeight];

        // Initialize grid nodes
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Tile tile = TileSystem.Instance.GetTile(x, y);
                if (tile != null)
                {
                    grid[x, y] = new PathNode(new Vector2Int(x, y), tile.walkable);
                }
                else
                {
                    grid[x, y] = new PathNode(new Vector2Int(x, y), false);
                }
            }
        }
    }

    public void UpdateNode(int x, int y, bool walkable)
    {
        if (x >= 0 && y >= 0 && x < gridWidth && y < gridHeight)
        {
            grid[x, y].walkable = walkable;
        }
    }

    public List<Vector2Int> FindPath(Vector2Int startPos, Vector2Int targetPos)
    {
        // Make sure grid is initialized
        if (grid == null || !isInitialized)
        {
            Debug.LogWarning("Attempted to find path before pathfinding was initialized");
            return null;
        }

        // Check if start or target is out of bounds or unwalkable
        if (!IsValidPosition(startPos) || !IsValidPosition(targetPos))
        {
            return null;
        }

        // If target is unwalkable, find nearest walkable tile
        if (!GetNode(targetPos).walkable)
        {
            targetPos = FindNearestWalkableTile(targetPos);
            if (targetPos == new Vector2Int(-1, -1))
            {
                return null; // No walkable tiles found
            }
        }

        // Initialize lists for A* algorithm
        List<PathNode> openList = new List<PathNode>();
        HashSet<PathNode> closedList = new HashSet<PathNode>();

        PathNode startNode = GetNode(startPos);
        PathNode targetNode = GetNode(targetPos);

        openList.Add(startNode);

        // Reset nodes for new path calculation
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                PathNode node = grid[x, y];
                node.gCost = int.MaxValue;
                node.parent = null;
            }
        }

        startNode.gCost = 0;
        startNode.hCost = CalculateDistance(startPos, targetPos);

        while (openList.Count > 0)
        {
            // Get node with lowest fCost
            PathNode currentNode = openList.OrderBy(x => x.fCost).ThenBy(x => x.hCost).First();

            // Remove current from open list and add to closed list
            openList.Remove(currentNode);
            closedList.Add(currentNode);

            // Check if reached target
            if (currentNode.position == targetPos)
            {
                return RetracePath(startNode, targetNode);
            }

            // Check all neighbors
            foreach (PathNode neighbor in GetNeighbors(currentNode))
            {
                if (!neighbor.walkable || closedList.Contains(neighbor))
                {
                    continue;
                }

                int tentativeGCost = currentNode.gCost + CalculateDistance(currentNode.position, neighbor.position);

                if (tentativeGCost < neighbor.gCost || !openList.Contains(neighbor))
                {
                    neighbor.gCost = tentativeGCost;
                    neighbor.hCost = CalculateDistance(neighbor.position, targetPos);
                    neighbor.parent = currentNode;

                    if (!openList.Contains(neighbor))
                    {
                        openList.Add(neighbor);
                    }
                }
            }
        }

        // If we get here, there is no path to the target
        return null;
    }

    private Vector2Int FindNearestWalkableTile(Vector2Int position)
    {
        // Simple BFS to find nearest walkable tile
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(position);
        visited.Add(position);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            // If current is walkable, return it
            if (IsValidPosition(current) && GetNode(current).walkable)
            {
                return current;
            }

            // Check neighbors
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // Up
                new Vector2Int(1, 0),   // Right
                new Vector2Int(0, -1),  // Down
                new Vector2Int(-1, 0)   // Left
            };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (IsValidPosition(neighbor) && !visited.Contains(neighbor))
                {
                    queue.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }
        }

        // No walkable tile found
        return new Vector2Int(-1, -1);
    }

    private List<Vector2Int> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;

            // Safety check
            if (currentNode == null)
            {
                break;
            }
        }

        path.Reverse();
        return path;
    }

    private List<PathNode> GetNeighbors(PathNode node)
    {
        List<PathNode> neighbors = new List<PathNode>();

        // Check the 4 adjacent nodes (no diagonals for simplicity)
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Up
            new Vector2Int(1, 0),   // Right
            new Vector2Int(0, -1),  // Down
            new Vector2Int(-1, 0)   // Left
        };

        foreach (Vector2Int dir in directions)
        {
            Vector2Int neighborPos = node.position + dir;

            if (IsValidPosition(neighborPos))
            {
                neighbors.Add(GetNode(neighborPos));
            }
        }

        return neighbors;
    }

    private int CalculateDistance(Vector2Int a, Vector2Int b)
    {
        // Manhattan distance for 4-way movement
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.y >= 0 && pos.x < gridWidth && pos.y < gridHeight;
    }

    private PathNode GetNode(Vector2Int pos)
    {
        if (IsValidPosition(pos))
        {
            return grid[pos.x, pos.y];
        }
        return null;
    }

    // Debug method to visualize path
    public void DebugDrawPath(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0)
            return;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 start = TileSystem.Instance.TileToWorldPosition(path[i]) + new Vector3(0.5f, 0.5f, 0);
            Vector3 end = TileSystem.Instance.TileToWorldPosition(path[i + 1]) + new Vector3(0.5f, 0.5f, 0);
            Debug.DrawLine(start, end, Color.red, 1f);
        }
    }
}