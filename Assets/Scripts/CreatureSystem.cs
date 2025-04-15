using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Definition of a creature type
[CreateAssetMenu(fileName = "NewCreatureType", menuName = "Colony/Creatures/Creature Definition")]
public class CreatureDefinition : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique identifier for this creature type")]
    public string creatureID;

    [Tooltip("Display name for this creature type")]
    public string displayName;

    [Tooltip("Description of this creature type")]
    [TextArea(3, 6)]
    public string description;

    [Header("Visual")]
    [Tooltip("Sprites for this creature (idle, walk, work, etc.)")]
    public Sprite[] sprites;

    [Tooltip("Animation controller (optional)")]
    public RuntimeAnimatorController animatorController;

    [Header("Properties")]
    [Tooltip("Base movement speed")]
    public float moveSpeed = 3f;

    [Tooltip("Base health points")]
    public float maxHealth = 100f;

    [Tooltip("Base stamina points")]
    public float maxStamina = 100f;

    [Tooltip("Stamina regeneration rate per second")]
    public float staminaRegenRate = 5f;

    [Header("Needs")]
    [Tooltip("Needs this creature type has")]
    public List<NeedDefinition> needs = new List<NeedDefinition>();

    [Tooltip("Initial value range for needs (min)")]
    [Range(0, 100)]
    public float initialNeedMin = 50f;

    [Tooltip("Initial value range for needs (max)")]
    [Range(0, 100)]
    public float initialNeedMax = 100f;

    [Header("Abilities")]
    [Tooltip("Abilities this creature type has")]
    public List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    [Header("Traits")]
    [Tooltip("Tags describing this creature type")]
    public List<CreatureTag> tags = new List<CreatureTag>();

    [Header("Inventory")]
    [Tooltip("Maximum weight the creature can carry")]
    public float carryCapacity = 50f;

    [Tooltip("Maximum number of distinct item stacks")]
    public int inventorySlots = 5;
}

// Actual creature instance in the game
public class Creature : MonoBehaviour
{
    [Header("Definition")]
    public CreatureDefinition definition;

    [Header("Status")]
    public string creatureName;
    public float currentHealth;
    public float currentStamina;

    [Header("State")]
    public CreatureState currentState;
    public float stateTimer;
    public Vector2Int targetPosition;
    public Item targetItem;
    public Creature targetCreature;
    public float actionProgress;

    [Header("Components")]
    public SpriteRenderer spriteRenderer;
    public Animator animator;

    [Header("Pathfinding")]
    public List<Vector2Int> currentPath;
    public int currentPathIndex;
    public bool isMoving;

    // Runtime tracking of needs
    private Dictionary<string, Need> needsDict = new Dictionary<string, Need>();

    // Runtime tracking of abilities
    private Dictionary<string, Ability> abilitiesDict = new Dictionary<string, Ability>();

    // Inventory management
    private List<Item> inventory = new List<Item>();
    private float currentCarryWeight = 0f;

    // Brain/AI reference
    private CreatureBrain brain;

    // Action indicator for UI feedback
    private ActionIndicator actionIndicator;

    private void Awake()
    {
        // Find required components
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        // Create brain
        brain = gameObject.AddComponent<CreatureBrain>();

        // Create action indicator
        CreateActionIndicator();
    }

    private void Start()
    {
        if (definition == null)
        {
            Debug.LogError("Creature has no definition assigned!");
            return;
        }

        // Initialize based on definition
        InitializeFromDefinition();

        // Start in idle state
        SetState(CreatureState.Idle);
    }

    private void Update()
    {
        // Update needs
        UpdateNeeds(Time.deltaTime);

        // Update abilities
        UpdateAbilities(Time.deltaTime);

        // Process current state
        ProcessState();

        // Regenerate stamina
        RegenerateStamina(Time.deltaTime);

        // Let brain make decisions if in idle state
        if (currentState == CreatureState.Idle)
        {
            brain.MakeDecision();
        }
    }

    // Initialize creature based on definition
    private void InitializeFromDefinition()
    {
        // Basic setup
        if (string.IsNullOrEmpty(creatureName))
        {
            creatureName = definition.displayName;
        }

        currentHealth = definition.maxHealth;
        currentStamina = definition.maxStamina;

        // Initialize needs
        foreach (var needDef in definition.needs)
        {
            float initialValue = Random.Range(definition.initialNeedMin, definition.initialNeedMax);
            Need need = new Need(needDef, initialValue);
            needsDict[needDef.needID] = need;
        }

        // Initialize abilities
        foreach (var abilityDef in definition.abilities)
        {
            Ability ability = new Ability(abilityDef);
            abilitiesDict[abilityDef.abilityID] = ability;
        }

        // Set up sprite
        if (spriteRenderer != null && definition.sprites.Length > 0)
        {
            spriteRenderer.sprite = definition.sprites[0];
        }

        // Set up animator
        if (animator != null && definition.animatorController != null)
        {
            animator.runtimeAnimatorController = definition.animatorController;
        }
    }

    // Create action indicator
    private void CreateActionIndicator()
    {
        GameObject indicatorObj = new GameObject("ActionIndicator");
        indicatorObj.transform.SetParent(transform);
        indicatorObj.transform.localPosition = new Vector3(0, 1f, 0);

        actionIndicator = indicatorObj.AddComponent<ActionIndicator>();
        actionIndicator.Initialize(transform);
        actionIndicator.SetActive(false);
    }

    // Update all needs
    private void UpdateNeeds(float deltaTime)
    {
        bool needsAttention = false;

        foreach (var need in needsDict.Values)
        {
            need.Update(deltaTime);

            // Check if any need is fatal
            if (need.IsFatal())
            {
                Die(need.definition.displayName + " deprivation");
                return;
            }

            // Check if any need is critical and requires attention
            if (need.IsCritical() && currentState == CreatureState.Idle)
            {
                needsAttention = true;
            }
        }

        // If a need is critical and we're idle, address it
        if (needsAttention)
        {
            brain.AddressMostUrgentNeed();
        }
    }

    // Update all abilities
    private void UpdateAbilities(float deltaTime)
    {
        foreach (var ability in abilitiesDict.Values)
        {
            ability.Update(deltaTime);
        }
    }

    // Regenerate stamina over time
    private void RegenerateStamina(float deltaTime)
    {
        // Don't regenerate while performing strenuous actions
        if (currentState == CreatureState.Working ||
            currentState == CreatureState.Building ||
            currentState == CreatureState.Fighting)
        {
            return;
        }

        currentStamina = Mathf.Min(definition.maxStamina, currentStamina + definition.staminaRegenRate * deltaTime);
    }

    // Process current state and transition if needed
    private void ProcessState()
    {
        switch (currentState)
        {
            case CreatureState.Idle:
                ProcessIdleState();
                break;
            case CreatureState.Moving:
                ProcessMovingState();
                break;
            case CreatureState.Consuming:
                ProcessConsumingState();
                break;
            case CreatureState.Working:
                ProcessWorkingState();
                break;
            case CreatureState.Building:
                ProcessBuildingState();
                break;
            case CreatureState.Gathering:
                ProcessGatheringState();
                break;
            case CreatureState.Sleeping:
                ProcessSleepingState();
                break;
            case CreatureState.Socializing:
                ProcessSocializingState();
                break;
            case CreatureState.Hauling:
                ProcessHaulingState();
                break;
            case CreatureState.Fighting:
                ProcessFightingState();
                break;
            case CreatureState.Fleeing:
                ProcessFleeingState();
                break;
            case CreatureState.UsingItem:
                ProcessUsingItemState();
                break;
            case CreatureState.UsingStructure:
                ProcessUsingStructureState();
                break;
        }
    }

    // State processing methods
    private void ProcessIdleState()
    {
        // If idle, let brain make decisions
        // Actual AI logic is in CreatureBrain
    }

    private void ProcessMovingState()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            SetState(CreatureState.Idle);
            return;
        }

        if (isMoving)
        {
            // Continue movement - handled in fixed update
            return;
        }

        // Check if we've reached the target position
        if (currentPathIndex >= currentPath.Count)
        {
            // Reached destination
            OnReachedDestination();
            return;
        }

        // Move to next position in path
        Vector2Int nextPos = currentPath[currentPathIndex];
        Vector3 worldPos = TileSystem.Instance.GetWorldPosition(nextPos);

        // Start moving
        StartCoroutine(MoveToPosition(worldPos));
    }

    private void ProcessConsumingState()
    {
        // Using/consuming an item
        if (targetItem == null)
        {
            SetState(CreatureState.Idle);
            return;
        }

        stateTimer -= Time.deltaTime;
        actionProgress = 1 - (stateTimer / GetConsumableUseTime(targetItem));

        if (stateTimer <= 0)
        {
            // Finish consuming
            targetItem.Use(this);
            SetState(CreatureState.Idle);
        }
    }

    private void ProcessWorkingState()
    {
        stateTimer -= Time.deltaTime;
        actionProgress = 1 - (stateTimer / GetWorkTime());

        if (stateTimer <= 0)
        {
            // Work completed, process results
            CompleteWork();
            SetState(CreatureState.Idle);
        }
    }

    private void ProcessBuildingState()
    {
        if (targetItem == null || !(targetItem is StructureItem))
        {
            SetState(CreatureState.Idle);
            return;
        }

        stateTimer -= Time.deltaTime;
        actionProgress = 1 - (stateTimer / GetBuildTime());

        if (stateTimer <= 0)
        {
            // Building completed
            CompleteBuild();
            SetState(CreatureState.Idle);
        }
    }

    private void ProcessGatheringState()
    {
        // Implementation depends on resource system
        stateTimer -= Time.deltaTime;
        actionProgress = 1 - (stateTimer / GetGatherTime());

        if (stateTimer <= 0)
        {
            // Gathering completed
            CompleteGathering();
            SetState(CreatureState.Idle);
        }
    }

    private void ProcessSleepingState()
    {
        // Process sleeping (fulfill sleep need)
        Need sleepNeed = null;
        foreach (var need in needsDict.Values)
        {
            if (need.definition.needID.Contains("sleep"))
            {
                sleepNeed = need;
                break;
            }
        }

        if (sleepNeed != null)
        {
            sleepNeed.Fulfill(sleepNeed.definition.fulfillmentRate * Time.deltaTime);

            // Also regenerate stamina faster while sleeping
            currentStamina = Mathf.Min(definition.maxStamina,
                currentStamina + definition.staminaRegenRate * 3 * Time.deltaTime);

            // Wake up if need is fully satisfied or critical interrupt
            if (sleepNeed.currentValue >= 95f || brain.HasCriticalInterrupt())
            {
                SetState(CreatureState.Idle);
            }
        }
        else
        {
            // No sleep need found, exit state
            SetState(CreatureState.Idle);
        }
    }

    private void ProcessSocializingState()
    {
        if (targetCreature == null)
        {
            SetState(CreatureState.Idle);
            return;
        }

        // Process socializing (fulfill social need)
        Need socialNeed = null;
        foreach (var need in needsDict.Values)
        {
            if (need.definition.needID.Contains("social"))
            {
                socialNeed = need;
                break;
            }
        }

        if (socialNeed != null)
        {
            socialNeed.Fulfill(socialNeed.definition.fulfillmentRate * Time.deltaTime);

            // Exit if need is satisfied or critical interrupt
            if (socialNeed.currentValue >= 95f || brain.HasCriticalInterrupt())
            {
                SetState(CreatureState.Idle);
            }
        }
        else
        {
            // No social need found, exit state
            SetState(CreatureState.Idle);
        }
    }

    private void ProcessHaulingState()
    {
        // If we've reached the destination with hauled item
        if (!isMoving && currentPathIndex >= currentPath.Count)
        {
            // Place the item
            if (targetItem != null)
            {
                PlaceItem(targetItem, targetPosition);
            }

            SetState(CreatureState.Idle);
        }
    }

    private void ProcessFightingState()
    {
        // Combat system implementation
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            // Attack completed
            if (targetCreature != null)
            {
                // Process attack
                PerformAttack();

                // Reset timer for next attack
                stateTimer = GetAttackTime();
            }
            else
            {
                SetState(CreatureState.Idle);
            }
        }
    }

    private void ProcessFleeingState()
    {
        // If we've reached safety
        if (!isMoving && currentPathIndex >= currentPath.Count)
        {
            SetState(CreatureState.Idle);
        }
    }

    private void ProcessUsingItemState()
    {
        if (targetItem == null)
        {
            SetState(CreatureState.Idle);
            return;
        }

        stateTimer -= Time.deltaTime;
        actionProgress = 1 - (stateTimer / GetItemUseTime(targetItem));

        if (stateTimer <= 0)
        {
            // Finish using item
            UseItem(targetItem);
            SetState(CreatureState.Idle);
        }
    }

    private void ProcessUsingStructureState()
    {
        if (targetItem == null || !(targetItem is StructureItem))
        {
            SetState(CreatureState.Idle);
            return;
        }

        // Process using a structure to fulfill a need
        string satisfiedNeedID = null;
        float effectiveness = 0f;

        foreach (var needEntry in needsDict)
        {
            if (targetItem.CanSatisfyNeed(needEntry.Key, out float itemEffectiveness))
            {
                satisfiedNeedID = needEntry.Key;
                effectiveness = itemEffectiveness;
                break;
            }
        }

        if (satisfiedNeedID != null)
        {
            Need need = needsDict[satisfiedNeedID];
            need.Fulfill(effectiveness * need.definition.fulfillmentRate * Time.deltaTime);

            // Exit if need is satisfied or critical interrupt
            if (need.currentValue >= 95f || brain.HasCriticalInterrupt())
            {
                // Release the structure
                if (targetItem is FurnitureItem furniture)
                {
                    furniture.RemoveUser(this);
                }

                SetState(CreatureState.Idle);
            }
        }
        else
        {
            // Structure doesn't satisfy any need, exit state
            SetState(CreatureState.Idle);
        }
    }

    // Called when creature reaches a destination
    private void OnReachedDestination()
    {
        // What happens next depends on why we were moving
        switch (brain.GetCurrentObjective())
        {
            case CreatureObjective.FulfillNeed:
                HandleReachedNeedDestination();
                break;

            case CreatureObjective.PickupItem:
                if (targetItem != null)
                {
                    PickupItem(targetItem);
                }
                SetState(CreatureState.Idle);
                break;

            case CreatureObjective.HaulItem:
                // Continue in hauling state until the haul is complete
                break;

            case CreatureObjective.Build:
                if (targetItem != null && targetItem is StructureItem structure)
                {
                    BeginBuilding(structure);
                }
                else
                {
                    SetState(CreatureState.Idle);
                }
                break;

            case CreatureObjective.GatherResource:
                BeginGathering();
                break;

            case CreatureObjective.Socialize:
                if (targetCreature != null)
                {
                    BeginSocializing(targetCreature);
                }
                else
                {
                    SetState(CreatureState.Idle);
                }
                break;

            default:
                SetState(CreatureState.Idle);
                break;
        }
    }

    // Handle reaching a destination for need fulfillment
    private void HandleReachedNeedDestination()
    {
        string needID = brain.GetTargetNeedID();
        if (string.IsNullOrEmpty(needID))
        {
            SetState(CreatureState.Idle);
            return;
        }

        // Check if there's a structure to use
        StructureItem structure = FindUsableStructureAt(targetPosition, needID);
        if (structure != null)
        {
            UseStructure(structure, needID);
            return;
        }

        // Check for consumable items in inventory
        Item consumable = FindConsumableFor(needID);
        if (consumable != null)
        {
            ConsumeItem(consumable);
            return;
        }

        // Handle natural needs (sleeping on ground, etc)
        if (needID.Contains("sleep"))
        {
            SetState(CreatureState.Sleeping);
            ShowActionIndicator("Sleep");
            return;
        }

        // Default - can't fulfill need here
        SetState(CreatureState.Idle);
    }

    // Movement coroutine
    private IEnumerator MoveToPosition(Vector3 targetPos)
    {
        isMoving = true;

        // Set movement animation
        if (animator != null)
        {
            animator.SetBool("Walking", true);
        }

        // Flip sprite based on direction
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = targetPos.x < transform.position.x;
        }

        // Calculate move duration based on speed and distance
        float distance = Vector2.Distance(transform.position, targetPos);
        float duration = distance / definition.moveSpeed;

        // Apply movement penalties if carrying heavy items
        if (currentCarryWeight > definition.carryCapacity * 0.5f)
        {
            float weightFactor = 1f + (currentCarryWeight / definition.carryCapacity);
            duration *= weightFactor;
        }

        float elapsedTime = 0f;
        Vector3 startPos = transform.position;

        while (elapsedTime < duration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure final position is exact
        transform.position = targetPos;

        // Update tile position
        currentPathIndex++;

        // Reset animation
        if (animator != null)
        {
            animator.SetBool("Walking", false);
        }

        isMoving = false;
    }

    // Set creature state
    public void SetState(CreatureState newState)
    {
        // Handle exit actions for current state
        switch (currentState)
        {
            case CreatureState.Moving:
                StopAllCoroutines(); // Stop movement coroutine
                isMoving = false;
                if (animator != null)
                {
                    animator.SetBool("Walking", false);
                }
                break;

            case CreatureState.UsingStructure:
                // Release any structure being used
                if (targetItem is FurnitureItem furniture)
                {
                    furniture.RemoveUser(this);
                }
                break;
        }

        // Switch to new state
        currentState = newState;

        // Handle enter actions for new state
        switch (newState)
        {
            case CreatureState.Idle:
                HideActionIndicator();
                break;

            case CreatureState.Moving:
                ShowActionIndicator("Moving");
                break;

            case CreatureState.Consuming:
                ShowActionIndicator("Eating");
                break;

            case CreatureState.Working:
                ShowActionIndicator("Working");
                break;

            case CreatureState.Building:
                ShowActionIndicator("Building");
                break;

            case CreatureState.Gathering:
                ShowActionIndicator("Gathering");
                break;

            case CreatureState.Sleeping:
                ShowActionIndicator("Sleeping");
                break;

            case CreatureState.Socializing:
                ShowActionIndicator("Talking");
                break;

            case CreatureState.Hauling:
                ShowActionIndicator("Hauling");
                break;

            case CreatureState.Fighting:
                ShowActionIndicator("Fighting");
                break;

            case CreatureState.Fleeing:
                ShowActionIndicator("Fleeing");
                break;
        }

        // Reset state timer and action progress
        stateTimer = 0f;
        actionProgress = 0f;
    }

    // Public methods for other systems to interact with creature

    // Get a need by ID
    public Need GetNeed(string needID)
    {
        if (needsDict.TryGetValue(needID, out Need need))
        {
            return need;
        }
        return null;
    }

    // Get all needs
    public List<Need> GetAllNeeds()
    {
        return new List<Need>(needsDict.Values);
    }

    // Get the most urgent need
    public Need GetMostUrgentNeed()
    {
        Need mostUrgent = null;
        float highestUrgency = 0f;

        foreach (var need in needsDict.Values)
        {
            float urgency = need.GetUrgency();
            if (urgency > highestUrgency)
            {
                highestUrgency = urgency;
                mostUrgent = need;
            }
        }

        return mostUrgent;
    }

    // Check if creature has an ability
    public bool HasAbility(string abilityID)
    {
        return abilitiesDict.ContainsKey(abilityID);
    }

    // Get an ability by ID
    public Ability GetAbility(string abilityID)
    {
        if (abilitiesDict.TryGetValue(abilityID, out Ability ability))
        {
            return ability;
        }
        return null;
    }

    // Get a list of all abilities
    public List<Ability> GetAllAbilities()
    {
        return new List<Ability>(abilitiesDict.Values);
    }

    // Take damage
    public void TakeDamage(float amount, string source = "")
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (currentHealth <= 0)
        {
            Die(source);
        }
    }

    // Die
    private void Die(string cause = "")
    {
        Debug.Log($"{creatureName} has died. Cause: {cause}");

        // Drop inventory
        DropAllItems();

        // Notify any systems that need to know
        if (WorldManager.Instance != null)
        {
            WorldManager.Instance.OnCreatureDied(this);
        }

        // Destroy game object
        Destroy(gameObject);
    }

    // Inventory management methods

    // Add item to inventory
    public bool AddItem(Item item)
    {
        if (item == null) return false;

        // Check weight capacity
        if (currentCarryWeight + (item.definition.weight * item.quantity) > definition.carryCapacity)
        {
            return false;
        }

        // Check for stackable items
        if (item.definition.isStackable)
        {
            foreach (var existingItem in inventory)
            {
                if (existingItem.CanStack(item))
                {
                    if (existingItem.Stack(item))
                    {
                        // Update weight
                        currentCarryWeight += item.definition.weight * item.quantity;

                        // If we used up all of the new item
                        if (item.quantity <= 0)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        // If we still have items to add and have slots available
        if (inventory.Count < definition.inventorySlots)
        {
            inventory.Add(item);
            currentCarryWeight += item.definition.weight * item.quantity;
            return true;
        }

        return false;
    }

    // Remove item from inventory
    public Item RemoveItem(Item item)
    {
        if (item == null) return null;

        int index = inventory.IndexOf(item);
        if (index >= 0)
        {
            Item removedItem = inventory[index];
            inventory.RemoveAt(index);

            currentCarryWeight -= removedItem.definition.weight * removedItem.quantity;

            return removedItem;
        }

        return null;
    }

    // Find item in inventory by definition ID
    public Item FindItemByID(string itemID)
    {
        return inventory.FirstOrDefault(item => item.definition.itemID == itemID);
    }

    // Find item in inventory by tag
    public Item FindItemByTag(string tagID)
    {
        return inventory.FirstOrDefault(item => item.definition.HasTag(tagID));
    }

    // Drop all items
    private void DropAllItems()
    {
        // Implementation depends on world item system
        foreach (var item in inventory)
        {
            // Create world item at current position
            WorldManager.Instance.CreateWorldItem(item, transform.position);
        }

        inventory.Clear();
        currentCarryWeight = 0f;
    }

    // Action helpers

    // Start moving to a position
    public void MoveTo(Vector2Int targetPos)
    {
        // Clear previous path
        currentPath = null;
        currentPathIndex = 0;
        isMoving = false;

        // Get current position
        TileSystem tileSystem = FindObjectOfType<TileSystem>();
        if (tileSystem != null)
        {
            Vector2Int currentPos = tileSystem.GetTilePosition(transform.position);
            Vector2Int randomPos = GetRandomNearbyPosition(currentPos, 10);

            currentObjective = CreatureObjective.None;
            creature.MoveTo(randomPos);
            return true;
        }

        // Find path
        currentPath = Pathfinding.Instance.FindPath(currentPos, targetPos);

        if (currentPath != null && currentPath.Count > 0)
        {
            targetPosition = targetPos;
            SetState(CreatureState.Moving);
        }
        else
        {
            Debug.LogWarning($"No path found from {currentPos} to {targetPos}");
            SetState(CreatureState.Idle);
        }
    }

    // Pick up an item
    private void PickupItem(Item item)
    {
        if (item == null) return;

        if (AddItem(item))
        {
            // Remove item from world
            WorldManager.Instance.RemoveWorldItem(item);
        }
    }

    // Place an item
    private void PlaceItem(Item item, Vector2Int position)
    {
        if (item == null) return;

        // Remove from inventory
        Item removedItem = RemoveItem(item);
        if (removedItem != null)
        {
            // Place in world
            WorldManager.Instance.PlaceWorldItem(removedItem, position);
        }
    }

    // Begin building
    private void BeginBuilding(StructureItem structure)
    {
        if (structure == null) return;

        // Check if we have the build ability
        Ability buildAbility = null;
        foreach (var ability in abilitiesDict.Values)
        {
            if (ability.definition is BuildAbility)
            {
                buildAbility = ability;
                break;
            }
        }

        if (buildAbility == null)
        {
            Debug.LogWarning($"{creatureName} cannot build - missing build ability");
            SetState(CreatureState.Idle);
            return;
        }

        // Check if we have all required materials in inventory
        bool hasMaterials = true;
        foreach (var ingredient in structure.definition.craftingIngredients)
        {
            int requiredCount = ingredient.quantity;
            int foundCount = 0;

            foreach (var item in inventory)
            {
                if ((ingredient.specificItemID != null && item.definition.itemID == ingredient.specificItemID) ||
                    (ingredient.itemTag != null && item.definition.HasTag(ingredient.itemTag.tagID)))
                {
                    foundCount += item.quantity;
                }
            }

            if (foundCount < requiredCount)
            {
                hasMaterials = false;
                break;
            }
        }

        if (!hasMaterials)
        {
            Debug.LogWarning($"{creatureName} cannot build - missing materials");
            SetState(CreatureState.Idle);
            return;
        }

        // Start building
        targetItem = structure;
        stateTimer = GetBuildTime();
        buildAbility.StartUsing();
        SetState(CreatureState.Building);
    }

    // Complete building
    private void CompleteBuild()
    {
        if (targetItem == null || !(targetItem is StructureItem structure))
        {
            return;
        }

        // Consume materials from inventory
        foreach (var ingredient in structure.definition.craftingIngredients)
        {
            int requiredCount = ingredient.quantity;

            // Find matching items
            for (int i = inventory.Count - 1; i >= 0 && requiredCount > 0; i--)
            {
                Item item = inventory[i];

                bool isMatch = (ingredient.specificItemID != null && item.definition.itemID == ingredient.specificItemID) ||
                               (ingredient.itemTag != null && item.definition.HasTag(ingredient.itemTag.tagID));

                if (isMatch)
                {
                    if (item.quantity <= requiredCount)
                    {
                        // Use entire stack
                        requiredCount -= item.quantity;
                        currentCarryWeight -= item.definition.weight * item.quantity;
                        inventory.RemoveAt(i);
                    }
                    else
                    {
                        // Use partial stack
                        item.quantity -= requiredCount;
                        currentCarryWeight -= item.definition.weight * requiredCount;
                        requiredCount = 0;
                    }
                }
            }
        }

        // Mark as built and place in world
        structure.isBuilt = true;
        WorldManager.Instance.PlaceStructure(structure, targetPosition);

        // Apply build ability
        Ability buildAbility = GetBuildAbilities().FirstOrDefault();
        if (buildAbility != null)
        {
            buildAbility.FinishUsing(1f); // 1 second cooldown

            // Apply quality modifier if applicable
            if (buildAbility.definition is BuildAbility buildDef)
            {
                // Apply quality modifier
                structure.durability = Mathf.Min(1f, structure.durability + buildDef.qualityModifier);
            }
        }

        targetItem = null;
    }

    // Begin gathering
    private void BeginGathering()
    {
        // Implementation depends on resource system
        ResourceNode resource = WorldManager.Instance.GetResourceNodeAt(targetPosition);
        if (resource == null)
        {
            SetState(CreatureState.Idle);
            return;
        }

        // Check if we have the gather ability
        Ability gatherAbility = null;
        foreach (var ability in abilitiesDict.Values)
        {
            if (ability.definition is GatherAbility)
            {
                gatherAbility = ability;
                break;
            }
        }

        if (gatherAbility == null)
        {
            Debug.LogWarning($"{creatureName} cannot gather - missing gather ability");
            SetState(CreatureState.Idle);
            return;
        }

        // Check if we can gather this resource type
        GatherAbility gatherDef = gatherAbility.definition as GatherAbility;
        if (gatherDef != null)
        {
            bool canGather = false;
            foreach (var validTag in gatherDef.validResourceTags)
            {
                if (resource.HasResourceTag(validTag.tagID))
                {
                    canGather = true;
                    break;
                }
            }

            if (!canGather)
            {
                Debug.LogWarning($"{creatureName} cannot gather this resource type");
                SetState(CreatureState.Idle);
                return;
            }
        }

        // Start gathering
        stateTimer = GetGatherTime();
        gatherAbility.StartUsing();
        SetState(CreatureState.Gathering);
    }

    // Complete gathering
    private void CompleteGathering()
    {
        ResourceNode resource = WorldManager.Instance.GetResourceNodeAt(targetPosition);
        if (resource == null)
        {
            return;
        }

        // Get the gather ability
        Ability gatherAbility = null;
        foreach (var ability in abilitiesDict.Values)
        {
            if (ability.definition is GatherAbility)
            {
                gatherAbility = ability;
                break;
            }
        }

        if (gatherAbility == null)
        {
            return;
        }

        // Get gathered resources
        List<Item> gatheredItems = resource.Harvest(this, gatherAbility);

        // Add to inventory
        foreach (var item in gatheredItems)
        {
            if (!AddItem(item))
            {
                // Drop on ground if inventory is full
                WorldManager.Instance.CreateWorldItem(item, transform.position);
            }
        }

        // Apply cooldown
        gatherAbility.FinishUsing(1f);
    }

    // Begin socializing
    private void BeginSocializing(Creature other)
    {
        if (other == null)
        {
            SetState(CreatureState.Idle);
            return;
        }

        targetCreature = other;
        SetState(CreatureState.Socializing);

        // Ask the other creature to socialize too if they're not busy
        if (other.currentState == CreatureState.Idle)
        {
            other.targetCreature = this;
            other.SetState(CreatureState.Socializing);
        }
    }

    // Consume an item
    private void ConsumeItem(Item item)
    {
        if (item == null)
        {
            SetState(CreatureState.Idle);
            return;
        }

        targetItem = item;
        stateTimer = GetConsumableUseTime(item);
        SetState(CreatureState.Consuming);
    }

    // Use an item
    private void UseItem(Item item)
    {
        if (item == null) return;

        item.Use(this);

        // Remove item if it's used up
        if (item is ConsumableItem consumable)
        {
            if (consumable.usesRemaining <= 0 && consumable.quantity <= 0)
            {
                RemoveItem(item);
            }
        }
    }

    // Use a structure
    private void UseStructure(StructureItem structure, string needID)
    {
        if (structure == null)
        {
            SetState(CreatureState.Idle);
            return;
        }

        // Check if structure can satisfy this need
        float effectiveness;
        if (!structure.CanSatisfyNeed(needID, out effectiveness))
        {
            SetState(CreatureState.Idle);
            return;
        }

        // For furniture, try to add as user
        if (structure is FurnitureItem furniture)
        {
            if (!furniture.AddUser(this))
            {
                // Furniture is full
                SetState(CreatureState.Idle);
                return;
            }
        }

        targetItem = structure;
        SetState(CreatureState.UsingStructure);
    }

    // Perform attack in combat
    private void PerformAttack()
    {
        if (targetCreature == null) return;

        // Get combat-related abilities for attack calculations
        float damageAmount = 5f; // Base damage

        // Apply bonuses from abilities, traits, etc.
        // This would be expanded in a real combat system

        // Deal damage
        targetCreature.TakeDamage(damageAmount, creatureName);

        // Consume stamina
        currentStamina = Mathf.Max(0, currentStamina - 10f);
    }

    // Find usable structures at a position
    private StructureItem FindUsableStructureAt(Vector2Int position, string needID)
    {
        return WorldManager.Instance.GetUsableStructureAt(position, needID);
    }

    // Find consumable for a need
    private Item FindConsumableFor(string needID)
    {
        foreach (var item in inventory)
        {
            float effectiveness;
            if (item.CanSatisfyNeed(needID, out effectiveness))
            {
                return item;
            }
        }

        return null;
    }

    // Get creature's build abilities
    private List<Ability> GetBuildAbilities()
    {
        return abilitiesDict.Values.Where(a => a.definition is BuildAbility).ToList();
    }

    // Get creature's gather abilities
    private List<Ability> GetGatherAbilities()
    {
        return abilitiesDict.Values.Where(a => a.definition is GatherAbility).ToList();
    }

    // Get time to build something
    private float GetBuildTime()
    {
        Ability buildAbility = GetBuildAbilities().FirstOrDefault();

        if (buildAbility != null && targetItem != null)
        {
            // Apply build speed modifier
            float baseTime = targetItem.definition.craftingTime;

            if (buildAbility.definition is BuildAbility buildDef)
            {
                return baseTime / buildDef.buildSpeedMultiplier;
            }

            return baseTime / buildAbility.GetEffectiveness();
        }

        return 10f; // Default build time
    }

    // Get time to gather resources
    private float GetGatherTime()
    {
        Ability gatherAbility = GetGatherAbilities().FirstOrDefault();

        if (gatherAbility != null)
        {
            return gatherAbility.GetCompletionTime();
        }

        return 5f; // Default gather time
    }

    // Get time for generic work
    private float GetWorkTime()
    {
        // Implementation depends on work type
        return 5f;
    }

    // Get time to use a consumable
    private float GetConsumableUseTime(Item item)
    {
        if (item is ConsumableItem consumable)
        {
            // Look for a need satisfier with use time
            foreach (var satisfier in consumable.definition.needSatisfiers)
            {
                return satisfier.useTime;
            }
        }

        return 3f; // Default consumption time
    }

    // Get time to use a regular item
    private float GetItemUseTime(Item item)
    {
        // Implementation depends on item type
        return 3f;
    }

    // Get time between attacks
    private float GetAttackTime()
    {
        // Implementation would depend on combat ability and weapon
        return 1.5f;
    }

    // Action indicator methods
    private void ShowActionIndicator(string action)
    {
        if (actionIndicator != null)
        {
            actionIndicator.SetText(action);
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
}

// Enum for creature states
public enum CreatureState
{
    Idle,
    Moving,
    Consuming,
    Working,
    Building,
    Gathering,
    Sleeping,
    Socializing,
    Hauling,
    Fighting,
    Fleeing,
    UsingItem,
    UsingStructure
}

// Enum for creature objectives (for the brain)
public enum CreatureObjective
{
    None,
    FulfillNeed,
    PickupItem,
    HaulItem,
    Build,
    GatherResource,
    Socialize,
    Combat,
    Flee
}

// Creature brain - handles AI decision making
public class CreatureBrain : MonoBehaviour
{
    private Creature creature;
    private CreatureObjective currentObjective = CreatureObjective.None;
    private string targetNeedID;

    private void Awake()
    {
        creature = GetComponent<Creature>();
    }

    // Main decision making method - called when idle
    public void MakeDecision()
    {
        // First check for urgent needs
        if (TryAddressCriticalNeeds())
        {
            return;
        }

        // Check for assigned job/task
        if (TryHandleAssignedTask())
        {
            return;
        }

        // Address any need below threshold
        if (TryAddressRegularNeeds())
        {
            return;
        }

        // If nothing else to do, explore, socialize, or perform idle behaviors
        TryIdleBehavior();
    }

    // Try to address critical needs
    private bool TryAddressCriticalNeeds()
    {
        List<Need> criticalNeeds = new List<Need>();

        foreach (var need in creature.GetAllNeeds())
        {
            if (need.IsCritical())
            {
                criticalNeeds.Add(need);
            }
        }

        if (criticalNeeds.Count > 0)
        {
            // Sort by urgency
            criticalNeeds.Sort((a, b) => b.GetUrgency().CompareTo(a.GetUrgency()));

            // Address most urgent need
            AddressNeed(criticalNeeds[0]);
            return true;
        }

        return false;
    }

    // Try to handle assigned task
    private bool TryHandleAssignedTask()
    {
        // Implementation would depend on task assignment system
        return false;
    }

    // Try to address any need below a comfortable threshold
    private bool TryAddressRegularNeeds()
    {
        List<Need> needsToAddress = new List<Need>();

        foreach (var need in creature.GetAllNeeds())
        {
            // Address needs below 70%
            if (need.currentValue < 70f)
            {
                needsToAddress.Add(need);
            }
        }

        if (needsToAddress.Count > 0)
        {
            // Sort by urgency
            needsToAddress.Sort((a, b) => b.GetUrgency().CompareTo(a.GetUrgency()));

            // Address most urgent need
            AddressNeed(needsToAddress[0]);
            return true;
        }

        return false;
    }

    // Try idle behaviors when nothing else to do
    private bool TryIdleBehavior()
    {
        // Random chance for different idle behaviors
        float roll = Random.value;

        if (roll < 0.3f)
        {
            // Explore - move to a random nearby position
            TileSystem tileSystem = FindObjectOfType<TileSystem>();
            if (tileSystem != null)
            {
                Vector2Int currentPos = tileSystem.GetTilePosition(transform.position);
                Vector2Int randomPos = GetRandomNearbyPosition(currentPos, 10);

                currentObjective = CreatureObjective.None;
                creature.MoveTo(randomPos);
                return true;
            }
            Vector2Int randomPos = GetRandomNearbyPosition(currentPos, 10);

            currentObjective = CreatureObjective.None;
            creature.MoveTo(randomPos);
            return true;
        }
        else if (roll < 0.5f)
        {
            // Try to socialize if we have a social need
            Need socialNeed = null;
            foreach (var need in creature.GetAllNeeds())
            {
                if (need.definition.needID.Contains("social"))
                {
                    socialNeed = need;
                    break;
                }
            }

            if (socialNeed != null && socialNeed.currentValue < 90f)
            {
                // Find another creature to socialize with
                Creature other = WorldManager.Instance.FindNearestIdleCreature(transform.position, creature);

                if (other != null)
                {
                    currentObjective = CreatureObjective.Socialize;
                    creature.targetCreature = other;
                    creature.MoveTo(TileSystem.Instance.GetTilePosition(other.transform.position));
                    return true;
                }
            }
        }
        else if (roll < 0.7f && WorldManager.Instance.HasPendingJobs())
        {
            // Try to take a job from the job system
            Job job = WorldManager.Instance.GetSuitableJob(creature);

            if (job != null)
            {
                // Handle job based on type
                switch (job.jobType)
                {
                    case JobType.Build:
                        currentObjective = CreatureObjective.Build;
                        creature.targetItem = job.targetItem;
                        creature.MoveTo(job.targetPosition);
                        return true;

                    case JobType.Haul:
                        currentObjective = CreatureObjective.HaulItem;
                        creature.targetItem = job.targetItem;
                        creature.targetPosition = job.targetPosition;
                        creature.MoveTo(TileSystem.Instance.GetTilePosition(job.targetItem.transform.position));
                        return true;

                    case JobType.Gather:
                        currentObjective = CreatureObjective.GatherResource;
                        creature.MoveTo(job.targetPosition);
                        return true;
                }
            }
        }

        // Default - just wait
        return false;
    }

    // Address a specific need
    public void AddressMostUrgentNeed()
    {
        Need urgentNeed = creature.GetMostUrgentNeed();
        if (urgentNeed != null)
        {
            AddressNeed(urgentNeed);
        }
    }

    public List<Item> GetInventoryItems()
    {
        return new List<Item>(inventory);
    }

    // Address a specific need
    private void AddressNeed(Need need)
    {
        if (need == null) return;

        targetNeedID = need.definition.needID;
        currentObjective = CreatureObjective.FulfillNeed;

        // Check if we have a consumable in inventory that can satisfy this need
        Item consumable = null;
        foreach (var item in creature.GetInventoryItems())  // You need to implement this method
        {
            float effectiveness;
            if (item is ConsumableItem && item.CanSatisfyNeed(need.definition.needID, out effectiveness))
            {
                consumable = item;
                break;
            }
        }

        if (consumable != null)
        {
            // Use the consumable
            creature.targetItem = consumable;
            creature.SetState(CreatureState.Consuming);
            return;
        }

        // Try to find appropriate location to satisfy need
        Vector2Int destination = FindLocationForNeed(need);

        if (destination != Vector2Int.zero)
        {
            creature.MoveTo(destination);
        }
        else
        {
            // Couldn't find location
            Debug.LogWarning($"Couldn't find location to satisfy {need.definition.displayName} for {creature.creatureName}");

            // Just move somewhere random as fallback
            TileSystem tileSystem = FindObjectOfType<TileSystem>();
            if (tileSystem != null)
            {
                Vector2Int currentPos = tileSystem.GetTilePosition(transform.position);
                Vector2Int randomPos = GetRandomNearbyPosition(currentPos, 10);

                currentObjective = CreatureObjective.None;
                creature.MoveTo(randomPos);
                return true;
            }
            Vector2Int randomPos = GetRandomNearbyPosition(currentPos, 10);
            creature.MoveTo(randomPos);
        }
    }

    // Find appropriate location to satisfy a need
    private Vector2Int FindLocationForNeed(Need need)
    {
        if (need == null) return Vector2Int.zero;

        string needID = need.definition.needID;

        // Get current position
        TileSystem tileSystem = FindObjectOfType<TileSystem>();
        if (tileSystem != null)
        {
            Vector2Int currentPos = tileSystem.GetTilePosition(transform.position);
            Vector2Int randomPos = GetRandomNearbyPosition(currentPos, 10);

            currentObjective = CreatureObjective.None;
            creature.MoveTo(randomPos);
            return true;
        }

        // First try to find a structure that satisfies this need
        StructureItem structure = WorldManager.Instance.FindNearestStructureForNeed(
            needID, currentPos, 20f);

        if (structure != null)
        {
            return structure.worldPosition;
        }

        // If no structure, find an appropriate location based on need type
        if (needID.Contains("food") || needID.Contains("hunger"))
        {
            // Try to find food source
            Vector2Int foodSourcePos = WorldManager.Instance.FindNearestResourceOfType(
                "food", currentPos, 30f);

            if (foodSourcePos != Vector2Int.zero)
            {
                return foodSourcePos;
            }

            // Try stockpile with food
            Vector2Int stockpilePos = WorldManager.Instance.FindNearestStockpileWithTag(
                "food", currentPos, 30f);

            if (stockpilePos != Vector2Int.zero)
            {
                return stockpilePos;
            }
        }
        else if (needID.Contains("sleep") || needID.Contains("rest"))
        {
            // Find a quiet place to sleep if no bed
            Vector2Int sleepPos = WorldManager.Instance.FindSuitableSleepLocation(currentPos);

            if (sleepPos != Vector2Int.zero)
            {
                return sleepPos;
            }
        }
        else if (needID.Contains("drink") || needID.Contains("thirst"))
        {
            // Try to find water source
            Vector2Int waterSourcePos = WorldManager.Instance.FindNearestResourceOfType(
                "water", currentPos, 30f);

            if (waterSourcePos != Vector2Int.zero)
            {
                return waterSourcePos;
            }

            // Try stockpile with drinks
            Vector2Int stockpilePos = WorldManager.Instance.FindNearestStockpileWithTag(
                "drink", currentPos, 30f);

            if (stockpilePos != Vector2Int.zero)
            {
                return stockpilePos;
            }
        }
        else if (needID.Contains("social"))
        {
            // Find another creature to socialize with
            Creature other = WorldManager.Instance.FindNearestIdleCreature(transform.position, creature);

            if (other != null)
            {
                return TileSystem.Instance.GetTilePosition(other.transform.position);
            }

            // Or find a designated social area
            Vector2Int socialAreaPos = WorldManager.Instance.FindNearestAreaOfType(
                "social", currentPos, 30f);

            if (socialAreaPos != Vector2Int.zero)
            {
                return socialAreaPos;
            }
        }

        // Fallback - pick a random location
        return GetRandomNearbyPosition(currentPos, 10);
    }

    // Get a random position near the specified position
    private Vector2Int GetRandomNearbyPosition(Vector2Int center, int maxDistance)
    {
        for (int attempts = 0; attempts < 10; attempts++)
        {
            int dx = Random.Range(-maxDistance, maxDistance + 1);
            int dy = Random.Range(-maxDistance, maxDistance + 1);

            Vector2Int testPos = new Vector2Int(center.x + dx, center.y + dy);

            // Check if walkable
            if (TileSystem.Instance.IsTileWalkable(testPos))
            {
                return testPos;
            }
        }

        // Fallback to original position if no walkable tile found
        return center;
    }

    // Check if there's a critical reason to interrupt current state
    public bool HasCriticalInterrupt()
    {
        // Check for critical needs
        foreach (var need in creature.GetAllNeeds())
        {
            if (need.IsCritical())
            {
                return true;
            }
        }

        // Check for danger
        // This would be expanded in a real danger system

        return false;
    }

    // Get the current objective
    public CreatureObjective GetCurrentObjective()
    {
        return currentObjective;
    }

    // Get the target need ID
    public string GetTargetNeedID()
    {
        return targetNeedID;
    }
}

// Resource node class for gathering system
public class ResourceNode : MonoBehaviour
{
    public string resourceType;
    public List<ResourceTag> resourceTags = new List<ResourceTag>();
    public float resourceAmount = 100f;
    public float maxResourceAmount = 100f;
    public float regrowthRate = 0.1f;
    public bool canRegrow = true;
    public List<ItemDefinition> possibleDrops = new List<ItemDefinition>();

    public bool HasResourceTag(string tagID)
    {
        return resourceTags.HasTag(tagID);
    }

    public List<Item> Harvest(Creature harvester, Ability gatherAbility)
    {
        List<Item> result = new List<Item>();

        if (resourceAmount <= 0)
        {
            return result;
        }

        // Calculate how much to harvest
        float harvestAmount = 10f; // Base amount

        if (gatherAbility != null && gatherAbility.definition is GatherAbility gatherDef)
        {
            harvestAmount *= gatherDef.gatherEfficiency;

            // Check for bonus resources
            if (Random.value < gatherDef.bonusResourceChance)
            {
                harvestAmount *= 1.5f;
            }
        }

        // Limit by available amount
        harvestAmount = Mathf.Min(harvestAmount, resourceAmount);
        resourceAmount -= harvestAmount;

        // Create items based on harvest amount
        foreach (var dropDef in possibleDrops)
        {
            // Calculate quantity based on harvest amount and item value
            int quantity = Mathf.FloorToInt(harvestAmount / dropDef.value);

            if (quantity > 0)
            {
                Item item = dropDef.CreateInstance(quantity);
                result.Add(item);
            }
        }

        // Check if depleted
        if (resourceAmount <= 0 && !canRegrow)
        {
            // Resource is permanently depleted
            Destroy(gameObject);
        }

        return result;
    }

    private void Update()
    {
        // Regrow resources over time
        if (canRegrow && resourceAmount < maxResourceAmount)
        {
            resourceAmount = Mathf.Min(maxResourceAmount, resourceAmount + regrowthRate * Time.deltaTime);
        }
    }
}

// Basic job for job system
public class Job
{
    public JobType jobType;
    public int priority;
    public Vector2Int targetPosition;
    public Item targetItem;
    public List<string> requiredAbilities = new List<string>();
    public float jobDuration;

    public bool IsCreatureSuitable(Creature creature)
    {
        foreach (var abilityID in requiredAbilities)
        {
            if (!creature.HasAbility(abilityID))
            {
                return false;
            }
        }

        return true;
    }
}

// Job types
public enum JobType
{
    Build,
    Haul,
    Gather,
    Craft,
    Repair,
    Combat
}