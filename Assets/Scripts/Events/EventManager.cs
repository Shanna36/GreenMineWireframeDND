// Assets/Scripts/Events/EventManager.cs
using System.Collections;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    [Header("Refs")]
    public EventPopupUI popupUI;

    [Header("Debug")]
    public bool enableDebugHotkey = true;
    public KeyCode debugTriggerKey = KeyCode.L;
    public EventDefinitionSO debugEvent;

    private Coroutine activeEventRoutine;

    private void Update()
    {
        if (!enableDebugHotkey) return;
        if (debugEvent == null) return;

        if (Input.GetKeyDown(debugTriggerKey))
        {
            TriggerEvent(debugEvent);
        }
    }

    public void TriggerEvent(EventDefinitionSO def)
    {
        if (def == null) return;

        // Only allow one active event routine at a time in V1.
        if (activeEventRoutine != null)
        {
            StopCoroutine(activeEventRoutine);
            activeEventRoutine = null;
        }

        switch (def.eventType)
        {
            case EventType.LogisticsDelay:
                activeEventRoutine = StartCoroutine(HandleLogisticsDelay(def));
                break;

            default:
                Debug.LogWarning($"EventManager: No handler implemented for {def.eventType}");
                break;
        }
    }

    private IEnumerator HandleLogisticsDelay(EventDefinitionSO def)
    {
        // Apply the “shipping disabled” effect immediately.
        if (def.targetType == TargetType.Shipping)
        {
            if (PackingArea.Instance == null)
            {
                Debug.LogError("LogisticsDelay: PackingArea.Instance is null.");
                yield break;
            }

            PackingArea.Instance.DisableShippingForSeconds(def.timerSeconds);
        }
        else
        {
            Debug.LogWarning($"LogisticsDelay: TargetType {def.targetType} not implemented in V1.");
        }

        bool resolved = false;

        // Primary action: pay to bypass
        void PayToBypass()
        {
            if (!def.canPayToBypass) return;

            if (MoneyManager.Instance == null)
            {
                Debug.LogError("LogisticsDelay: MoneyManager.Instance is null.");
                return;
            }

            bool paid = MoneyManager.Instance.TrySpend(def.bypassCost, TransactionType.Purchase, def.bypassLabel);
            if (!paid)
            {
                Debug.Log("LogisticsDelay: Not enough money to bypass.");
                return;
            }

            // Re-enable shipping immediately.
            if (PackingArea.Instance != null)
                PackingArea.Instance.SetShippingDisabled(false);

            popupUI?.Hide();
            resolved = true;
        }

        // Secondary action: just close popup and wait it out
        void WaitItOut()
        {
            popupUI?.Hide();
            resolved = true;
        }

        // Show popup
        if (popupUI != null)
        {
            string primaryLabel = def.canPayToBypass ? $"{def.actionText} (£{def.bypassCost})" : "OK";
            popupUI.Show(def.eventName, def.playerPrompt, primaryLabel, PayToBypass, "Wait", WaitItOut);
        }

        // Optional: show “resolves in X seconds” in the popup while it’s open
        float endTime = Time.time + Mathf.Max(0f, def.timerSeconds);
        while (!resolved)
        {
            if (popupUI != null && popupUI.root != null && popupUI.root.activeSelf)
            {
                float remaining = Mathf.Max(0f, endTime - Time.time);
                popupUI.SetTimerVisible(true, remaining);
            }
            yield return null;
        }

        // Handler ends. Shipping auto re-enables inside PackingArea when the timer expires,
        // unless the player paid to bypass (we cleared it immediately).
        activeEventRoutine = null;
    }
}