using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Storage slot for a stockpile tile
[System.Serializable]
public class StorageSlot
{
    public Item item;
    public int quantity;
    public ItemTags allowedTags = ItemTags.None;
    public int maxCapacity = 50;

    public StorageSlot(ItemTags tags = ItemTags.None, int capacity = 50)
    {
        allowedTags = tags;
        maxCapacity = capacity;
        item = null;
        quantity = 0;
    }

    public bool CanStore(Item newItem)
    {
        // Check if slot is empty or item matches and is countable
        if (item == null)
        {
            return newItem.tags.HasFlag(allowedTags);
        }

        return item.itemName == newItem.itemName &&
               item.isCountable &&
               quantity < maxCapacity;
    }

    public bool CanStore(ItemTags tags)
    {
        return (item == null || quantity < maxCapacity) &&
               (allowedTags == ItemTags.None || (allowedTags & tags) != 0);
    }

    public int AddItem(Item newItem, int amount = 1)
    {
        if (item == null)
        {
            item = newItem;
            quantity = Mathf.Min(amount, maxCapacity);
            return amount - quantity; // Return leftover
        }
        else if (item.itemName == newItem.itemName && item.isCountable)
        {
            int spaceAvailable = maxCapacity - quantity;
            int amountToAdd = Mathf.Min(amount, spaceAvailable);
            quantity += amountToAdd;
            return amount - amountToAdd; // Return leftover
        }

        return amount; // Could not add any
    }

    public Item RemoveItem(int amount = 1)
    {
        if (item == null || quantity <= 0)
        {
            return null;
        }

        Item removedItem = item;

        if (item.isCountable)
        {
            int amountToRemove = Mathf.Min(amount, quantity);
            quantity -= amountToRemove;

            if (quantity <= 0)
            {
                item = null;
            }
        }
        else
        {
            // Non-countable item, just remove it
            item = null;
            quantity = 0;
        }

        return removedItem;
    }

    public bool IsEmpty()
    {
        return item == null || quantity <= 0;
    }

    public bool IsFull()
    {
        return item != null && (!item.isCountable || quantity >= maxCapacity);
    }
}

public class StockpileTile
{
    public List<StorageSlot> slots = new List<StorageSlot>();
    public Vector2Int position;
    public bool isFull = false;

    public StockpileTile(Vector2Int pos, int numSlots = 3)
    {
        position = pos;

        // Create default slots
        for (int i = 0; i < numSlots; i++)
        {
            slots.Add(new StorageSlot());
        }
    }

    public bool CanStoreItem(Item item)
    {
        foreach (var slot in slots)
        {
            if (slot.CanStore(item))
            {
                return true;
            }
        }
        return false;
    }

    public bool CanStoreItemType(ItemTags tags)
    {
        foreach (var slot in slots)
        {
            if (slot.CanStore(tags))
            {
                return true;
            }
        }
        return false;
    }

    public int AddItem(Item item, int amount = 1)
    {
        int remaining = amount;

        // Try to add to matching slots first
        foreach (var slot in slots)
        {
            if (slot.item != null && slot.item.itemName == item.itemName)
            {
                remaining = slot.AddItem(item, remaining);
                if (remaining <= 0)
                {
                    break;
                }
            }
        }

        // If we still have items, try empty slots
        if (remaining > 0)
        {
            foreach (var slot in slots)
            {
                if (slot.IsEmpty() && slot.CanStore(item))
                {
                    remaining = slot.AddItem(item, remaining);
                    if (remaining <= 0)
                    {
                        break;
                    }
                }
            }
        }

        // Update full status
        UpdateFullStatus();

        return remaining;
    }

    public Item GetItem(string itemName, int amount = 1)
    {
        foreach (var slot in slots)
        {
            if (slot.item != null && slot.item.itemName == itemName)
            {
                Item item = slot.RemoveItem(amount);
                UpdateFullStatus();
                return item;
            }
        }
        return null;
    }

    public Item GetItemOfType(ItemTags tags)
    {
        foreach (var slot in slots)
        {
            if (slot.item != null && (slot.item.tags & tags) != 0)
            {
                Item item = slot.RemoveItem(1);
                UpdateFullStatus();
                return item;
            }
        }
        return null;
    }

    private void UpdateFullStatus()
    {
        isFull = true;
        foreach (var slot in slots)
        {
            if (!slot.IsFull())
            {
                isFull = false;
                break;
            }
        }
    }
}

public class StockpileSystem : MonoBehaviour
{
    public static StockpileSystem Instance { get; private set; }

    private Dictionary<Vector2Int, StockpileTile> stockpileTiles = new Dictionary<Vector2Int, StockpileTile>();

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

    public void RegisterStockpileTile(Vector2Int position)
    {
        if (!stockpileTiles.ContainsKey(position))
        {
            stockpileTiles[position] = new StockpileTile(position);
        }
    }

    public void UnregisterStockpileTile(Vector2Int position)
    {
        if (stockpileTiles.ContainsKey(position))
        {
            stockpileTiles.Remove(position);
        }
    }

    public bool CanStoreItemAt(Vector2Int position, Item item)
    {
        if (stockpileTiles.TryGetValue(position, out StockpileTile tile))
        {
            return tile.CanStoreItem(item);
        }
        return false;
    }

    public bool CanStoreItemTypeAt(Vector2Int position, ItemTags tags)
    {
        if (stockpileTiles.TryGetValue(position, out StockpileTile tile))
        {
            return tile.CanStoreItemType(tags);
        }
        return false;
    }

    public int AddItemAt(Vector2Int position, Item item, int amount = 1)
    {
        if (stockpileTiles.TryGetValue(position, out StockpileTile tile))
        {
            return tile.AddItem(item, amount);
        }
        return amount; // Couldn't add any
    }

    public Item GetItemAt(Vector2Int position, string itemName, int amount = 1)
    {
        if (stockpileTiles.TryGetValue(position, out StockpileTile tile))
        {
            return tile.GetItem(itemName, amount);
        }
        return null;
    }

    public Item GetItemOfTypeAt(Vector2Int position, ItemTags tags)
    {
        if (stockpileTiles.TryGetValue(position, out StockpileTile tile))
        {
            return tile.GetItemOfType(tags);
        }
        return null;
    }

    public Vector2Int FindStockpileForItem(Item item, Vector2Int nearPosition)
    {
        Vector2Int bestPosition = new Vector2Int(-1, -1);
        float closestDistance = float.MaxValue;

        foreach (var kvp in stockpileTiles)
        {
            Vector2Int pos = kvp.Key;
            StockpileTile tile = kvp.Value;

            if (!tile.isFull && tile.CanStoreItem(item))
            {
                float distance = Vector2Int.Distance(nearPosition, pos);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestPosition = pos;
                }
            }
        }

        return bestPosition;
    }

    public Vector2Int FindItemInStockpile(string itemName, Vector2Int nearPosition)
    {
        Vector2Int bestPosition = new Vector2Int(-1, -1);
        float closestDistance = float.MaxValue;

        foreach (var kvp in stockpileTiles)
        {
            Vector2Int pos = kvp.Key;
            StockpileTile tile = kvp.Value;

            foreach (var slot in tile.slots)
            {
                if (slot.item != null && slot.item.itemName == itemName && slot.quantity > 0)
                {
                    float distance = Vector2Int.Distance(nearPosition, pos);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        bestPosition = pos;
                    }
                    break; // Found an item in this tile
                }
            }
        }

        return bestPosition;
    }

    public Vector2Int FindItemTypeInStockpile(ItemTags tags, Vector2Int nearPosition)
    {
        Vector2Int bestPosition = new Vector2Int(-1, -1);
        float closestDistance = float.MaxValue;

        foreach (var kvp in stockpileTiles)
        {
            Vector2Int pos = kvp.Key;
            StockpileTile tile = kvp.Value;

            foreach (var slot in tile.slots)
            {
                if (slot.item != null && (slot.item.tags & tags) != 0 && slot.quantity > 0)
                {
                    float distance = Vector2Int.Distance(nearPosition, pos);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        bestPosition = pos;
                    }
                    break; // Found an item in this tile
                }
            }
        }

        return bestPosition;
    }

    public bool IsStockpileTile(Vector2Int position)
    {
        return stockpileTiles.ContainsKey(position);
    }

    public bool IsStockpileTileFull(Vector2Int position)
    {
        if (stockpileTiles.TryGetValue(position, out StockpileTile tile))
        {
            return tile.isFull;
        }
        return true; // Non-existent tiles are considered full
    }
}