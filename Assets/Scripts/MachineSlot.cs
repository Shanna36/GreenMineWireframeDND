using UnityEngine;



[System.Serializable]
public class MachineOption
{
    public string displayName;   // Shown in UI, optional
    public GameObject prefab;    // Machine model to spawn
}

public class MachineSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject hoverMenu; // Assign a popup panel or world-space canvas

    [Header("Slot Setup")]
    public MachineType machineType;          // What kind of machine belongs here (Sorting, Baler, etc.)
    public Transform spawnPoint;             // Where the chosen machine will appear

    [Header("Available Options (3 per slot)")]
    public MachineOption[] options;          // Assign 3 prefabs in the Inspector

    private GameObject currentMachineInstance;
    private int currentIndex = -1;           // For quick cycling in tests

    /// <summary>
    /// Show or hide the selection menu and manage cursor.
    /// </summary>
    private void SetMenuVisible(bool isVisible)
    {
        if (hoverMenu == null)
            return;

        hoverMenu.SetActive(isVisible);

        // For now, keep the cursor visible and unlocked either way while testing.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// Spawn one of the machine options into this slot.
    /// This can be called from UI buttons with the appropriate index.
    /// </summary>
    public void SelectOption(int index)
    {
        Debug.Log($"[MachineSlot] SelectOption({index}) on {name}");

        if (options == null || options.Length == 0)
        {
            Debug.LogWarning($"[MachineSlot] No options assigned on {name}.");
            return;
        }

        if (index < 0 || index >= options.Length)
        {
            Debug.LogWarning($"[MachineSlot] Index {index} is out of range on {name}.");
            return;
        }

        // Destroy any previous machine
        if (currentMachineInstance != null)
        {
            Destroy(currentMachineInstance);
            currentMachineInstance = null;
        }

        MachineOption option = options[index];

        if (option.prefab == null)
        {
            Debug.LogWarning($"[MachineSlot] Option {index} on {name} has no prefab assigned.");
            return;
        }

        Transform parent = spawnPoint != null ? spawnPoint : transform;

        currentMachineInstance = Instantiate(
            option.prefab,
            parent.position,
            parent.rotation,
            parent
        );

        currentIndex = index;

        // If the prefab has a Machine component, we can initialise it here later.
        // var machine = currentMachineInstance.GetComponent<Machine>();
        // if (machine != null) machine.Initialise(machineType, this);
    }

    public void SelectBasic()
    {
        Debug.Log($"[MachineSlot] SelectBasic called on {name}");
        SelectOption(0);
        SetMenuVisible(false);
    }

    public void SelectMedium()
    {
        Debug.Log($"[MachineSlot] SelectMedium called on {name}");
        SelectOption(1);
        SetMenuVisible(false);
    }

    public void SelectPremium()
    {
        Debug.Log($"[MachineSlot] SelectPremium called on {name}");
        SelectOption(2);
        SetMenuVisible(false);
    }

    /// <summary>
    /// Clicking the slot toggles the selection menu on and off.
    /// </summary>
    private void OnMouseDown()
    {
        if (hoverMenu == null)
            return;

        bool newState = !hoverMenu.activeSelf;
        SetMenuVisible(newState);
        Debug.Log($"[MachineSlot] OnMouseDown fired on: {name}, menu state: {newState}");
    }

    private void OnMouseEnter()
    {
        Debug.Log($"[MachineSlot] OnMouseEnter fired on: {name}");
        // Hover no longer controls menu visibility.
    }

    private void OnMouseExit()
    {
        Debug.Log($"[MachineSlot] OnMouseExit fired on: {name}");
        // Hover no longer controls menu visibility.
    }
}
