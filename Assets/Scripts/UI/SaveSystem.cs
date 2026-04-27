using UnityEngine;
using System.Collections.Generic;

public static class SaveSystem
{
    public static void Save(List<UpgradeData> upgrades)
    {
        foreach (var u in upgrades)
        {
            PlayerPrefs.SetInt(u.id + "_level", u.currentLevel);
            PlayerPrefs.SetInt(u.id + "_equipped", u.equippedLevel);
        }
    }

    public static void Load(List<UpgradeData> upgrades)
    {
        foreach (var u in upgrades)
        {
            u.currentLevel = PlayerPrefs.GetInt(u.id + "_level", 0);
            u.equippedLevel = PlayerPrefs.GetInt(u.id + "_equipped", 0);
        }
    }
}