using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Reflection;



[System.Serializable]
public class MachineOption
{
    public string displayName;   // Shown in UI, optional
    public MachineConfig config;   // changed to scriptable object reference
    
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

    // Fired whenever the player selects/changes the machine option for this slot.
    public event Action<MachineSlot> OnSelectionChanged;

    // --- Throughput Aggregator helpers ---

    // True if a valid MachineConfig is currently selected for this slot.
    public bool HasMachineInstalled => CurrentConfig != null;

    // v1: always operational. Later you can wire breakdowns/maintenance into this.
    public bool IsOperational => true;

    // The currently selected MachineConfig (or null if none selected).
    public MachineConfig CurrentConfig
    {
        get
        {
            if (options == null || options.Length == 0) return null;
            if (currentIndex < 0 || currentIndex >= options.Length) return null;
            return options[currentIndex]?.config;
        }
    }

    // Current throughput in tonnes per hour for the selected option.
    public float CurrentThroughputTPH
    {
        get
        {
            var cfg = CurrentConfig;
            if (cfg == null) return 0f;

            // Try common property/field names without forcing a specific MachineConfig implementation.
            // This prevents compile errors if the field name differs.
            Type t = cfg.GetType();

            // Properties (PascalCase)
            PropertyInfo p;
            p = t.GetProperty("ThroughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(cfg);

            p = t.GetProperty("throughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(cfg);

            // Fields (camelCase)
            FieldInfo f;
            f = t.GetField("throughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(cfg);

            f = t.GetField("throughputTph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(cfg);

            f = t.GetField("throughput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(cfg);

            // If we can't find a matching member, default to 0.
            return 0f;
        }
    }

    private void Start()
    {
        // Ensure a consistent initial state even if the menu is left enabled in the editor.
        SetMenuVisible(false);
    }

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

        if (option.config == null)
        {
            Debug.LogWarning($"[MachineSlot] Option {index} on {name} has no MachineConfig assigned.");
            return;
        }

        if (option.config.machinePrefab == null)
        {
            Debug.LogWarning($"[MachineSlot] MachineConfig '{option.config.name}' has no prefab assigned.");
            return;
        }

        Transform parent = spawnPoint != null ? spawnPoint : transform;

        currentMachineInstance = Instantiate(
            option.config.machinePrefab,
            parent.position,
            parent.rotation,
            parent
        );

        currentIndex = index;

        // Notify any listeners (e.g., a ThroughputAggregator) that this slot's selection changed.
        OnSelectionChanged?.Invoke(this);

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
        // If clicking UI (e.g., the popup buttons), don't also toggle the slot menu.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

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
