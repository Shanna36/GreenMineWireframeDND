using System.Collections.Generic;
using UnityEngine;

public class FlowPath : MonoBehaviour
{
    [Header("Waypoints in travel order")]
    [Tooltip("Drag waypoint transforms here in the order items should follow them.")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();

    [Header("Path chaining")]
    [Tooltip("Optional next path to continue onto when this one ends.")]
    [SerializeField] private FlowPath nextPath;

    public int WaypointCount => waypoints.Count;
    public FlowPath NextPath => nextPath;

    public Transform GetWaypoint(int index)
    {
        if (index < 0 || index >= waypoints.Count)
        {
            Debug.LogWarning($"[FlowPath] Waypoint index {index} is out of range on '{name}'.");
            return null;
        }

        return waypoints[index];
    }

    public Vector3 GetWaypointPosition(int index)
    {
        Transform wp = GetWaypoint(index);
        return wp != null ? wp.position : transform.position;
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count == 0)
            return;

        Gizmos.color = Color.green;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null)
                continue;

            Gizmos.DrawSphere(waypoints[i].position, 0.12f);

            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }
}