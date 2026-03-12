using UnityEngine;

public class FlowSpawner : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private FlowItem[] flowItemPrefabs;
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
        if (flowItemPrefabs == null || flowItemPrefabs.Length == 0 || spawnPath == null)
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
        if (flowItemPrefabs == null || flowItemPrefabs.Length == 0)
            return;

        int randomIndex = Random.Range(0, flowItemPrefabs.Length);
        FlowItem selectedPrefab = flowItemPrefabs[randomIndex];

        if (selectedPrefab == null)
            return;

        FlowItem newItem = Instantiate(selectedPrefab, transform.position, Quaternion.identity);
        newItem.SetPath(spawnPath);
    }
}