using System.Collections;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    [Header("References")]
    public EventPopupUI popupUI;
    public GameStateManager gameStateManager;

    [Header("Debug Hotkeys")]
    public bool enableDebugHotkeys = true;

    [Header("Debug: Logistics")]
    public KeyCode logisticsTriggerKey = KeyCode.L;
    public EventDefinitionSO logisticsDebugEvent;

    [Header("Debug: Maintenance")]
    public KeyCode maintenanceTriggerKey = KeyCode.M;
    public EventDefinitionSO maintenanceDebugEvent;

    private Coroutine activeEventRoutine;

    private void Update()
    {
        if (gameStateManager != null && gameStateManager.IsGameOver) return;
        if (!enableDebugHotkeys) return;

        if (logisticsDebugEvent != null && Input.GetKeyDown(logisticsTriggerKey))
        {
            Debug.LogWarning($"[EventManager] Logistics hotkey pressed. eventId={logisticsDebugEvent.eventId} type={logisticsDebugEvent.eventType}");
            TriggerEvent(logisticsDebugEvent);
        }

        if (maintenanceDebugEvent != null && Input.GetKeyDown(maintenanceTriggerKey))
        {
            Debug.LogWarning($"[EventManager] Maintenance hotkey pressed. eventId={maintenanceDebugEvent.eventId} type={maintenanceDebugEvent.eventType}");
            TriggerEvent(maintenanceDebugEvent);
        }
    }

    public void TriggerEvent(EventDefinitionSO def)
    {
        if (def == null) return;
        if (gameStateManager != null && gameStateManager.IsGameOver) return;
        Debug.LogWarning($"[EventManager] TriggerEvent: {def.eventName} | id={def.eventId} | type={def.eventType}");
        Debug.LogWarning($"[EventManager] Routing: eventType={def.eventType} targetType={def.targetType} targetId='{def.targetId}' activeEventRoutine={(activeEventRoutine != null ? "YES" : "NO")}");

        // Only allow one active event at a time in V1
        if (activeEventRoutine != null)
        {
            StopCoroutine(activeEventRoutine);
            activeEventRoutine = null;
        }

        switch (def.eventType)
        {
            case EventType.LogisticsDelay:
                Debug.Log("[EventManager] Starting coroutine: HandleLogisticsDelay");
                activeEventRoutine = StartCoroutine(HandleLogisticsDelay(def));
                break;

            case EventType.MaintenanceDegrade:
                Debug.Log("[EventManager] Starting coroutine: HandleMaintenanceDegrade");
                activeEventRoutine = StartCoroutine(HandleMaintenanceDegrade(def));
                break;

            default:
                Debug.LogWarning($"No handler implemented for event type {def.eventType} (id={def.eventId})");
                break;
        }
    }

    private IEnumerator HandleLogisticsDelay(EventDefinitionSO def)
    {
        if (def.targetType == TargetType.Shipping)
        {
            if (PackingArea.Instance == null)
            {
                Debug.LogError("LogisticsDelay: PackingArea.Instance is null.");
                yield break;
            }

            PackingArea.Instance.DisableShippingForSeconds(def.timerSeconds);
        }

        bool resolved = false;

        void PayToBypass()
        {
            if (!def.canPayToBypass) return;
            if (MoneyManager.Instance == null) return;

            bool paid = MoneyManager.Instance.TrySpend(
                def.bypassCost,
                PayType.Purchase,
                def.bypassLabel
            );

            if (!paid) return;

            PackingArea.Instance?.SetShippingDisabled(false);
            popupUI?.Hide();
            resolved = true;
        }

        void WaitItOut()
        {
            popupUI?.Hide();
            resolved = true;
        }

        popupUI?.Show(
            def.eventName,
            def.playerPrompt,
            def.canPayToBypass ? $"{def.actionText} (£{def.bypassCost})" : "OK",
            PayToBypass,
            "Wait",
            WaitItOut
        );

        float endTime = Time.time + Mathf.Max(0f, def.timerSeconds);
        while (!resolved)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                popupUI?.Hide();
                activeEventRoutine = null;
                yield break;
            }
            if (popupUI != null && popupUI.root.activeSelf)
            {
                popupUI.SetTimerVisible(true, endTime - Time.time);
            }
            yield return null;
        }

        activeEventRoutine = null;
    }

    private IEnumerator HandleMaintenanceDegrade(EventDefinitionSO def)
    {
        Debug.Log($"[EventManager] Maintenance started: {def.eventName} targetId={def.targetId} timer={def.timerSeconds}");
        if (def.targetType != TargetType.MachineSlot)
        {
            Debug.LogWarning("MaintenanceDegrade: invalid target type for V1");
            yield break;
        }

        if (!System.Enum.TryParse(def.targetId, out MachineType machineType))
        {
            Debug.LogError($"MaintenanceDegrade: unknown MachineType '{def.targetId}'");
            yield break;
        }

        MachineSlot targetSlot = null;
        foreach (var slot in FindObjectsByType<MachineSlot>(FindObjectsSortMode.None))
        {
            if (slot.machineType == machineType)
            {
                targetSlot = slot;
                break;
            }
        }

        if (targetSlot == null)
        {
            Debug.LogError($"MaintenanceDegrade: no MachineSlot found for {machineType}");
            yield break;
        }

        //added to combat no loss condition

    

    // NEW: Skip if already broken
    if (!targetSlot.IsOperational)
    {
        targetSlot.SetWarningState(false);
        Debug.LogWarning($"[EventManager] Machine {machineType} is already broken. Skipping maintenance event.");
        activeEventRoutine = null;
        yield break;
    }

        // Yellow state: degraded throughput
        targetSlot.ApplyThroughputMultiplier(0.7f);

        bool decisionMade = false;
        bool paidMaintenance = false;

        void PayNow()
        {
            if (!def.canPayToBypass) return;
            if (MoneyManager.Instance == null) return;

            if (!MoneyManager.Instance.TrySpend(def.bypassCost, PayType.Maintenance, def.bypassLabel)) return;

            targetSlot.RestoreOperational();
            targetSlot.SetWarningState(false);
            popupUI?.Hide();
            paidMaintenance = true;
            decisionMade = true;
        }

        void Delay()
        {
            targetSlot.SetWarningState(true);
            popupUI?.Hide();
            decisionMade = true;
        }

        popupUI?.Show(
            def.eventName,
            def.playerPrompt,
            def.canPayToBypass ? $"{def.actionText} (£{def.bypassCost})" : "OK",
            PayNow,
            "Delay",
            Delay
        );

        while (!decisionMade)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                popupUI?.Hide();
                activeEventRoutine = null;
                yield break;
            }
            yield return null;
        }

        if (paidMaintenance)
        {
            activeEventRoutine = null;
            yield break;
        }

        // Escalation timer
        float endTime = Time.time + Mathf.Max(0f, def.timerSeconds);
        while (Time.time < endTime)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                popupUI?.Hide();
                activeEventRoutine = null;
                yield break;
            }
            yield return null;
        }

        // Red state: breakdown
        targetSlot.StopOperational();
        targetSlot.SetWarningState(false);

        int repairCost = Mathf.Max(def.bypassCost * 2, def.bypassCost + 1);
        bool repaired = false;
        bool ignored = false;

        void Repair()
        {
            if (MoneyManager.Instance == null) return;
            if (!MoneyManager.Instance.TrySpend(repairCost, PayType.Maintenance, "Repair")) return;

            targetSlot.RestoreOperational();
            targetSlot.SetWarningState(false);
            popupUI?.Hide();
            repaired = true;
        }

        void Ignore()
        {
            // Player can ignore for now; the machine remains broken.
            popupUI?.Hide();
            ignored = true;
        }

        popupUI?.Show(
            "Machine Breakdown",
            $"{machineType} has broken down due to delayed maintenance.",
            $"Repair (£{repairCost})",
            Repair,
            "Ignore",
            Ignore
        );

        // End the event routine when the player either repairs OR ignores.
        while (!repaired && !ignored)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                popupUI?.Hide();
                activeEventRoutine = null;
                yield break;
            }
            yield return null;
        }

        activeEventRoutine = null;
    }
}