using UnityEngine;

public class FlowSpawner : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private FlowItemPool[] itemPools;
    [SerializeField] private FlowPath spawnPath;
    [SerializeField] private GameStateManager gameStateManager;

    [Header("Timing")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private bool spawnOnStart = true;

    private float spawnTimer;
    private bool hasSpawnedAfterGameStart;

    private void Start()
    {
        if (gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
        }

        spawnTimer = spawnInterval;
        hasSpawnedAfterGameStart = false;
    }

    private void Update()
    {
        if (itemPools == null || itemPools.Length == 0 || spawnPath == null)
            return;

        if (gameStateManager != null && !gameStateManager.HasGameStarted)
            return;

        if (spawnOnStart && !hasSpawnedAfterGameStart)
        {
            SpawnItem();
            hasSpawnedAfterGameStart = true;
            spawnTimer = spawnInterval;
            return;
        }

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