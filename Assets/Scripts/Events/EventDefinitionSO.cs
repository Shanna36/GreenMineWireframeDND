// Assets/Scripts/Events/EventDefinitionSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "GreenMine/Events/Event Definition", fileName = "Event_")]
public class EventDefinitionSO : ScriptableObject
{
    [Header("ID + UI")]
    public string eventId = "E00";
    public string eventName = "Logistics Event";
    [TextArea(2, 5)] public string playerPrompt = "A logistics issue occurred.";
    public string actionText = "Hire Replacement Truck";

    [Header("Routing")]
    public EventType eventType = EventType.LogisticsDelay;
    public TargetType targetType = TargetType.Shipping;

    [Tooltip("For V1, use 'Default' for Shipping/PickingLine. For machines, you'd use e.g. 'ECS'.")]
    public string targetId = "Default";

    [Header("Timing")]
    public bool hasTimer = true;
    public float timerSeconds = 30f;

    [Header("V1 Options")]
    public bool canPayToBypass = true;
    public int bypassCost = 500;

    [Tooltip("Optional label for spending log/UI.")]
    public string bypassLabel = "Hire Truck";
}