using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemCardUI : MonoBehaviour
{
    public TMP_Text title;
    public TMP_Text description;
    public Button button;

    private ItemData currentItem;

    public void Setup(ItemData item)
    {
        currentItem = item;

        title.text = item.itemName;
        description.text = item.description;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        ItemSelectionUI.Instance.SelectItem(currentItem);
    }
}