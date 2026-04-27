using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance;

    public List<ItemData> allItems;

    private List<ItemData> availableItems = new List<ItemData>();

    private void Awake()
    {
        Instance = this;

        // copy items into working list
        availableItems = new List<ItemData>(allItems);
    }

    public List<ItemData> GetRandomItems(int count)
    {
        List<ItemData> result = new List<ItemData>();
        List<ItemData> pool = new List<ItemData>(availableItems);

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            ItemData selected = GetWeightedRandom(pool);
            result.Add(selected);

            pool.Remove(selected); // no duplicates in same roll
        }

        return result;
    }

    private ItemData GetWeightedRandom(List<ItemData> pool)
    {
        int totalWeight = 0;

        foreach (var item in pool)
            totalWeight += item.weight;

        int random = Random.Range(0, totalWeight);

        foreach (var item in pool)
        {
            if (random < item.weight)
                return item;

            random -= item.weight;
        }

        return pool[0];
    }

    public void RemoveIfNeeded(ItemData item)
    {
        if (!item.canRepeat)
        {
            availableItems.Remove(item);
        }
    }
}