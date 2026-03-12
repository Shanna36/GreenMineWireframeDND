using UnityEngine;

public class FlowItem : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private FlowPath currentPath;
    [SerializeField] private int waypointIndex = 0;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float arriveDistance = 0.05f;

    private void Start()
    {
        if (currentPath == null || currentPath.WaypointCount == 0)
        {
            Debug.LogWarning($"[FlowItem] No valid path assigned on '{name}'.");
            enabled = false;
            return;
        }

        transform.position = currentPath.GetWaypointPosition(waypointIndex);
    }

    private void Update()
    {
        if (currentPath == null || currentPath.WaypointCount == 0)
            return;

        if (waypointIndex >= currentPath.WaypointCount)
        {
            HandlePathEnd();
            return;
        }

        Vector3 targetPosition = currentPath.GetWaypointPosition(waypointIndex);

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        float distance = Vector3.Distance(transform.position, targetPosition);
        if (distance <= arriveDistance)
        {
            waypointIndex++;

            if (waypointIndex >= currentPath.WaypointCount)
            {
                HandlePathEnd();
            }
        }
    }

    private void HandlePathEnd()
    {
        if (currentPath != null && currentPath.NextPath != null)
        {
            SetPath(currentPath.NextPath);
        }
        else
        {
            Debug.Log($"[FlowItem] '{name}' reached end of path.");
            enabled = false;
        }
    }

    public void SetPath(FlowPath path)
    {
        currentPath = path;
        waypointIndex = 0;

        if (currentPath != null && currentPath.WaypointCount > 0)
        {
            transform.position = currentPath.GetWaypointPosition(0);
            enabled = true;
        }
        else
        {
            enabled = false;
        }
    }
}