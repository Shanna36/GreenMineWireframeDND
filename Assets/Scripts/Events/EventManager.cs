using System.Collections;
using System.Collections.Generic;
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

    [Tooltip("Optional: If set, auto-trigger will randomly pick one of these Logistics events instead of always using logisticsDebugEvent.")]
    public EventDefinitionSO[] logisticsAutoPool;

    [Header("Debug: Maintenance")]
    public KeyCode maintenanceTriggerKey = KeyCode.M;
    public EventDefinitionSO maintenanceDebugEvent;

    [Tooltip("Optional: If set, auto-trigger will randomly pick one of these Maintenance events instead of always using maintenanceDebugEvent.")]
    public EventDefinitionSO[] maintenanceAutoPool;

    [Header("Debug: Contamination")]
    public KeyCode contaminationTriggerKey = KeyCode.C;
    public EventDefinitionSO contaminationDebugEvent;

    [Header("Debug: Safety")]
    public KeyCode safetyTriggerKey = KeyCode.F;
    [Tooltip("Optional: drag your SafetyEventController (BatteryFireSafetyEvent) here to trigger it with the hotkey.")]
    public BatteryFireSafetyEvent batteryFireSafetyEvent;

    private Coroutine activeEventRoutine;

    // Queued events to avoid stacking/overlap.
    private readonly Queue<EventDefinitionSO> _pendingEvents = new Queue<EventDefinitionSO>();

    // Tracks which owner string we used when acquiring EventLock for the currently running coroutine.
    private string _activeLockOwner = null;

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
        if (enableAutoEvents)
        {
            StartNewAutoCycle();
        }
        else
        {
            _startTime = Time.time;
            _guaranteeDeadline = _startTime + Mathf.Max(0f, guaranteeWindowSeconds);
        }
    }

    private void StartNewAutoCycle()
    {
        _startTime = Time.time;
        _guaranteeDeadline = _startTime + Mathf.Max(0f, guaranteeWindowSeconds);
        ScheduleInitialAutoTimes();

        Debug.LogWarning($"[EventManager] Starting new auto-event cycle. Next windows: L={_nextLogisticsTime:0.0}, M={_nextMaintenanceTime:0.0}, C={_nextContaminationTime:0.0}");
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

        // Also don't overlap with non-EventManager events (e.g., Safety fire) that use the shared EventLock.
        if (EventLock.IsLocked) return false;

        // Cooldown between auto events.
        if (Time.time - _lastAutoEventTime < autoCooldownSeconds) return false;

        return true;
    }

    private EventDefinitionSO PickFromPoolOrFallback(EventDefinitionSO[] pool, EventDefinitionSO fallback)
    {
        if (pool != null && pool.Length > 0)
        {
            // Filter nulls defensively.
            int safety = 0;
            while (safety < 10)
            {
                var choice = pool[UnityEngine.Random.Range(0, pool.Length)];
                if (choice != null) return choice;
                safety++;
            }
        }
        return fallback;
    }

    private void Update()
    {
        if (gameStateManager != null && gameStateManager.IsGameOver) return;

        // Do not allow any events until the factory has actually started running.
        // This prevents debug hotkeys and auto events from firing before all machines are selected.
        if (gameStateManager != null && !gameStateManager.HasGameStarted) return;

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

        // If we're idle and there are queued events, start the next one.
        if (activeEventRoutine == null && (popupUI == null || popupUI.root == null || !popupUI.root.activeSelf) && !EventLock.IsLocked)
        {
            if (_pendingEvents.Count > 0)
            {
                var next = _pendingEvents.Dequeue();
                if (next != null)
                {
                    Debug.LogWarning($"[EventManager] Dequeuing event: {next.eventName} (id={next.eventId})");
                    TriggerEvent(next);
                }
            }
        }

        // Auto events (V1): trigger each configured event once per cycle, repeating indefinitely.
        if (enableAutoEvents)
        {
            // If we can't trigger right now (popup already open / cooldown), we simply wait.
            if (CanAutoTrigger())
            {
                // Ensure we still hit the guarantee window even if the player had popups open a lot.
                bool pastGuarantee = Time.time >= _guaranteeDeadline;

                if (!_logisticsFired && (Time.time >= _nextLogisticsTime || pastGuarantee))
                {
                    var chosen = PickFromPoolOrFallback(logisticsAutoPool, logisticsDebugEvent);
                    if (chosen != null)
                    {
                        Debug.LogWarning($"[EventManager] Auto-triggering Logistics event: {chosen.eventName} (id={chosen.eventId}).");
                        TriggerEvent(chosen);
                        _logisticsFired = true;
                        _lastAutoEventTime = Time.time;
                    }
                }
                else if (!_maintenanceFired && (Time.time >= _nextMaintenanceTime || pastGuarantee))
                {
                    var chosen = PickFromPoolOrFallback(maintenanceAutoPool, maintenanceDebugEvent);
                    if (chosen != null)
                    {
                        Debug.LogWarning($"[EventManager] Auto-triggering Maintenance event: {chosen.eventName} (id={chosen.eventId}).");
                        TriggerEvent(chosen);
                        _maintenanceFired = true;
                        _lastAutoEventTime = Time.time;
                    }
                }
                else if (!_contaminationFired && contaminationDebugEvent != null && (Time.time >= _nextContaminationTime || pastGuarantee))
                {
                    Debug.LogWarning("[EventManager] Auto-triggering Contamination event.");
                    TriggerEvent(contaminationDebugEvent);
                    _contaminationFired = true;
                    _lastAutoEventTime = Time.time;
                }
            }

            // When all three have fired, start a new cycle with fresh random times.
            if (_logisticsFired && _maintenanceFired && _contaminationFired)
            {
                StartNewAutoCycle();
            }
        }
    }

    public void TriggerEvent(EventDefinitionSO def)
    {
        if (def == null) return;
        if (gameStateManager != null && gameStateManager.IsGameOver) return;
        Debug.LogWarning($"[EventManager] TriggerEvent: {def.eventName} | id={def.eventId} | type={def.eventType}");
        Debug.LogWarning($"[EventManager] Routing: eventType={def.eventType} targetType={def.targetType} targetId='{def.targetId}' activeEventRoutine={(activeEventRoutine != null ? "YES" : "NO")}");

        // Only allow one active event at a time in V1.
        // If something is already running (or another system holds the lock), queue this event.
        if (activeEventRoutine != null || (popupUI != null && popupUI.root != null && popupUI.root.activeSelf) || EventLock.IsLocked)
        {
            // Prevent runaway queue growth by ignoring duplicates of the same asset reference.
            if (!_pendingEvents.Contains(def))
            {
                _pendingEvents.Enqueue(def);
                Debug.LogWarning($"[EventManager] Event busy. Queued: {def.eventName} (id={def.eventId}). QueueCount={_pendingEvents.Count}");
            }
            else
            {
                Debug.LogWarning($"[EventManager] Event busy. Duplicate not queued: {def.eventName} (id={def.eventId}).");
            }
            return;
        }

        // Acquire the shared lock so other event systems (e.g., Safety) cannot overlap.
        _activeLockOwner = $"EventManager:{def.eventType}:{def.eventId}";
        if (!EventLock.TryAcquire(_activeLockOwner))
        {
            // Should be rare because we checked IsLocked above, but keep it safe.
            _pendingEvents.Enqueue(def);
            Debug.LogWarning($"[EventManager] Failed to acquire EventLock. Queued: {def.eventName} (id={def.eventId}).");
            _activeLockOwner = null;
            return;
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
                EndActiveEvent();
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
            def.canPayToBypass ? $"{def.actionText} ({def.bypassCost})" : "OK",
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
                EndActiveEvent();
                yield break;
            }
            if (popupUI != null && popupUI.root.activeSelf)
            {
                popupUI.SetTimerVisible(true, endTime - Time.time);
            }
            yield return null;
        }

        EndActiveEvent();
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

        // Find all matching slots (there may be more than one in-scene).
        MachineSlot targetSlot = null;
        var allSlots = FindObjectsByType<MachineSlot>(FindObjectsSortMode.None);

        // 1) Prefer a matching slot that has a machine installed (so warnings are visible).
        foreach (var slot in allSlots)
        {
            if (slot == null) continue;
            if (slot.machineType != machineType) continue;

            if (slot.HasMachineInstalled)
            {
                targetSlot = slot;
                break;
            }
        }

        // 2) Fallback: first matching slot.
        if (targetSlot == null)
        {
            foreach (var slot in allSlots)
            {
                if (slot == null) continue;
                if (slot.machineType != machineType) continue;
                targetSlot = slot;
                break;
            }
        }

        if (targetSlot != null)
        {
            Debug.LogWarning($"[EventManager] Maintenance target selected: '{targetSlot.name}' (machineType={machineType}, hasMachine={targetSlot.HasMachineInstalled}).");
        }

        if (targetSlot == null)
        {
            Debug.LogError($"MaintenanceDegrade: no MachineSlot found for {machineType}");
            EndActiveEvent();
            yield break;
        }

        // Debug: confirm the warning visual is actually assigned on this slot (via reflection so we don't couple to field names).
        try
        {
            var t = targetSlot.GetType();
            var f = t.GetField("warningEffect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null)
            {
                var go = f.GetValue(targetSlot) as GameObject;
                Debug.LogWarning($"[EventManager] Slot '{targetSlot.name}' warningEffect={(go != null ? go.name : "NULL")}");
            }
        }
        catch { /* ignore */ }

        // Skip if already broken
        if (!targetSlot.IsOperational)
        {
            targetSlot.SetWarningState(false);
            Debug.LogWarning($"[EventManager] Machine {machineType} is already broken. Skipping maintenance event.");
            EndActiveEvent();
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
            def.canPayToBypass ? $"{def.actionText} ({def.bypassCost})" : "OK",
            PayNow,
            "Delay",
            Delay
        );

        while (!decisionMade)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                popupUI?.Hide();
                EndActiveEvent();
                yield break;
            }
            yield return null;
        }

        if (paidMaintenance)
        {
            EndActiveEvent();
            yield break;
        }

        // Escalation timer
        float endTime = Time.time + Mathf.Max(0f, def.timerSeconds);
        while (Time.time < endTime)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                popupUI?.Hide();
                EndActiveEvent();
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
            $"Repair ({repairCost})",
            Repair,
            "Ignore",
            Ignore
        );

        while (!repaired && !ignored)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                popupUI?.Hide();
                EndActiveEvent();
                yield break;
            }
            yield return null;
        }

        EndActiveEvent();
    }

    private IEnumerator HandleContaminationSpike(EventDefinitionSO def)
    {
        if (PackingArea.Instance == null)
        {
            Debug.LogError("ContaminationSpike: PackingArea.Instance is null.");
            EndActiveEvent();
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
            def.canPayToBypass ? $"{def.actionText} ({def.bypassCost})" : "OK",
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
                EndActiveEvent();
                yield break;
            }
            yield return null;
        }

        // If paid, clear immediately and end.
        if (paidToBypass)
        {
            PackingArea.Instance.ClearContamination();
            EndActiveEvent();
            yield break;
        }

        // Otherwise, keep contamination active for the configured duration.
        float endTime = Time.time + Mathf.Max(0f, def.timerSeconds);
        while (Time.time < endTime)
        {
            if (gameStateManager != null && gameStateManager.IsGameOver)
            {
                PackingArea.Instance.ClearContamination();
                EndActiveEvent();
                yield break;
            }

            // We can reuse the existing timer label on the popup if it's still visible.
            if (popupUI != null && popupUI.root.activeSelf)
                popupUI.SetTimerVisible(true, endTime - Time.time);

            yield return null;
        }

        PackingArea.Instance.ClearContamination();
        EndActiveEvent();
    }

    private void EndActiveEvent()
    {
        activeEventRoutine = null;
        if (!string.IsNullOrEmpty(_activeLockOwner))
        {
            EventLock.Release(_activeLockOwner);
            _activeLockOwner = null;
        }
    }
}