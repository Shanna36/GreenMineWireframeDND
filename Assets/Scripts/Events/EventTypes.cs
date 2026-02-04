// Assets/Scripts/Events/EventTypes.cs
using System;

public enum EventType
{
    LogisticsDelay,
    MaintenanceDegrade,
    // Later: TimedSafety, HardStopRepair, ContaminationSpike
}

public enum TargetType
{
    Shipping,
    PickingLine,
    MachineSlot
}