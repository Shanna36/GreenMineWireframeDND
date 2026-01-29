using TMPro;
using UnityEngine;

/// <summary>
/// UI controller for displaying the player's money.
/// Listens to MoneyManager balance changes.
/// </summary>
public class MoneyHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;

    private void OnEnable()
    {
        Debug.Log("MoneyHUD OnEnable fired");

        if (moneyText == null)
        {
            Debug.LogError("MoneyHUD: moneyText is not assigned in the Inspector.");
            return;
        }

        StartCoroutine(SubscribeWhenReady());
    }

    private System.Collections.IEnumerator SubscribeWhenReady()
    {
        // Wait until MoneyManager exists (scene load order can make HUD enable first)
        while (MoneyManager.Instance == null)
        {
            yield return null;
        }

        Debug.Log("MoneyHUD subscribed to MoneyManager");
        MoneyManager.Instance.OnBalanceChanged += HandleBalanceChanged;

        // Force an initial refresh
        HandleBalanceChanged(MoneyManager.Instance.CurrentCoins, 0, TransactionType.Ship, "Init");
    }

    private void OnDisable()
    {
        // Always unsubscribe to avoid memory leaks / duplicate calls
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnBalanceChanged -= HandleBalanceChanged;
        }
    }

    private void HandleBalanceChanged(
        int newBalance,
        int delta,
        TransactionType type,
        string label
    )
    {
        moneyText.text = $"{newBalance:N0}";
    }
}
