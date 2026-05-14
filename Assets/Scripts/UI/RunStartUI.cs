using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class RunStartUI : MonoBehaviour
{
    public static RunStartUI Instance;

    public MinionRowUI rowA;
    public MinionRowUI rowB;
    public MinionRowUI rowC;

    public TMP_Text totalText;
    public int maxTotal = 15;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;

        rowA.Setup(this);
        rowB.Setup(this);
        rowC.Setup(this);

        UpdateTotal();
    }

    public bool CanAdd()
    {
        return GetTotal() < maxTotal;
    }

    public void OnValueChanged()
    {
        UpdateTotal();
    }

    void UpdateTotal()
    {
        totalText.text = GetTotal() + "/" + maxTotal;
    }

    int GetTotal()
    {
        return rowA.GetValue() + rowB.GetValue() + rowC.GetValue();
    }

    public void StartRun()
    {
        // Save values
        RunSetupData.Instance.typeA = rowA.GetValue();
        RunSetupData.Instance.typeB = rowB.GetValue();
        RunSetupData.Instance.typeC = rowC.GetValue();

        Time.timeScale = 1f;

        SceneManager.LoadScene("CaveScene"); // <-- your scene name
    }
}