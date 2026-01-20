using System;
using System.Collections.Generic;
using UnityEngine;

public class ThroughputAggregator : MonoBehaviour
{
    [Header("Game time model")]
    [Tooltip("How many simulated operating hours pass during one in-game day (e.g., 16 for two shifts).")]
    public float simulatedHoursPerDay = 16f;

    [Tooltip("Real-time seconds that represent one in-game day (e.g., 120 seconds = 2 minutes).")]
    public float secondsPerGameDay = 120f;

    [Header("Line machines (in flow order)")]
    [Tooltip("Drag your machine slots here in the order material flows through them.")]
    public List<MachineSlot> machineSlots = new List<MachineSlot>();

    public float EffectiveThroughputTPH { get; private set; } = 0f;          // tonnes/hour
    public float TonnesPerSimDay { get; private set; } = 0f;                 // tonnes per in-game day (simulated hours)
    public float TonnesPerRealSecond { get; private set; } = 0f;             // tonnes per real-time second

    public event Action<float> OnThroughputChanged; // passes new EffectiveThroughputTPH

    [Header("Debug")]
    [Tooltip("If enabled, logs effective throughput and derived rates when Recalculate() runs.")]
    public bool debugLog = true;

    [Tooltip("How often (in seconds) to refresh the throughput calculation + debug log while playing.")]
    public float debugLogInterval = 1f;

    private float _nextDebugTime = 0f;

    private void Awake()
    {
        Recalculate();
    }

    private void Update()
    {
        if (!debugLog) return;
        if (!Application.isPlaying) return;

        // Recalculate periodically so the log reflects selections made after the scene starts.
        if (Time.time >= _nextDebugTime)
        {
            _nextDebugTime = Time.time + Mathf.Max(0.1f, debugLogInterval);
            Recalculate();
        }
    }

    /// <summary>
    /// Call this whenever a machine is installed/upgraded/removed or changes operational state.
    /// </summary>
    public void Recalculate()
    {
        float previous = EffectiveThroughputTPH;

        // If any required slot has no machine, you can either treat throughput as 0 (line can't run),
        // or ignore missing slots if your design allows it. For v1, "missing = 0" is simplest/clearest.
        float bottleneck = float.PositiveInfinity;

        foreach (var slot in machineSlots)
        {
            if (slot == null)
                continue;

            if (!slot.HasMachineInstalled)
            {
                bottleneck = 0f;
                break;
            }

            // Optional hook for maintenance/breakdown later:
            if (!slot.IsOperational)
            {
                bottleneck = 0f;
                break;
            }

            float tph = slot.CurrentThroughputTPH;

            // Defensive clamp
            if (tph < 0f) tph = 0f;

            if (tph < bottleneck)
                bottleneck = tph;
        }

        if (float.IsPositiveInfinity(bottleneck))
            bottleneck = 0f;

        EffectiveThroughputTPH = bottleneck;

        // Convert to tonnes per simulated day
        TonnesPerSimDay = EffectiveThroughputTPH * Mathf.Max(0f, simulatedHoursPerDay);

        // Convert to tonnes per real second (for smooth filling)
        // TonnesPerRealSecond = TonnesPerSimDay / secondsPerGameDay
        TonnesPerRealSecond = (secondsPerGameDay > 0f) ? (TonnesPerSimDay / secondsPerGameDay) : 0f;

        if (debugLog)
        {
            Debug.Log(
                $"[ThroughputAggregator] EffectiveThroughputTPH={EffectiveThroughputTPH:0.###} t/h | " +
                $"TonnesPerSimDay={TonnesPerSimDay:0.###} t/day (simHours={simulatedHoursPerDay:0.###}) | " +
                $"TonnesPerRealSecond={TonnesPerRealSecond:0.#####} t/s (secondsPerGameDay={secondsPerGameDay:0.###})"
            );
        }

        // Notify listeners if it changed meaningfully
        if (!Mathf.Approximately(previous, EffectiveThroughputTPH))
        {
            OnThroughputChanged?.Invoke(EffectiveThroughputTPH);
        }
    }
}