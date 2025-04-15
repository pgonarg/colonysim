using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum BehaviorType
{
    PrioritizeLowest,    // Prioritize the need with the lowest value
    PrioritizeFastest    // Prioritize the need that can be fulfilled fastest
}

public enum CreatureState
{
    Idle,
    MovingToFood,
    MovingToSleep,
    MovingToDrink,
    MovingToSocial,
    MovingToStockpile,
    MovingToStructure,
    MovingToBuildSite,
    Eating,
    Sleeping,
    Drinking,
    Socializing,
    GatheringFood,
    GatheringMaterials,
    Building,
    PlacingItem
}

public enum NeedType
{
    Food,
    Sleep,
    Drink,
    Socialization
}

public class Need
{
    public NeedType type;
    public float value;
    public float decayRate;
    public float criticalThreshold;
    public float fulfillRate;

    public Need(NeedType needType, float initialValue = 100f)
    {
        type = needType;
        value = initialValue;

        // Default values
        switch (type)
        {
            case NeedType.Food:
                decayRate = 0.3f;
                criticalThreshold = 20f;
                fulfillRate = 10f;
                break;
            case NeedType.Sleep:
                decayRate = 0.2f;
                criticalThreshold = 15f;
                fulfillRate = 8f;
                break;
            case NeedType.Drink:
                decayRate = 0.4f;
                criticalThreshold = 25f;
                fulfillRate = 15f;
                break;
            case NeedType.Socialization:
                decayRate = 0.15f;
                criticalThreshold = 10f;
                fulfillRate = 12f;
                break;
        }
    }

    public void Decay(float deltaTime)
    {
        value = Mathf.Max(0f, value - (decayRate * deltaTime));
    }

    public void Fulfill(float amount)
    {
        value = Mathf.Min(100f, value + amount);
    }

    public bool IsCritical()
    {
        return value < criticalThreshold;
    }

    public float GetFulfillmentTime()
    {
        // Estimate how long it would take to fulfill this need from current value to max
        float amountNeeded = 100f - value;
        return amountNeeded / fulfillRate;
    }
}

public class Creature : MonoBehaviour
{
    [Header("Creature Settings")]
    public string creatureName = "Dwarf";
    public float moveSpeed = 2f;
    public BehaviorType behavior = BehaviorType.PrioritizeLowest;
    public SpriteRenderer spriteRenderer;
    public CreatureTags creatureTags = CreatureTags.Worker;

    [Header("Status")]
    public CreatureState currentState = CreatureState.Idle;
    public float stateTimer = 0f;
    public float foodCarried = 0f;
    public float maxFoodCarry = 50f;

    [Header("Inventory")]
    public List<Item> inventory = new List<Item>();
    public int maxInventoryItems = 3;

    [Header("Jobs")]
    public bool hasBuildJob = false;
    public Vector2Int buildJobLocation;
    public Structure buildStructure;
    public List<Item> requiredMaterials = new List<Item>();

    [Header("Needs")]
    public List<Need> needs = new List<Need>();

    [Header("Action Indicator")]
    public GameObject actionIndicatorPrefab;
    private ActionIndicator actionIndicator;

    // Pathing variables
    private List<Vector2Int> currentPath;
    private int currentPathIndex;
    private Vector2Int currentTilePosition;
    private Vector2Int targetTilePosition;
    private Vector3 moveTarget;
    private bool isMoving = false;

    // References
    private Tile targetTile;

    void Start()
    {
        // Initialize needs
        needs.Add(new Need(NeedType.Food, Random.Range(50f, 100f)));
        needs.Add(new Need(NeedType.Sleep, Random.Range(50f, 100f)));
        needs.Add(new Need(NeedType.Drink, Random.Range(50f, 100f)));
        needs.Add(new Need(NeedType.Socialization, Random.Range(50f, 100f)));

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Ensure proper rendering order
        if (spriteRenderer != null)
        {
            // Either use sorting layer
            spriteRenderer.sortingLayerName = "Creatures";
            spriteRenderer.sortingOrder = 1;

            // Or ensure Z position is correct (negative to appear in front of tiles)
            Vector3 pos = transform.position;
            if (pos.z >= 0)
            {
                pos.z = -1f;
                transform.position = pos;
            }
        }

        // Create action indicator
        if (actionIndicatorPrefab != null)
        {
            GameObject indicatorObj = Instantiate(actionIndicatorPrefab, transform.position, Quaternion.identity);
            actionIndicator = indicatorObj.GetComponent<ActionIndicator>();
            if (actionIndicator != null)
            {
                actionIndicator.Initialize(transform);
                actionIndicator.SetActive(false); // Hide initially
            }
        }

        // Wait for systems to initialize
        StartCoroutine(WaitForSystems());
    }

    void Update()
    {
        // Update needs
        foreach (Need need in needs)
        {
            need.Decay(Time.deltaTime);

            // Check if creature should die
            if (need.value <= 0f && need.IsCritical())
            {
                Die();
                return;
            }
        }

        // Handle current state
        switch (currentState)
        {
            case CreatureState.Idle:
                HandleIdleState();
                break;
            case CreatureState.MovingToFood:
            case CreatureState.MovingToSleep:
            case CreatureState.MovingToDrink:
            case CreatureState.MovingToSocial:
            case CreatureState.MovingToStockpile:
            case CreatureState.MovingToBuildSite:
            case CreatureState.MovingToStructure:
                HandleMovingState();
                break;
            case CreatureState.Eating:
                HandleEatingState();
                break;
            case CreatureState.Sleeping:
                HandleSleepingState();
                break;
            case CreatureState.Drinking:
                HandleDrinkingState();
                break;
            case CreatureState.Socializing:
                HandleSocializingState();
                break;
            case CreatureState.GatheringFood:
                HandleGatheringFoodState();
                break;
            case CreatureState.GatheringMaterials:
                HandleGatheringMaterialsState();
                break;
            case CreatureState.Building:
                HandleBuildingState();
                break;
            case CreatureState.PlacingItem:
                HandlePlacingItemState();
                break;
        }

        // Smooth movement between tiles
        if (isMoving)
        {
            // Store the current z position
            float zPos = transform.position.z;

            // Move towards target, but only in x and y
            Vector3 targetWithSameZ = new Vector3(moveTarget.x, moveTarget.y, zPos);
            transform.position = Vector3.MoveTowards(transform.position, targetWithSameZ, moveSpeed * Time.deltaTime);

            if (Vector2.Distance(new Vector2(transform.position.x, transform.position.y),
                                new Vector2(moveTarget.x, moveTarget.y)) < 0.01f)
            {
                isMoving = false;

                // Check if we have more path points
                if (currentPath != null && currentPathIndex < currentPath.Count - 1)
                {
                    currentPathIndex++;
                    SetNextMoveTarget();
                }
                else
                {
                    // We've reached our destination
                    OnReachedDestination();
                }
            }
        }
    }

    private IEnumerator WaitForSystems()
    {
        // Wait for TileSystem
        yield return new WaitUntil(() => TileSystem.Instance != null && TileSystem.Instance.IsMapGenerated());

        // Get starting position
        currentTilePosition = TileSystem.Instance.WorldToTilePosition(transform.position);

        // Wait for Pathfinding
        yield return new WaitUntil(() => Pathfinding.Instance != null && Pathfinding.Instance.IsInitialized());

        // Now it's safe to start making decisions
        currentState = CreatureState.Idle;
        Debug.Log($"Creature {creatureName} initialized");
    }

    private void HandleIdleState()
    {
        // First priority: build jobs
        if (hasBuildJob)
        {
            // First check if we have all materials
            bool hasMaterials = true;
            foreach (var materialCost in buildStructure.materialCosts)
            {
                int count = 0;
                foreach (Item item in inventory)
                {
                    if ((item.tags & materialCost.Key) != 0)
                    {
                        count++;
                    }
                }

                if (count < materialCost.Value)
                {
                    hasMaterials = false;
                    break;
                }
            }

            if (hasMaterials)
            {
                // Go to build site
                targetTile = TileSystem.Instance.GetTile(buildJobLocation);
                MoveTo(buildJobLocation);
                currentState = CreatureState.MovingToBuildSite;
                return;
            }
            else
            {
                // Need to gather materials
                Vector2Int stockpilePos = StockpileSystem.Instance.FindItemTypeInStockpile(
                    buildStructure.requiredMaterials, currentTilePosition);

                if (stockpilePos.x >= 0)
                {
                    targetTile = TileSystem.Instance.GetTile(stockpilePos);
                    MoveTo(stockpilePos);
                    currentState = CreatureState.GatheringMaterials;
                    ShowActionIndicator("GET MATS");
                    return;
                }
            }
        }

        // Second priority: needs
        // Determine the next action based on needs
        DetermineNextAction();
    }

    private void HandleMovingState()
    {
        // Movement is handled in the Update method
        // Just check if the state is still valid
        if (currentState == CreatureState.MovingToFood)
        {
            // Check if target food tile is still valid
            if (targetTile != null && !targetTile.HasFood())
            {
                // Food is gone, try to find another one
                FindAndMoveTo(TileType.FoodSource, true);
            }
        }
    }

    private void HandleEatingState()
    {
        // Increase food need over time
        Need foodNeed = GetNeed(NeedType.Food);
        foodNeed.Fulfill(foodNeed.fulfillRate * Time.deltaTime);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f || foodNeed.value >= 99f)
        {
            // Done eating
            if (targetTile != null)
            {
                targetTile.ReleaseFromState(CreatureState.Eating, this);
                targetTile.SetAvailable();
            }

            currentState = CreatureState.Idle;
            HideActionIndicator();
        }
    }

    private void HandleSleepingState()
    {
        // Increase sleep need over time
        Need sleepNeed = GetNeed(NeedType.Sleep);
        sleepNeed.Fulfill(sleepNeed.fulfillRate * Time.deltaTime);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f || sleepNeed.value >= 99f)
        {
            // Done sleeping
            if (targetTile != null)
            {
                targetTile.ReleaseFromState(CreatureState.Sleeping, this);
                targetTile.SetAvailable();
            }

            currentState = CreatureState.Idle;
            HideActionIndicator();
        }
    }

    private void HandleDrinkingState()
    {
        // Increase drink need over time
        Need drinkNeed = GetNeed(NeedType.Drink);
        drinkNeed.Fulfill(drinkNeed.fulfillRate * Time.deltaTime);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f || drinkNeed.value >= 99f)
        {
            // Done drinking
            if (targetTile != null)
            {
                targetTile.ReleaseFromState(CreatureState.Drinking, this);
                targetTile.SetAvailable();
            }

            currentState = CreatureState.Idle;
            HideActionIndicator();
        }
    }

    private void HandleSocializingState()
    {
        // Increase socialization need over time
        Need socialNeed = GetNeed(NeedType.Socialization);
        socialNeed.Fulfill(socialNeed.fulfillRate * Time.deltaTime);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f || socialNeed.value >= 99f)
        {
            // Done socializing
            if (targetTile != null)
            {
                targetTile.ReleaseFromState(CreatureState.Socializing, this);
                targetTile.SetAvailable();
            }

            currentState = CreatureState.Idle;
            HideActionIndicator();
        }
    }

    private void HandleGatheringFoodState()
    {
        if (targetTile != null && targetTile.HasFood())
        {
            // Gather food
            float gathered = targetTile.GatherFood(5f * Time.deltaTime);
            foodCarried += gathered;

            // Check if we have gathered enough or the source is depleted
            if (foodCarried >= maxFoodCarry || !targetTile.HasFood())
            {
                // Release the tile
                targetTile.ReleaseFromState(CreatureState.GatheringFood, this);

                // Find stockpile to deposit food
                FindAndMoveTo(TileType.Stockpile);
                HideActionIndicator();
            }
        }
        else
        {
            // Release the tile
            if (targetTile != null)
            {
                targetTile.ReleaseFromState(CreatureState.GatheringFood, this);
            }

            // Source is depleted, look for another one
            FindAndMoveTo(TileType.FoodSource, true);
            HideActionIndicator();
        }
    }

    private void HandleBuildingState()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            // Done building
            if (targetTile != null)
            {
                // Place the structure
                targetTile.PlaceStructure(buildStructure);

                // Consume materials from inventory
                foreach (Item material in requiredMaterials)
                {
                    Item foundItem = inventory.Find(i => i.itemName == material.itemName);
                    if (foundItem != null)
                    {
                        inventory.Remove(foundItem);
                    }
                }

                // Release the tile
                targetTile.ReleaseFromState(CreatureState.Building, this);

                // Update pathfinding if walkability changed
                if (Pathfinding.Instance != null && !buildStructure.walkable)
                {
                    Pathfinding.Instance.UpdateNode(targetTile.position.x, targetTile.position.y, false);
                }
            }

            // Reset job
            hasBuildJob = false;
            requiredMaterials.Clear();
            buildStructure = null;

            currentState = CreatureState.Idle;
            HideActionIndicator();
        }
    }

    private void HandleGatheringMaterialsState()
    {
        if (StockpileSystem.Instance != null && targetTile != null)
        {
            if (hasBuildJob && buildStructure != null)
            {
                // Check what material we need
                foreach (var materialCost in buildStructure.materialCosts)
                {
                    // See if we already have this material
                    int countNeeded = materialCost.Value;
                    int countHave = 0;

                    foreach (Item item in inventory)
                    {
                        if ((item.tags & materialCost.Key) != 0)
                        {
                            countHave++;
                        }
                    }

                    if (countHave < countNeeded)
                    {
                        // Try to get this material from the stockpile
                        Item material = StockpileSystem.Instance.GetItemOfTypeAt(targetTile.position, materialCost.Key);

                        if (material != null)
                        {
                            // Add to inventory
                            if (inventory.Count < maxInventoryItems)
                            {
                                inventory.Add(material);
                                ShowActionIndicator("GOT ITEM");

                                // Wait a moment
                                stateTimer = 1f;

                                // Check if we have all needed materials
                                bool allMaterialsGathered = true;
                                foreach (var matCost in buildStructure.materialCosts)
                                {
                                    int count = 0;
                                    foreach (Item i in inventory)
                                    {
                                        if ((i.tags & matCost.Key) != 0)
                                        {
                                            count++;
                                        }
                                    }

                                    if (count < matCost.Value)
                                    {
                                        allMaterialsGathered = false;
                                        break;
                                    }
                                }

                                if (allMaterialsGathered)
                                {
                                    // Go to build site
                                    targetTile = TileSystem.Instance.GetTile(buildJobLocation);
                                    MoveTo(buildJobLocation);
                                    currentState = CreatureState.MovingToBuildSite;
                                    return;
                                }
                            }
                            else
                            {
                                // Inventory full, go build with what we have
                                targetTile = TileSystem.Instance.GetTile(buildJobLocation);
                                MoveTo(buildJobLocation);
                                currentState = CreatureState.MovingToBuildSite;
                                return;
                            }
                        }
                        else
                        {
                            // Material not found, try another stockpile
                            Vector2Int stockpilePos = StockpileSystem.Instance.FindItemTypeInStockpile(
                                materialCost.Key, currentTilePosition);

                            if (stockpilePos.x >= 0)
                            {
                                targetTile = TileSystem.Instance.GetTile(stockpilePos);
                                MoveTo(stockpilePos);
                                return;
                            }
                        }
                    }
                }

                // If we got here, either we have all materials or there are no more to find
                targetTile = TileSystem.Instance.GetTile(buildJobLocation);
                MoveTo(buildJobLocation);
                currentState = CreatureState.MovingToBuildSite;
            }
            else
            {
                // No build job, go idle
                currentState = CreatureState.Idle;
                HideActionIndicator();
            }
        }
        else
        {
            // No stockpile system or target tile, go idle
            currentState = CreatureState.Idle;
            HideActionIndicator();
        }
    }

    private void HandlePlacingItemState()
    {
        if (targetTile != null && inventory.Count > 0)
        {
            // The specific item we want to place should be the first in our inventory
            Item itemToPlace = inventory[0];

            if (targetTile.CanPlaceItem())
            {
                // Place the item
                if (targetTile.PlaceItem(itemToPlace))
                {
                    // Remove from inventory
                    inventory.RemoveAt(0);

                    ShowActionIndicator("PLACED");
                    stateTimer = 1f;
                }
                else
                {
                    // Can't place item here
                    ShowActionIndicator("INVALID");
                    stateTimer = 1f;
                }
            }
            else
            {
                // Can't place item here
                ShowActionIndicator("INVALID");
                stateTimer = 1f;
            }

            // After a short delay, go back to idle
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                currentState = CreatureState.Idle;
                HideActionIndicator();
            }
        }
        else
        {
            // No target tile or empty inventory
            currentState = CreatureState.Idle;
            HideActionIndicator();
        }
    }

    private void OnReachedDestination()
    {
        // Handle what happens when we reach our destination based on current state
        switch (currentState)
        {
            case CreatureState.MovingToFood:
                if (targetTile != null && targetTile.HasFood())
                {
                    // Check if tile can be occupied
                    if (targetTile.CanOccupyForState(CreatureState.GatheringFood, this) &&
                        targetTile.OccupyForState(CreatureState.GatheringFood, this))
                    {
                        // Start gathering or eating
                        stateTimer = Random.Range(3f, 6f);
                        currentState = CreatureState.GatheringFood;

                        // Show action indicator
                        ShowActionIndicator("GATHER");
                    }
                    else
                    {
                        // Tile is occupied, find another one
                        FindAndMoveTo(TileType.FoodSource, true);
                    }
                }
                else
                {
                    currentState = CreatureState.Idle;
                    HideActionIndicator();
                }
                break;

            case CreatureState.MovingToStockpile:
                if (foodCarried > 0)
                {
                    // Deposit food
                    foodCarried = 0;

                    // Maybe eat some food if hungry
                    Need foodNeed = GetNeed(NeedType.Food);
                    if (foodNeed.value < 50f)
                    {
                        // Check if tile can be occupied
                        if (targetTile.CanOccupyForState(CreatureState.Eating, this) &&
                            targetTile.OccupyForState(CreatureState.Eating, this))
                        {
                            stateTimer = Random.Range(3f, 6f);
                            currentState = CreatureState.Eating;
                            ShowActionIndicator("EAT");
                        }
                        else
                        {
                            // Find another stockpile
                            FindAndMoveTo(TileType.Stockpile);
                        }
                    }
                    else
                    {
                        currentState = CreatureState.Idle;
                        HideActionIndicator();
                    }
                }
                else
                {
                    // Check if tile can be occupied
                    if (targetTile.CanOccupyForState(CreatureState.Eating, this) &&
                        targetTile.OccupyForState(CreatureState.Eating, this))
                    {
                        // Eat from stockpile
                        stateTimer = Random.Range(3f, 6f);
                        currentState = CreatureState.Eating;
                        ShowActionIndicator("EAT");
                    }
                    else
                    {
                        // Find another stockpile
                        FindAndMoveTo(TileType.Stockpile);
                    }
                }
                break;

            case CreatureState.MovingToSleep:
                // Check if tile can be occupied
                if (targetTile.CanOccupyForState(CreatureState.Sleeping, this) &&
                    targetTile.OccupyForState(CreatureState.Sleeping, this))
                {
                    // Start sleeping
                    stateTimer = Random.Range(5f, 10f);
                    currentState = CreatureState.Sleeping;
                    ShowActionIndicator("ZZZ");
                }
                else
                {
                    // Find another place to sleep
                    List<Tile> groundTiles = TileSystem.Instance.GetAllTilesOfType(TileType.Ground);
                    if (groundTiles.Count > 0)
                    {
                        targetTile = groundTiles[Random.Range(0, groundTiles.Count)];
                        MoveTo(targetTile.position);
                        currentState = CreatureState.MovingToSleep;
                    }
                    else
                    {
                        currentState = CreatureState.Idle;
                    }
                }
                break;

            case CreatureState.MovingToDrink:
                // Check if tile can be occupied
                if (targetTile.CanOccupyForState(CreatureState.Drinking, this) &&
                    targetTile.OccupyForState(CreatureState.Drinking, this))
                {
                    // Start drinking
                    stateTimer = Random.Range(2f, 4f);
                    currentState = CreatureState.Drinking;
                    ShowActionIndicator("DRINK");
                }
                else
                {
                    // Find another place to drink
                    List<Tile> drinkTiles = TileSystem.Instance.GetAllTilesOfType(TileType.Ground);
                    if (drinkTiles.Count > 0)
                    {
                        targetTile = drinkTiles[Random.Range(0, drinkTiles.Count)];
                        MoveTo(targetTile.position);
                        currentState = CreatureState.MovingToDrink;
                    }
                    else
                    {
                        currentState = CreatureState.Idle;
                    }
                }
                break;

            case CreatureState.MovingToSocial:
                // Check if tile can be occupied
                if (targetTile.CanOccupyForState(CreatureState.Socializing, this) &&
                    targetTile.OccupyForState(CreatureState.Socializing, this))
                {
                    // Start socializing
                    stateTimer = Random.Range(4f, 8f);
                    currentState = CreatureState.Socializing;
                    ShowActionIndicator("CHAT");
                }
                else
                {
                    // Find another social area
                    Tile socialTile = TileSystem.Instance.GetClosestTileOfType(currentTilePosition, TileType.SocialArea);
                    if (socialTile != null)
                    {
                        targetTile = socialTile;
                        MoveTo(targetTile.position);
                        currentState = CreatureState.MovingToSocial;
                    }
                    else
                    {
                        currentState = CreatureState.Idle;
                    }
                }
                break;

            case CreatureState.MovingToBuildSite:
                if (targetTile != null && targetTile.CanPlaceStructure())
                {
                    // Check if we have all the required materials
                    bool hasMaterials = true;
                    foreach (Item item in requiredMaterials)
                    {
                        if (!inventory.Exists(i => i.itemName == item.itemName))
                        {
                            hasMaterials = false;
                            break;
                        }
                    }

                    if (hasMaterials)
                    {
                        // Start building
                        stateTimer = Random.Range(5f, 10f);
                        currentState = CreatureState.Building;
                        ShowActionIndicator("BUILD");

                        // Mark tile as occupied
                        targetTile.OccupyForState(CreatureState.Building, this);
                    }
                    else
                    {
                        // Need to gather materials
                        currentState = CreatureState.GatheringMaterials;
                        ShowActionIndicator("GET MATS");

                        // Find stockpile with needed materials
                        Vector2Int stockpilePos = StockpileSystem.Instance.FindItemTypeInStockpile(
                            buildStructure.requiredMaterials, currentTilePosition);

                        if (stockpilePos.x >= 0)
                        {
                            targetTile = TileSystem.Instance.GetTile(stockpilePos);
                            MoveTo(stockpilePos);
                        }
                        else
                        {
                            // No materials found, cancel job
                            hasBuildJob = false;
                            currentState = CreatureState.Idle;
                            HideActionIndicator();
                        }
                    }
                }
                else
                {
                    // Invalid build site
                    hasBuildJob = false;
                    currentState = CreatureState.Idle;
                    HideActionIndicator();
                }
                break;

            case CreatureState.MovingToStructure:
                if (targetTile != null && targetTile.type == TileType.Structure)
                {
                    // Check if tile can be occupied for the specific need
                    NeedType primaryNeed = GetLowestNeed().type;

                    if (targetTile.CanBeUsedForNeed(primaryNeed, this) &&
                        targetTile.CanOccupyForState(GetStateForNeed(primaryNeed), this) &&
                        targetTile.OccupyForState(GetStateForNeed(primaryNeed), this))
                    {
                        // Use the structure
                        stateTimer = Random.Range(3f, 6f);
                        currentState = GetStateForNeed(primaryNeed);
                        targetTile.SetInUse(this);

                        // Show action indicator
                        ShowActionIndicator(GetActionTextForNeed(primaryNeed));
                    }
                    else
                    {
                        // Structure is occupied, find another one
                        FindStructureForNeed(primaryNeed);
                    }
                }
                else
                {
                    currentState = CreatureState.Idle;
                    HideActionIndicator();
                }
                break;
        }
    }

    private Need GetLowestNeed()
    {
        Need lowestNeed = needs[0];
        foreach (Need need in needs)
        {
            if (need.value < lowestNeed.value)
            {
                lowestNeed = need;
            }
        }
        return lowestNeed;
    }

    private CreatureState GetStateForNeed(NeedType needType)
    {
        switch (needType)
        {
            case NeedType.Food:
                return CreatureState.Eating;
            case NeedType.Sleep:
                return CreatureState.Sleeping;
            case NeedType.Drink:
                return CreatureState.Drinking;
            case NeedType.Socialization:
                return CreatureState.Socializing;
            default:
                return CreatureState.Idle;
        }
    }

    private string GetActionTextForNeed(NeedType needType)
    {
        switch (needType)
        {
            case NeedType.Food:
                return "EAT";
            case NeedType.Sleep:
                return "ZZZ";
            case NeedType.Drink:
                return "DRINK";
            case NeedType.Socialization:
                return "CHAT";
            default:
                return "";
        }
    }

    private void FindStructureForNeed(NeedType needType)
    {
        // Find structures that can satisfy this need
        List<Tile> structureTiles = TileSystem.Instance.GetAllTilesOfType(TileType.Structure);
        List<Tile> suitableTiles = new List<Tile>();

        foreach (Tile tile in structureTiles)
        {
            if (tile.CanBeUsedForNeed(needType, this))
            {
                suitableTiles.Add(tile);
            }
        }

        if (suitableTiles.Count > 0)
        {
            // Find the closest suitable tile
            Tile closestTile = suitableTiles[0];
            float closestDistance = Vector2Int.Distance(currentTilePosition, closestTile.position);

            foreach (Tile tile in suitableTiles)
            {
                float distance = Vector2Int.Distance(currentTilePosition, tile.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTile = tile;
                }
            }

            targetTile = closestTile;
            MoveTo(targetTile.position);
            currentState = CreatureState.MovingToStructure;
        }
        else
        {
            // Fall back to default behavior
            switch (needType)
            {
                case NeedType.Food:
                    FindAndMoveTo(TileType.FoodSource, true);
                    break;
                case NeedType.Sleep:
                    FindAndMoveTo(TileType.Ground);
                    currentState = CreatureState.MovingToSleep;
                    break;
                case NeedType.Drink:
                    FindAndMoveTo(TileType.Ground);
                    currentState = CreatureState.MovingToDrink;
                    break;
                case NeedType.Socialization:
                    FindAndMoveTo(TileType.SocialArea);
                    break;
            }
        }
    }

    private void DetermineNextAction()
    {
        // Check if we're carrying food and should deposit it
        if (foodCarried > 0)
        {
            // Find a non-full stockpile
            Vector2Int stockpilePos = new Vector2Int(-1, -1);

            if (StockpileSystem.Instance != null)
            {
                // Use the stockpile system to find a place with room
                Item foodItem = new Item("Food", ItemTags.Food, NeedTags.Food);
                stockpilePos = StockpileSystem.Instance.FindStockpileForItem(foodItem, currentTilePosition);
            }
            else
            {
                // Fall back to basic stockpile finding
                List<Tile> stockpiles = TileSystem.Instance.GetAllTilesOfType(TileType.Stockpile);

                if (stockpiles.Count > 0)
                {
                    // Find closest stockpile
                    Tile closest = null;
                    float closestDist = float.MaxValue;

                    foreach (Tile stockpile in stockpiles)
                    {
                        float dist = Vector2Int.Distance(currentTilePosition, stockpile.position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closest = stockpile;
                        }
                    }

                    if (closest != null)
                    {
                        stockpilePos = closest.position;
                    }
                }
            }

            if (stockpilePos.x >= 0)
            {
                targetTile = TileSystem.Instance.GetTile(stockpilePos);
                MoveTo(stockpilePos);
                currentState = CreatureState.MovingToStockpile;
                return;
            }
        }

        // Find the need that requires attention
        Need priorityNeed = null;

        if (behavior == BehaviorType.PrioritizeLowest)
        {
            // Find the lowest need
            priorityNeed = needs.OrderBy(n => n.value).FirstOrDefault();
        }
        else if (behavior == BehaviorType.PrioritizeFastest)
        {
            // Check if any need is critical
            Need criticalNeed = needs.FirstOrDefault(n => n.IsCritical());

            if (criticalNeed != null)
            {
                priorityNeed = criticalNeed;
            }
            else
            {
                // Find the need that can be fulfilled fastest
                priorityNeed = needs.OrderBy(n => n.GetFulfillmentTime()).FirstOrDefault();
            }
        }

        if (priorityNeed != null)
        {
            // First try to find a structure that satisfies this need
            FindStructureForNeed(priorityNeed.type);

            // If we're still in the Idle state after that attempt, it means no suitable structure was found,
            // so fall back to old behavior based on tile types
            if (currentState == CreatureState.Idle)
            {
                // Take action based on the priority need
                switch (priorityNeed.type)
                {
                    case NeedType.Food:
                        // Check if there's a stockpile with food
                        Vector2Int foodStockpilePos = new Vector2Int(-1, -1);
                        if (StockpileSystem.Instance != null)
                        {
                            foodStockpilePos = StockpileSystem.Instance.FindItemTypeInStockpile(ItemTags.Food, currentTilePosition);
                        }

                        if (foodStockpilePos.x >= 0 && Random.value < 0.7f) // 70% chance to prefer stockpile if it exists
                        {
                            targetTile = TileSystem.Instance.GetTile(foodStockpilePos);
                            MoveTo(foodStockpilePos);
                            currentState = CreatureState.MovingToStockpile;
                        }
                        else
                        {
                            // Go directly to food source
                            FindAndMoveTo(TileType.FoodSource, true);
                        }
                        break;

                    case NeedType.Sleep:
                        // Find a quiet place to sleep (ground tile)
                        List<Tile> groundTiles = TileSystem.Instance.GetAllTilesOfType(TileType.Ground);
                        if (groundTiles.Count > 0)
                        {
                            // Find an unoccupied tile
                            Tile sleepTile = null;
                            for (int i = 0; i < Mathf.Min(10, groundTiles.Count); i++)
                            {
                                Tile tile = groundTiles[Random.Range(0, groundTiles.Count)];
                                if (tile.CanOccupyForState(CreatureState.Sleeping, this))
                                {
                                    sleepTile = tile;
                                    break;
                                }
                            }

                            if (sleepTile != null)
                            {
                                targetTile = sleepTile;
                                MoveTo(sleepTile.position);
                                currentState = CreatureState.MovingToSleep;
                            }
                            else
                            {
                                // Fall back to random tile if all checked are occupied
                                targetTile = groundTiles[Random.Range(0, groundTiles.Count)];
                                MoveTo(targetTile.position);
                                currentState = CreatureState.MovingToSleep;
                            }
                        }
                        break;

                    case NeedType.Drink:
                        // For simplicity, just find a ground tile for drinking (could be expanded to water tiles)
                        List<Tile> drinkTiles = TileSystem.Instance.GetAllTilesOfType(TileType.Ground);
                        if (drinkTiles.Count > 0)
                        {
                            // Find an unoccupied tile
                            Tile drinkTile = null;
                            for (int i = 0; i < Mathf.Min(10, drinkTiles.Count); i++)
                            {
                                Tile tile = drinkTiles[Random.Range(0, drinkTiles.Count)];
                                if (tile.CanOccupyForState(CreatureState.Drinking, this))
                                {
                                    drinkTile = tile;
                                    break;
                                }
                            }

                            if (drinkTile != null)
                            {
                                targetTile = drinkTile;
                                MoveTo(drinkTile.position);
                                currentState = CreatureState.MovingToDrink;
                            }
                            else
                            {
                                // Fall back to random tile if all checked are occupied
                                targetTile = drinkTiles[Random.Range(0, drinkTiles.Count)];
                                MoveTo(targetTile.position);
                                currentState = CreatureState.MovingToDrink;
                            }
                        }
                        break;

                    case NeedType.Socialization:
                        // Find a social area or another creature
                        Tile socialTile = TileSystem.Instance.GetClosestTileOfType(currentTilePosition, TileType.SocialArea);
                        if (socialTile != null)
                        {
                            if (socialTile.CanOccupyForState(CreatureState.Socializing, this))
                            {
                                targetTile = socialTile;
                                MoveTo(targetTile.position);
                                currentState = CreatureState.MovingToSocial;
                            }
                            else
                            {
                                // Find another social area
                                List<Tile> socialAreas = TileSystem.Instance.GetAllTilesOfType(TileType.SocialArea);
                                if (socialAreas.Count > 0)
                                {
                                    // Find an unoccupied one
                                    Tile unoccupiedSocialTile = socialAreas.Find(t => t.CanOccupyForState(CreatureState.Socializing, this));
                                    if (unoccupiedSocialTile != null)
                                    {
                                        targetTile = unoccupiedSocialTile;
                                        MoveTo(targetTile.position);
                                        currentState = CreatureState.MovingToSocial;
                                    }
                                    else
                                    {
                                        // All occupied, try anyway
                                        targetTile = socialAreas[Random.Range(0, socialAreas.Count)];
                                        MoveTo(targetTile.position);
                                        currentState = CreatureState.MovingToSocial;
                                    }
                                }
                                else
                                {
                                    // No social area, look for a nearby ground tile
                                    List<Tile> socialGroundTiles = TileSystem.Instance.GetAllTilesOfType(TileType.Ground);
                                    if (socialGroundTiles.Count > 0)
                                    {
                                        targetTile = socialGroundTiles[Random.Range(0, socialGroundTiles.Count)];
                                        MoveTo(targetTile.position);
                                        currentState = CreatureState.MovingToSocial;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // No social area, look for a nearby ground tile
                            List<Tile> socialGroundTiles = TileSystem.Instance.GetAllTilesOfType(TileType.Ground);
                            if (socialGroundTiles.Count > 0)
                            {
                                targetTile = socialGroundTiles[Random.Range(0, socialGroundTiles.Count)];
                                MoveTo(targetTile.position);
                                currentState = CreatureState.MovingToSocial;
                            }
                        }
                        break;
                }
            }
        }
        else
        {
            // All needs are satisfied, consider gathering food for stockpile
            if (Random.value < 0.3f) // 30% chance to gather food when idle
            {
                FindAndMoveTo(TileType.FoodSource, true);
                currentState = CreatureState.MovingToFood;
            }
        }
    }

    private void FindAndMoveTo(TileType tileType, bool mustHaveFood = false)
    {
        // Check if TileSystem instance exists
        if (TileSystem.Instance == null)
        {
            Debug.LogError("TileSystem instance is null! Make sure TileSystem is initialized before creatures try to find tiles.");
            currentState = CreatureState.Idle;
            return;
        }

        targetTile = TileSystem.Instance.GetClosestTileOfType(currentTilePosition, tileType, mustHaveFood);

        if (targetTile != null)
        {
            MoveTo(targetTile.position);

            // Set appropriate state
            switch (tileType)
            {
                case TileType.FoodSource:
                    currentState = CreatureState.MovingToFood;
                    break;
                case TileType.Stockpile:
                    currentState = CreatureState.MovingToStockpile;
                    break;
                case TileType.SocialArea:
                    currentState = CreatureState.MovingToSocial;
                    break;
            }
        }
        else
        {
            // Couldn't find target, stay idle
            currentState = CreatureState.Idle;
        }
    }

    private void MoveTo(Vector2Int targetPosition)
    {
        // Check if Pathfinding is ready
        if (Pathfinding.Instance == null || !Pathfinding.Instance.IsInitialized())
        {
            Debug.LogWarning($"Creature {creatureName} attempted to move before pathfinding was initialized");
            isMoving = false;
            currentState = CreatureState.Idle;
            return;
        }

        // Get path from pathfinding
        currentPath = Pathfinding.Instance.FindPath(currentTilePosition, targetPosition);

        if (currentPath != null && currentPath.Count > 0)
        {
            currentPathIndex = 0;
            targetTilePosition = targetPosition;
            SetNextMoveTarget();
        }
        else
        {
            // No path found, or path is empty
            if (currentPath == null)
            {
                Debug.LogWarning($"No path found for creature {creatureName} from {currentTilePosition} to {targetPosition}");
            }
            isMoving = false;
            currentState = CreatureState.Idle;
        }
    }

    private void SetNextMoveTarget()
    {
        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            Vector2Int nextTilePosition = currentPath[currentPathIndex];

            // Preserve Z position when setting move target
            Vector3 worldPos = TileSystem.Instance.TileToWorldPosition(nextTilePosition);
            moveTarget = new Vector3(worldPos.x, worldPos.y, transform.position.z);

            currentTilePosition = nextTilePosition;
            isMoving = true;
        }
    }

    private void ShowActionIndicator(string text)
    {
        if (actionIndicator != null)
        {
            actionIndicator.SetText(text);
            actionIndicator.SetActive(true);
        }
    }

    private void HideActionIndicator()
    {
        if (actionIndicator != null)
        {
            actionIndicator.SetActive(false);
        }
    }

    public void AssignBuildJob(Vector2Int location, Structure structure)
    {
        hasBuildJob = true;
        buildJobLocation = location;
        buildStructure = structure;

        // Find required materials
        requiredMaterials.Clear();
        foreach (var materialCost in structure.materialCosts)
        {
            for (int i = 0; i < materialCost.Value; i++)
            {
                Item material = new Item($"{materialCost.Key} Material", materialCost.Key, NeedTags.None);
                requiredMaterials.Add(material);
            }
        }

        // Start gathering materials
        Vector2Int stockpilePos = StockpileSystem.Instance.FindItemTypeInStockpile(
            structure.requiredMaterials, currentTilePosition);

        if (stockpilePos.x >= 0)
        {
            targetTile = TileSystem.Instance.GetTile(stockpilePos);
            MoveTo(stockpilePos);
            currentState = CreatureState.MovingToBuildSite;
            ShowActionIndicator("BUILD JOB");
        }
        else
        {
            Debug.LogWarning("No materials found for build job");
            hasBuildJob = false;
        }
    }

    public void AssignItemPlacementJob(Vector2Int location, Item item)
    {
        // First check if we have the item in inventory
        Item foundItem = inventory.Find(i => i.itemName == item.itemName);

        if (foundItem != null)
        {
            // We have the item, go place it
            targetTile = TileSystem.Instance.GetTile(location);
            MoveTo(location);
            currentState = CreatureState.MovingToStructure; // or could be a special MovingToPlace state
            ShowActionIndicator("PLACE");
        }
        else
        {
            // Need to find the item in stockpile
            Vector2Int stockpilePos = StockpileSystem.Instance.FindItemInStockpile(item.itemName, currentTilePosition);

            if (stockpilePos.x >= 0)
            {
                // First go get the item
                targetTile = TileSystem.Instance.GetTile(stockpilePos);
                MoveTo(stockpilePos);

                // Set up next actions after reaching stockpile
                Vector2Int finalDestination = location;

                // Create delegate to execute after getting item
                System.Action afterGettingItem = () => {
                    targetTile = TileSystem.Instance.GetTile(finalDestination);
                    MoveTo(finalDestination);
                    currentState = CreatureState.MovingToStructure;
                };

                // Store this in a field or use a state pattern for more complex sequences
                // For now, simplified approach
                currentState = CreatureState.GatheringMaterials;
                ShowActionIndicator("GET ITEM");
            }
            else
            {
                Debug.LogWarning("Item not found in any stockpile");
            }
        }
    }

    private Need GetNeed(NeedType type)
    {
        return needs.Find(n => n.type == type);
    }

    private void Die()
    {
        Debug.Log($"{creatureName} has died!");
        Destroy(gameObject);
    }

    // For debugging
    private void OnDrawGizmos()
    {
        if (currentPath != null && currentPath.Count > 0)
        {
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 start = TileSystem.Instance.TileToWorldPosition(currentPath[i]) + new Vector3(0.5f, 0.5f, 0);
                Vector3 end = TileSystem.Instance.TileToWorldPosition(currentPath[i + 1]) + new Vector3(0.5f, 0.5f, 0);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(start, end);
            }
        }
    }
}