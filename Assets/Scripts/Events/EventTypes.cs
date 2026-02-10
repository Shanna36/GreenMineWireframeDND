// Assets/Scripts/Events/EventTypes.cs
using System;

public enum EventType
{
    LogisticsDelay,
    MaintenanceDegrade,
    ContaminationSpike,
    // Later: TimedSafety, HardStopRepair
}

public enum TargetType
{
    Shipping,
    PickingLine,
    MachineSlot
}