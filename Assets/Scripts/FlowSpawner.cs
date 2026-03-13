using UnityEngine;

public class FlowSpawner : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private FlowItemPool[] itemPools;
    [SerializeField] private FlowPath spawnPath;

    [Header("Timing")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private bool spawnOnStart = true;

    private float spawnTimer;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnItem();
        }

        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        if (itemPools == null || itemPools.Length == 0 || spawnPath == null)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            SpawnItem();
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnItem()
    {
        if (itemPools == null || itemPools.Length == 0)
            return;

        int randomIndex = Random.Range(0, itemPools.Length);
        FlowItemPool selectedPool = itemPools[randomIndex];

        if (selectedPool == null)
            return;

        FlowItem newItem = selectedPool.GetItem();
        newItem.BeginFlow(spawnPath);
    }
}