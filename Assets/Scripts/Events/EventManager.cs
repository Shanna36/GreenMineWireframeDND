using System.Collections;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    [Header("References")]
    public EventPopupUI popupUI;

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
        if (!enableDebugHotkeys) return;

        if (logisticsDebugEvent != null && Input.GetKeyDown(logisticsTriggerKey))
        {
            TriggerEvent(logisticsDebugEvent);
        }

        if (maintenanceDebugEvent != null && Input.GetKeyDown(maintenanceTriggerKey))
        {
            TriggerEvent(maintenanceDebugEvent);
        }
    }

    public void TriggerEvent(EventDefinitionSO def)
    {
        Debug.Log($"TriggerEvent: {def.eventName} | actionText={def.actionText} | id={def.eventId}");
        if (def == null) return;

        // Only allow one active event at a time in V1
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

            case EventType.MaintenanceDegrade:
                activeEventRoutine = StartCoroutine(HandleMaintenanceDegrade(def));
                break;

            default:
                Debug.LogWarning($"No handler implemented for event type {def.eventType}");
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

        // Apply yellow-state degradation
        targetSlot.ApplyThroughputMultiplier(0.7f);

        bool resolved = false;

        void PayToFix()
        {
            if (MoneyManager.Instance == null) return;

            bool paid = MoneyManager.Instance.TrySpend(
                def.bypassCost,
                PayType.Maintenance,
                def.bypassLabel
            );

            if (!paid) return;

            targetSlot.RestoreOperational();
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
            PayToFix,
            "Wait",
            WaitItOut
        );

        float endTime = Time.time + Mathf.Max(0f, def.timerSeconds);
        while (!resolved && Time.time < endTime)
        {
            if (popupUI != null && popupUI.root.activeSelf)
            {
                popupUI.SetTimerVisible(true, endTime - Time.time);
            }
            yield return null;
        }

        if (!resolved)
        {
            targetSlot.RestoreOperational();
        }

        activeEventRoutine = null;
    }
}