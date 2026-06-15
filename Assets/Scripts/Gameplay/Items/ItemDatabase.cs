using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance;

    public List<ItemData> allItems;

    private List<ItemData> availableItems = new List<ItemData>();
    private System.Random seededRandom;

    private void Awake()
    {
        Instance = this;

        // copy items into working list
        availableItems = new List<ItemData>(allItems);
    }

    public void SetSeed(int seed)
    {
        seededRandom = new System.Random(seed);
    }

    public void ClearSeed()
    {
        seededRandom = null;
    }

    public List<ItemData> GetRandomItems(int count)
    {
        return GetRandomItems(count, seededRandom);
    }

    public List<ItemData> GetRandomItems(int count, System.Random rng)
    {
        List<ItemData> result = new List<ItemData>();
        List<ItemData> pool = new List<ItemData>(availableItems);

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            ItemData selected = GetWeightedRandom(pool, rng);
            result.Add(selected);

            pool.Remove(selected); // no duplicates in same roll
        }

        return result;
    }

    private ItemData GetWeightedRandom(List<ItemData> pool, System.Random rng)
    {
        int totalWeight = 0;

        foreach (var item in pool)
            totalWeight += Mathf.Max(1, item.weight);

        int random = rng != null ? rng.Next(0, totalWeight) : Random.Range(0, totalWeight);

        foreach (var item in pool)
        {
            int weight = Mathf.Max(1, item.weight);
            if (random < weight)
                return item;

            random -= weight;
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
