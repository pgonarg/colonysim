using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [Header("Creature Settings")]
    public GameObject creaturePrefab;
    public int initialCreatureCount = 5;
    public Sprite[] creatureSprites;

    [Header("UI")]
    public TextMeshProUGUI statusText;
    public Button designateStockpileButton;
    public Button designateSocialAreaButton;
    public Button createCreatureButton;
    public TMP_Dropdown behaviorDropdown;
    public TMP_Dropdown buildingSelectionDropdown;
    public Button buildButton;
    public Button placeFurnitureButton;
    public TMP_Dropdown furnitureSelectionDropdown;

    [Header("Game Settings")]
    public float foodRegrowInterval = 10f;

    // Game state
    private TileType tilePlacementMode = TileType.Ground;
    private float foodRegrowTimer = 0f;
    private List<Creature> creatures = new List<Creature>();
    private Structure selectedStructure;
    private Item selectedFurniture;
    private bool isBuildingMode = false;
    private bool isPlacingFurniture = false;

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
        // Initialize UI
        if (designateStockpileButton != null)
            designateStockpileButton.onClick.AddListener(() => SetTilePlacementMode(TileType.Stockpile));

        if (designateSocialAreaButton != null)
            designateSocialAreaButton.onClick.AddListener(() => SetTilePlacementMode(TileType.SocialArea));

        if (createCreatureButton != null)
            createCreatureButton.onClick.AddListener(CreateNewCreature);

        if (behaviorDropdown != null)
        {
            behaviorDropdown.ClearOptions();
            List<string> options = new List<string>
            {
                "Prioritize Lowest Need",
                "Prioritize Fastest Fulfillment"
            };
            behaviorDropdown.AddOptions(options);
            behaviorDropdown.onValueChanged.AddListener(OnBehaviorChanged);
        }

        // Set up building UI
        if (buildingSelectionDropdown != null)
        {
            buildingSelectionDropdown.ClearOptions();
            List<string> buildingOptions = new List<string>();

            // Add structure options from database
            if (ItemDatabase.Instance != null)
            {
                foreach (var structureDef in ItemDatabase.Instance.structureDefinitions)
                {
                    buildingOptions.Add(structureDef.structureName);
                }
            }
            else
            {
                // Default options if no database
                buildingOptions.Add("Bed");
                buildingOptions.Add("Table");
                buildingOptions.Add("Chair");
                buildingOptions.Add("Wall");
            }

            buildingSelectionDropdown.AddOptions(buildingOptions);
            buildingSelectionDropdown.onValueChanged.AddListener(OnBuildingTypeChanged);

            // Initialize selected structure
            OnBuildingTypeChanged(buildingSelectionDropdown.value);
        }

        // Set up furniture UI
        if (furnitureSelectionDropdown != null)
        {
            furnitureSelectionDropdown.ClearOptions();
            List<string> furnitureOptions = new List<string>();

            // Add item options from database
            if (ItemDatabase.Instance != null)
            {
                foreach (var itemDef in ItemDatabase.Instance.itemDefinitions)
                {
                    if (!itemDef.isCountable) // Only non-countable items are furniture
                    {
                        furnitureOptions.Add(itemDef.itemName);
                    }
                }
            }
            else
            {
                // Default options if no database
                furnitureOptions.Add("Bed");
                furnitureOptions.Add("Chair");
                furnitureOptions.Add("Table");
                furnitureOptions.Add("Lamp");
            }

            furnitureSelectionDropdown.AddOptions(furnitureOptions);
            furnitureSelectionDropdown.onValueChanged.AddListener(OnFurnitureTypeChanged);

            // Initialize selected furniture
            OnFurnitureTypeChanged(furnitureSelectionDropdown.value);
        }

        if (buildButton != null)
        {
            buildButton.onClick.AddListener(ToggleBuildMode);
        }

        if (placeFurnitureButton != null)
        {
            placeFurnitureButton.onClick.AddListener(ToggleFurnitureMode);
        }

        // Wait for TileSystem to initialize
        StartCoroutine(InitializeAfterTileSystem());
    }

    private IEnumerator InitializeAfterTileSystem()
    {
        Debug.Log("Starting world initialization...");

        // Wait for TileSystem to generate map
        yield return new WaitUntil(() => TileSystem.Instance != null && TileSystem.Instance.IsMapGenerated());
        Debug.Log("TileSystem initialized");

        // Wait for Pathfinding to initialize
        yield return new WaitUntil(() => Pathfinding.Instance != null && Pathfinding.Instance.IsInitialized());
        Debug.Log("Pathfinding initialized");

        // Now we can safely create creatures
        for (int i = 0; i < initialCreatureCount; i++)
        {
            CreateNewCreature();
            // Add a small delay between creature creation to avoid spikes
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("World initialization complete. Created " + initialCreatureCount + " creatures.");
    }

    private void Update()
    {
        // Handle tile placement with mouse
        if (Input.GetMouseButtonDown(0))
        {
            PlaceTileAtMousePosition();
        }

        // Food regrow timer
        foodRegrowTimer += Time.deltaTime;
        if (foodRegrowTimer >= foodRegrowInterval)
        {
            foodRegrowTimer = 0f;
            TileSystem.Instance.RegrowFood();
        }

        // Update status text
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            statusText.text = $"Creatures: {creatures.Count}\n";
            statusText.text += $"Placement Mode: {tilePlacementMode}\n";
            statusText.text += "Left-click to place tiles";
        }
    }

    private void SetTilePlacementMode(TileType type)
    {
        tilePlacementMode = type;
    }

    private void PlaceTileAtMousePosition()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int tilePos = TileSystem.Instance.WorldToTilePosition(mousePos);

        TileSystem.Instance.SetTileType(tilePos.x, tilePos.y, tilePlacementMode);
    }

    private void CreateNewCreature()
    {
        // Find a random walkable tile for spawn
        List<Tile> walkableTiles = new List<Tile>();

        for (int x = 0; x < TileSystem.Instance.mapWidth; x++)
        {
            for (int y = 0; y < TileSystem.Instance.mapHeight; y++)
            {
                Tile tile = TileSystem.Instance.GetTile(x, y);
                if (tile.walkable)
                {
                    walkableTiles.Add(tile);
                }
            }
        }

        if (walkableTiles.Count == 0)
        {
            Debug.LogError("No walkable tiles found for creature spawn!");
            return;
        }

        // Select random walkable tile
        Tile spawnTile = walkableTiles[Random.Range(0, walkableTiles.Count)];
        Vector3 spawnPos = TileSystem.Instance.TileToWorldPosition(spawnTile.position);

        // Set Z position to negative value to ensure creatures appear in front of tiles
        spawnPos.z = -1f;

        // Create creature
        GameObject creatureObj = Instantiate(creaturePrefab, spawnPos, Quaternion.identity);
        Creature creature = creatureObj.GetComponent<Creature>();

        if (creature != null)
        {
            // Set random name
            creature.creatureName = GenerateRandomName();

            // Set random sprite if available
            if (creatureSprites != null && creatureSprites.Length > 0 && creature.spriteRenderer != null)
            {
                creature.spriteRenderer.sprite = creatureSprites[Random.Range(0, creatureSprites.Length)];
            }

            // Add to list
            creatures.Add(creature);

            // Set current behavior
            if (behaviorDropdown != null)
            {
                OnBehaviorChanged(behaviorDropdown.value);
            }
        }
    }

    private void OnBehaviorChanged(int value)
    {
        BehaviorType newBehavior = value == 0 ? BehaviorType.PrioritizeLowest : BehaviorType.PrioritizeFastest;

        // Update all creatures
        foreach (Creature creature in creatures)
        {
            creature.behavior = newBehavior;
        }
    }

    private string GenerateRandomName()
    {
        string[] prefixes = { "Urist", "Durin", "Gimli", "Dori", "Balin", "Thorin", "Fili", "Kili", "Bifur", "Bofur" };
        string[] suffixes = { "son", "dottir", "hand", "beard", "axe", "hammer", "forge", "mine", "gold", "steel" };

        return prefixes[Random.Range(0, prefixes.Length)] + " " + suffixes[Random.Range(0, suffixes.Length)];
    }

    // Call when creature is destroyed
    public void RemoveCreature(Creature creature)
    {
        if (creatures.Contains(creature))
        {
            creatures.Remove(creature);
        }
    }
    private void OnBuildingTypeChanged(int index)
    {
        if (ItemDatabase.Instance != null && index < ItemDatabase.Instance.structureDefinitions.Count)
        {
            var structureDef = ItemDatabase.Instance.structureDefinitions[index];
            selectedStructure = ItemDatabase.Instance.CreateStructure(structureDef.structureName);
        }
        else
        {
            // Default structure if no database
            Dictionary<ItemTags, int> costs = new Dictionary<ItemTags, int>
            {
                { ItemTags.BuildingMaterial, 2 }
            };

            selectedStructure = new Structure("Generic Structure", costs, NeedTags.Shelter);
        }
    }

    private void OnFurnitureTypeChanged(int index)
    {
        if (ItemDatabase.Instance != null && index < ItemDatabase.Instance.itemDefinitions.Count)
        {
            var itemDef = ItemDatabase.Instance.itemDefinitions[index];
            selectedFurniture = ItemDatabase.Instance.CreateItem(itemDef.itemName);
        }
        else
        {
            // Default furniture if no database
            selectedFurniture = new Item("Generic Furniture", ItemTags.Decoration, NeedTags.Comfort);
            selectedFurniture.isCountable = false;
        }
    }

    private void ToggleBuildMode()
    {
        isBuildingMode = !isBuildingMode;

        if (isBuildingMode)
        {
            isPlacingFurniture = false; // Turn off furniture placement

            if (buildButton != null)
                buildButton.GetComponentInChildren<TextMeshProUGUI>().text = "Cancel Build";

            if (placeFurnitureButton != null)
                placeFurnitureButton.GetComponentInChildren<TextMeshProUGUI>().text = "Place Furniture";
        }
        else
        {
            if (buildButton != null)
                buildButton.GetComponentInChildren<TextMeshProUGUI>().text = "Build";
        }
    }

    private void ToggleFurnitureMode()
    {
        isPlacingFurniture = !isPlacingFurniture;

        if (isPlacingFurniture)
        {
            isBuildingMode = false; // Turn off building mode

            if (placeFurnitureButton != null)
                placeFurnitureButton.GetComponentInChildren<TextMeshProUGUI>().text = "Cancel Placement";

            if (buildButton != null)
                buildButton.GetComponentInChildren<TextMeshProUGUI>().text = "Build";
        }
        else
        {
            if (placeFurnitureButton != null)
                placeFurnitureButton.GetComponentInChildren<TextMeshProUGUI>().text = "Place Furniture";
        }
    }
}