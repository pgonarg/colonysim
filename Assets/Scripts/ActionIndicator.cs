using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionIndicator : MonoBehaviour
{
    [Header("Visual Settings")]
    public float floatHeight = 0.7f;
    public float bobAmount = 0.2f;
    public float bobSpeed = 2f;
    
    [Header("Icon References")]
    public Sprite eatSprite;
    public Sprite sleepSprite;
    public Sprite drinkSprite;
    public Sprite talkSprite;
    public Sprite buildSprite;
    public Sprite gatherSprite;
    public Sprite workSprite;
    public Sprite moveSprite;
    public Sprite haulSprite;
    public Sprite fightSprite;
    public Sprite fleeSprite;
    public Sprite useSprite;
    public Sprite defaultSprite;
    
    [Header("Critical Need Icons")]
    public Sprite hungryAlertSprite;
    public Sprite thirstyAlertSprite;
    public Sprite tiredAlertSprite;
    public Sprite lonelyAlertSprite;
    
    // Reference to transform to follow
    private Transform targetTransform;
    
    // Component references
    private SpriteRenderer iconRenderer;
    
    private void Awake()
    {
        // Get or add sprite renderer
        iconRenderer = GetComponent<SpriteRenderer>();
        if (iconRenderer == null)
        {
            iconRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // Set sorting layer
        iconRenderer.sortingLayerName = "UI";
        iconRenderer.sortingOrder = 10;
        
        // Set default sprite
        if (defaultSprite != null)
        {
            iconRenderer.sprite = defaultSprite;
        }
    }
    
    /// <summary>
    /// Initialize with the target to follow
    /// </summary>
    public void Initialize(Transform target)
    {
        targetTransform = target;
        
        // Start inactive
        SetActive(false);
    }
    
    private void Update()
    {
        if (targetTransform == null)
        {
            // Target is gone, destroy self
            Destroy(gameObject);
            return;
        }
        
        // Update position to follow target with bobbing effect
        UpdatePosition();
    }
    
    /// <summary>
    /// Update the indicator's position
    /// </summary>
    private void UpdatePosition()
    {
        // Calculate bobbing offset
        float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        
        // Position above target with bob effect
        transform.position = new Vector3(
            targetTransform.position.x,
            targetTransform.position.y + floatHeight + yOffset,
            targetTransform.position.z - 0.1f  // Slightly in front
        );
    }
    
    /// <summary>
    /// Set the action text (will be converted to appropriate icon)
    /// </summary>
    public void SetText(string action)
    {
        if (iconRenderer == null)
            return;
            
        // Convert action text to lowercase and trim for consistent matching
        string actionLower = action.ToLower().Trim();
        
        // Match action to appropriate sprite
        Sprite iconSprite = null;
        
        switch (actionLower)
        {
            // Basic actions
            case "eat":
            case "eating":
            case "consume":
                iconSprite = eatSprite;
                break;
                
            case "sleep":
            case "sleeping":
            case "zzz":
                iconSprite = sleepSprite;
                break;
                
            case "drink":
            case "drinking":
                iconSprite = drinkSprite;
                break;
                
            case "talk":
            case "talking":
            case "chat":
            case "socializing":
                iconSprite = talkSprite;
                break;
                
            case "build":
            case "building":
                iconSprite = buildSprite;
                break;
                
            case "gather":
            case "gathering":
            case "harvest":
                iconSprite = gatherSprite;
                break;
                
            case "work":
            case "working":
                iconSprite = workSprite;
                break;
                
            case "move":
            case "moving":
            case "walk":
            case "walking":
            case "explore":
                iconSprite = moveSprite;
                break;
                
            case "haul":
            case "hauling":
            case "carry":
                iconSprite = haulSprite;
                break;
                
            case "fight":
            case "fighting":
            case "attack":
                iconSprite = fightSprite;
                break;
                
            case "flee":
            case "fleeing":
            case "run":
                iconSprite = fleeSprite;
                break;
                
            case "use":
            case "using":
                iconSprite = useSprite;
                break;
                
            // Critical alerts
            case "hungry!":
            case "hungry":
                iconSprite = hungryAlertSprite;
                break;
                
            case "thirsty!":
            case "thirsty":
                iconSprite = thirstyAlertSprite;
                break;
                
            case "tired!":
            case "tired":
                iconSprite = tiredAlertSprite;
                break;
                
            case "lonely!":
            case "lonely":
                iconSprite = lonelyAlertSprite;
                break;
                
            // Default
            default:
                iconSprite = defaultSprite;
                break;
        }
        
        // Set the sprite (if we found a match and it's available)
        if (iconSprite != null)
        {
            iconRenderer.sprite = iconSprite;
        }
        else if (defaultSprite != null)
        {
            iconRenderer.sprite = defaultSprite;
        }
    }
    
    /// <summary>
    /// Set whether the indicator is active
    /// </summary>
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
    
    /// <summary>
    /// Load all icons from Resources folder
    /// </summary>
    public void LoadDefaultIcons()
    {
        if (eatSprite == null) eatSprite = Resources.Load<Sprite>("Icons/eat_icon");
        if (sleepSprite == null) sleepSprite = Resources.Load<Sprite>("Icons/sleep_icon");
        if (drinkSprite == null) drinkSprite = Resources.Load<Sprite>("Icons/drink_icon");
        if (talkSprite == null) talkSprite = Resources.Load<Sprite>("Icons/talk_icon");
        if (buildSprite == null) buildSprite = Resources.Load<Sprite>("Icons/build_icon");
        if (gatherSprite == null) gatherSprite = Resources.Load<Sprite>("Icons/gather_icon");
        if (workSprite == null) workSprite = Resources.Load<Sprite>("Icons/work_icon");
        if (moveSprite == null) moveSprite = Resources.Load<Sprite>("Icons/move_icon");
        if (haulSprite == null) haulSprite = Resources.Load<Sprite>("Icons/haul_icon");
        if (fightSprite == null) fightSprite = Resources.Load<Sprite>("Icons/fight_icon");
        if (fleeSprite == null) fleeSprite = Resources.Load<Sprite>("Icons/flee_icon");
        if (useSprite == null) useSprite = Resources.Load<Sprite>("Icons/use_icon");
        if (defaultSprite == null) defaultSprite = Resources.Load<Sprite>("Icons/default_icon");
        
        // Critical icons
        if (hungryAlertSprite == null) hungryAlertSprite = Resources.Load<Sprite>("Icons/hungry_icon");
        if (thirstyAlertSprite == null) thirstyAlertSprite = Resources.Load<Sprite>("Icons/thirsty_icon");
        if (tiredAlertSprite == null) tiredAlertSprite = Resources.Load<Sprite>("Icons/tired_icon");
        if (lonelyAlertSprite == null) lonelyAlertSprite = Resources.Load<Sprite>("Icons/lonely_icon");
    }
}