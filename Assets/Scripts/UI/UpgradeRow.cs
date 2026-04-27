using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UpgradeRowUI : MonoBehaviour
{
    public List<Image> segments;
    public Button buyButton;

    private UpgradeData data;

    public void Setup(UpgradeData upgradeData)
    {
        data = upgradeData;

        // Setup segment buttons
        for (int i = 0; i < segments.Count; i++)
        {
            int index = i;

            Button btn = segments[i].GetComponent<Button>();

            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SetLevel(index));
            }
            else
            {
                Debug.LogError("Segment missing Button component!");
            }
        }

        // Setup buy button
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(Buy);
        }
        else
        {
            Debug.LogError("BuyButton not assigned!");
        }

        Refresh();
    }

    public void Refresh()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (i < data.currentLevel)
                segments[i].color = Color.blue; // purchased
            else
                segments[i].color = Color.black; // empty
        }
    }

    public void Buy()
    {
        if (data.currentLevel < data.maxLevel)
        {
            data.currentLevel++;
            data.equippedLevel = data.currentLevel;

            Refresh();
        }
    }
    public void SetLevel(int index)
    {
        if (index < data.currentLevel)
        {
            data.equippedLevel = index + 1;
            Refresh();
        }
    }
}
