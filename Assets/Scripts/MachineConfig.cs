using UnityEngine;

/// <summary>
/// Data container describing a single machine option
/// (e.g. Optical Sorter – Basic, Advanced, Premium)
/// </summary>
[CreateAssetMenu(
    fileName = "MachineConfig",
    menuName = "GreenMine/Machine Config"
)]
public class MachineConfig : ScriptableObject
{
    // ------------------------
    // Identity
    // ------------------------

    [Header("Identity")]
    public string machineName;   // Display name in UI
    public MachineType machineType;
    public int tier;              // 1 = Basic, 2 = Advanced, 3 = Premium

    // ------------------------
    // Economics
    // ------------------------

    [Header("Economics")]
    public int cost;              // Purchase cost

    // ------------------------
    // Performance
    // ------------------------

    [Header("Performance")]
    public float throughputTonnesPerHour;
    public float contaminationPercent;

    // Convenience alias used by systems that expect throughput in tonnes/hour.
    // Keeps older/newer naming consistent across scripts.
    public float ThroughputTPH => throughputTonnesPerHour;

    // ------------------------
    // Visuals
    // ------------------------

    [Header("Visuals")]
    public GameObject machinePrefab;
}

/// moved MachineType enum to its own file MachineType.cs so both MachineSlot and MachineConfig can use it