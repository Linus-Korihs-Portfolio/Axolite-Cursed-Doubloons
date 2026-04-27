using UnityEngine;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public UpgradeRowUI rowPrefab;
    public Transform container;

    public List<UpgradeData> upgrades;

    void Start()
    {
        foreach (var upgrade in upgrades)
        {
            var row = Instantiate(rowPrefab, container);
            row.Setup(upgrade);
        }
    }
}