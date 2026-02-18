using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class EventManager : MonoBehaviour
{
    [Header("References")]
    public EventPopupUI popupUI;
    public GameStateManager gameStateManager;

    [Header("Debug Hotkeys")]
    public bool enableDebugHotkeys = true;

    [Header("Auto Events (V1)")]
    [Tooltip("If enabled, the EventManager will automatically trigger the configured debug events on timers (in addition to hotkeys).")]
    public bool enableAutoEvents = true;

    [Tooltip("Minimum seconds between auto-triggered events.")]
    public float autoCooldownSeconds = 25f;

    [Tooltip("Guarantee that each auto event triggers at least once within this many seconds after play starts.")]
    public float guaranteeWindowSeconds = 300f; // ~5 minutes

    [Header("Auto Timing Windows (seconds after start)")]
    public Vector2 logisticsAutoWindow = new Vector2(45f, 120f);
    public Vector2 maintenanceAutoWindow = new Vector2(75f, 180f);
    public Vector2 contaminationAutoWindow = new Vector2(105f, 240f);

    [Header("Debug: Logistics")]
    public KeyCode logisticsTriggerKey = KeyCode.L;
    public EventDefinitionSO logisticsDebugEvent;

    [Header("Debug: Maintenance")]
    public KeyCode maintenanceTriggerKey = KeyCode.M;
    public EventDefinitionSO maintenanceDebugEvent;

    [Header("Debug: Contamination")]
    public KeyCode contaminationTriggerKey = KeyCode.C;
    public EventDefinitionSO contaminationDebugEvent;

    [Header("Debug: Safety")]
    public KeyCode safetyTriggerKey = KeyCode.F;
    [Tooltip("Optional: drag your SafetyEventController (BatteryFireSafetyEvent) here to trigger it with the hotkey.")]
    public BatteryFireSafetyEvent batteryFireSafetyEvent;

    private Coroutine activeEventRoutine;

    // Auto scheduling state
    private float _nextLogisticsTime = -1f;
    private float _nextMaintenanceTime = -1f;
    private float _nextContaminationTime = -1f;

    private bool _logisticsFired;
    private bool _maintenanceFired;
    private bool _contaminationFired;


    private float _lastAutoEventTime = -999f;

    private float _startTime;
    private float _guaranteeDeadline;

    private void Start()
    {
        _startTime = Time.time;
        _guaranteeDeadline = _startTime + Mathf.Max(0f, guaranteeWindowSeconds);

        if (enableAutoEvents)
        {
            ScheduleInitialAutoTimes();
        }
    }

    private void ScheduleInitialAutoTimes()
    {
        _logisticsFired = false;
        _maintenanceFired = false;
        _contaminationFired = false;

        _lastAutoEventTime = -999f;

        _nextLogisticsTime = PickTimeInWindow(logisticsAutoWindow);
        _nextMaintenanceTime = PickTimeInWindow(maintenanceAutoWindow);
        _nextContaminationTime = PickTimeInWindow(contaminationAutoWindow);

        // Ensure we never schedule beyond the guarantee window.
        _nextLogisticsTime = Mathf.Min(_nextLogisticsTime, _guaranteeDeadline);
        _nextMaintenanceTime = Mathf.Min(_nextMaintenanceTime, _guaranteeDeadline);
        _nextContaminationTime = Mathf.Min(_nextContaminationTime, _guaranteeDeadline);

        // Enforce spacing so events don't stack.
        EnforceSpacing(ref _nextMaintenanceTime, _nextLogisticsTime);
        EnforceSpacing(ref _nextContaminationTime, _nextMaintenanceTime);
    }

    private float PickTimeInWindow(Vector2 window)
    {
        float min = Mathf.Min(window.x, window.y);
        float max = Mathf.Max(window.x, window.y);
        return _startTime + UnityEngine.Random.Range(min, max);
    }

    private void EnforceSpacing(ref float timeToAdjust, float priorTime)
    {
        float minGap = Mathf.Max(0f, autoCooldownSeconds);
        if (timeToAdjust < priorTime + minGap)
            timeToAdjust = priorTime + minGap;
    }

    private bool CanAutoTrigger()
    {
        if (!enableAutoEvents) return false;
        if (gameStateManager != null && gameStateManager.IsGameOver) return false;

        // Don't stack events on top of each other.
        if (activeEventRoutine != null) return false;
        if (popupUI != null && popupUI.root != null && popupUI.root.activeSelf) return false;

        // Cooldown between auto events.
        if (Time.time - _lastAutoEventTime < autoCooldownSeconds) return false;

        return true;
    }

    private void Update()
    {
        if (gameStateManager != null && gameStateManager.IsGameOver) return;

        // NOTE: In the Editor, hotkeys only register if the Game view has focus.

        if (enableDebugHotkeys)
        {
        bool LogisticsPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && logisticsTriggerKey == KeyCode.L)
                return Keyboard.current.lKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(logisticsTriggerKey);
        }

        bool MaintenancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && maintenanceTriggerKey == KeyCode.M)
                return Keyboard.current.mKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(maintenanceTriggerKey);
        }

        bool ContaminationPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && contaminationTriggerKey == KeyCode.C)
                return Keyboard.current.cKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(contaminationTriggerKey);
        }

        bool SafetyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool newInput = false;
            if (Keyboard.current != null)
            {
                // We only support F for the New Input System path in V1.
                if (safetyTriggerKey == KeyCode.F)
                    newInput = Keyboard.current.fKey.wasPressedThisFrame;
            }

            // Legacy fallback (useful when Active Input Handling is set to Both)
            bool legacy = Input.GetKeyDown(safetyTriggerKey);
            return newInput || legacy;
#else
            return Input.GetKeyDown(safetyTriggerKey);
#endif
        }

        if (logisticsDebugEvent != null && LogisticsPressed())
        {
            Debug.LogWarning($"[EventManager] Logistics hotkey pressed. eventId={logisticsDebugEvent.eventId} type={logisticsDebugEvent.eventType}");
            TriggerEvent(logisticsDebugEvent);
        }

        if (maintenanceDebugEvent != null && MaintenancePressed())
        {
            Debug.LogWarning($"[EventManager] Maintenance hotkey pressed. eventId={maintenanceDebugEvent.eventId} type={maintenanceDebugEvent.eventType}");
            TriggerEvent(maintenanceDebugEvent);
        }

        if (contaminationDebugEvent != null && ContaminationPressed())
        {
            Debug.LogWarning($"[EventManager] Contamination hotkey pressed. eventId={contaminationDebugEvent.eventId} type={contaminationDebugEvent.eventType}");
            TriggerEvent(contaminationDebugEvent);
        }

        if (SafetyPressed())
        {
            Debug.LogWarning($"[EventManager] Safety hotkey '{safetyTriggerKey}' detected. batteryFireSafetyEvent={(batteryFireSafetyEvent != null ? "SET" : "NULL")}");
            if (batteryFireSafetyEvent != null)
                batteryFireSafetyEvent.DebugForceFire();
        }
        }

        // Auto events (V1): trigger each configured event once within the guarantee window.
        if (enableAutoEvents)
        {
            // If we've already fired all three, do nothing.
            if (!_logisticsFired || !_maintenanceFired || !_contaminationFired)
            {
                // If we can't trigger right now (popup already open / cooldown), we simply wait.
                if (CanAutoTrigger())
                {
                    // Ensure we still hit the guarantee window even if the player had popups open a lot.
                    bool pastGuarantee = Time.time >= _guaranteeDeadline;

                    if (!_logisticsFired && logisticsDebugEvent != null && (Time.time >= _nextLogisticsTime || pastGuarantee))
                    {
                        Debug.LogWarning("[EventManager] Auto-triggering Logistics event.");
                        TriggerEvent(logisticsDebugEvent);
                        _logisticsFired = true;
                        _lastAutoEventTime = Time.time;
                    }
                    else if (!_maintenanceFired && maintenanceDebugEvent != null && (Time.time >= _nextMaintenanceTime || pastGuarantee))
                    {
                        Debug.LogWarning("[EventManager] Auto-triggering Maintenance event.");
                        TriggerEvent(maintenanceDebugEvent);
                        _maintenanceFired = true;
                        _lastAutoEventTime = Time.time;
                    }
                    else if (!_contaminationFired && contaminationDebugEvent != null && (Time.time >= _nextContaminationTime || pastGuarantee))
                    {
                        Debug.LogWarning("[EventManager] Auto-triggering Contamination event.");
                        TriggerEvent(contaminationDebugEvent);
                        _contaminationFired = true;
                        _lastAutoEventTime = Time.time;
                    }
                }
            }
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

            case EventType.ContaminationSpike:
                Debug.Log("[EventManager] Starting coroutine: HandleContaminationSpike");
                activeEventRoutine = StartCoroutine(HandleContaminationSpike(def));
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

        // Skip if already broken
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

    private IEnumerator HandleContaminationSpike(EventDefinitionSO def)
    {
        if (PackingArea.Instance == null)
        {
            Debug.LogError("ContaminationSpike: PackingArea.Instance is null.");
            yield break;
        }

        // V1: fixed severity
        const float contaminationMult = 0.8f; // -20% value

        // Apply contamination immediately so the HUD updates instantly.
        PackingArea.Instance.SetContaminationMultiplier(contaminationMult);

        bool decisionMade = false;
        bool paidToBypass = false;

        void PayToBypass()
        {
            if (!def.canPayToBypass) return;
            if (MoneyManager.Instance == null) return;

            if (!MoneyManager.Instance.TrySpend(def.bypassCost, PayType.Purchase, def.bypassLabel))
                return;

            paidToBypass = true;
            decisionMade = true;
            popupUI?.Hide();
        }

        void WaitItOut()
        {
            decisionMade = true;
            popupUI?.Hide();
        }

        popupUI?.Show(
            def.eventName,
            def.playerPrompt,
            def.canPayToBypass ? $"{def.actionText} (£{def.bypassCost})" : "OK",
            PayToBypass,
            "Wait",
            WaitItOut
        );

        while (!decisionMade)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                popupUI?.Hide();
                PackingArea.Instance.ClearContamination();
                activeEventRoutine = null;
                yield break;
            }
            yield return null;
        }

        // If paid, clear immediately and end.
        if (paidToBypass)
        {
            PackingArea.Instance.ClearContamination();
            activeEventRoutine = null;
            yield break;
        }

        // Otherwise, keep contamination active for the configured duration.
        float endTime = Time.time + Mathf.Max(0f, def.timerSeconds);
        while (Time.time < endTime)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                PackingArea.Instance.ClearContamination();
                activeEventRoutine = null;
                yield break;
            }

            // We can reuse the existing timer label on the popup if it's still visible.
            if (popupUI != null && popupUI.root.activeSelf)
                popupUI.SetTimerVisible(true, endTime - Time.time);

            yield return null;
        }

        PackingArea.Instance.ClearContamination();
        activeEventRoutine = null;
    }
}