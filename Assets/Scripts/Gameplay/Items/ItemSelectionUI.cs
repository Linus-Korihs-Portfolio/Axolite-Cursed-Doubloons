using System.Collections.Generic;
using UnityEngine;

public class ItemSelectionUI : MonoBehaviour
{
    public static ItemSelectionUI Instance;

    public ItemCardUI[] cards;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;

        List<ItemData> items = ItemDatabase.Instance.GetRandomItems(3);

        for (int i = 0; i < cards.Length; i++)
        {
            if (i < items.Count)
            {
                cards[i].gameObject.SetActive(true);
                cards[i].Setup(items[i]);
            }
            else
            {
                cards[i].gameObject.SetActive(false);
            }
        }
    }

    public void SelectItem(ItemData item)
    {
        ItemDatabase.Instance.RemoveIfNeeded(item);

        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }
}
