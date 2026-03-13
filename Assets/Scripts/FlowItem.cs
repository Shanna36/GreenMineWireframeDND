using UnityEngine;

public class FlowItem : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private FlowPath currentPath;
    [SerializeField] private int waypointIndex = 0;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float arriveDistance = 0.05f;

    [Header("Lifetime")]
    [SerializeField] private float lifespanSeconds = -1f;

    private float lifeTimer;

    [Header("Spawn Variation")]
    [SerializeField] private float randomYRotationMin = -15f;
    [SerializeField] private float randomYRotationMax = 15f;
    [SerializeField] private float randomXOffsetMin = -0.15f;
    [SerializeField] private float randomXOffsetMax = 0.15f;

    private float currentXOffset;

    private FlowItemPool owningPool;

    private void Update()
    {
        if (lifespanSeconds > 0f)
        {
            lifeTimer -= Time.deltaTime;

            if (lifeTimer <= 0f)
            {
                ReturnToPool();
                return;
            }
        }

        if (currentPath == null || currentPath.WaypointCount == 0)
            return;

        if (waypointIndex >= currentPath.WaypointCount)
        {
            HandlePathEnd();
            return;
        }

        Vector3 targetPosition = currentPath.GetWaypointPosition(waypointIndex);
        targetPosition.x += currentXOffset;

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
            ReturnToPool();
        }
    }

    public void SetPath(FlowPath path)
    {
        currentPath = path;
        waypointIndex = 0;

        if (currentPath != null && currentPath.WaypointCount > 0)
        {
            Vector3 startPosition = currentPath.GetWaypointPosition(0);
            startPosition.x += currentXOffset;
            transform.position = startPosition;
            enabled = true;
        }
        else
        {
            ReturnToPool();
        }
    }

    public void BeginFlow(FlowPath path)
    {
        lifeTimer = lifespanSeconds;
        SetPath(path);
        ApplySpawnVariation();
    }

    private void ApplySpawnVariation()
    {
        float randomY = Random.Range(randomYRotationMin, randomYRotationMax);
        //transform.rotation = Quaternion.Euler(0f, randomY, 0f);  causing flip issue w/ paper prefab

        currentXOffset = Random.Range(randomXOffsetMin, randomXOffsetMax);

        Vector3 position = transform.position;
        position.x += currentXOffset;
        transform.position = position;
    }

    public void SetOwningPool(FlowItemPool pool)
    {
        owningPool = pool;
    }

    public void ReturnToPool()
    {
        currentPath = null;
        waypointIndex = 0;
        enabled = false;
        lifeTimer = 0f;
        currentXOffset = 0f;

        if (owningPool != null)
        {
            owningPool.ReturnItem(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}