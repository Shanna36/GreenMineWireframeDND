using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MachineButtonUI : MonoBehaviour
{
    public MachineConfig config;      
    public TextMeshProUGUI priceText; 
    public Button button;             

    void Start()
    {
        UpdateButton();
    }

        void OnEnable()
    {
        UpdateButton();
    }

    void UpdateButton()
    {
        if (config == null) return;

        int cost = config.cost;

        priceText.text = cost.ToString();

        if (MoneyManager.Instance == null) return;

        bool canAfford = MoneyManager.Instance.CurrentMoney >= cost;

        button.interactable = canAfford;

        // Optional: make price red if unaffordable
        priceText.color = canAfford ? Color.black : Color.red;
    }
}