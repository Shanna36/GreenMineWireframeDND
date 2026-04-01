using System.Collections.Generic;
using UnityEngine;

public class FlowItemPool : MonoBehaviour
{
    [Header("Pool Setup")]
    [SerializeField] private FlowItem prefab;
    [SerializeField] private int initialSize = 10;
    [SerializeField] private Transform poolParent;

    private readonly Queue<FlowItem> availableItems = new Queue<FlowItem>();

    private void Awake()
    {
        if (prefab == null)
        {
            Debug.LogError($"[FlowItemPool] No prefab assigned on '{name}'.");
            return;
        }

        if (poolParent == null)
        {
            poolParent = transform;
        }

        Prewarm();
    }

    private void Prewarm()
    {
        for (int i = 0; i < initialSize; i++)
        {
            FlowItem item = CreateNewItem();
            ReturnItem(item);
        }
    }

    private FlowItem CreateNewItem()
    {
        FlowItem item = Instantiate(prefab, poolParent);
        item.SetOwningPool(this);
        item.gameObject.SetActive(false);
        return item;
    }

    public FlowItem GetItem()
    {
        FlowItem item;

        if (availableItems.Count > 0)
        {
            item = availableItems.Dequeue();
        }
        else
        {
            item = CreateNewItem();
        }

        item.gameObject.SetActive(true);
        return item;
    }

    public void ReturnItem(FlowItem item)
    {
        if (item == null)
            return;

        item.gameObject.SetActive(false);
        availableItems.Enqueue(item);
    }
}