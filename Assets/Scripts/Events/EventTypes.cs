// Assets/Scripts/Events/EventTypes.cs
using System;

public enum EventType
{
    LogisticsDelay,
    // Later: TimedSafety, MaintenanceDegrade, HardStopRepair, ContaminationSpike
}

public enum TargetType
{
    Shipping,
    PickingLine,
    MachineSlot
}