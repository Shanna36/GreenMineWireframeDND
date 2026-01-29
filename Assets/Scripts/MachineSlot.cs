using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Reflection;

[System.Serializable]
public class MachineOption
{
    public string displayName;   // Shown in UI, optional
    public MachineConfig config; // ScriptableObject reference
}

public class MachineSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject hoverMenu; // Assign a popup panel or world-space canvas

    [Header("Slot Setup")]
    public MachineType machineType;          // What kind of machine belongs here (Sorting, Baler, etc.)
    public Transform spawnPoint;             // Where the chosen machine will appear

    [Header("Available Options (3 per slot)")]
    public MachineOption[] options;          // Assign 3 options in the Inspector

    private GameObject currentMachineInstance;
    private int currentIndex = -1;           // Selected option index

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
    // Uses reflection as a safety net so you don't have to perfectly align field names during refactors.
    public float CurrentThroughputTPH
    {
        get
        {
            var cfg = CurrentConfig;
            if (cfg == null) return 0f;

            Type t = cfg.GetType();

            // Prefer the standardized property we added to MachineConfig.
            PropertyInfo p = t.GetProperty("ThroughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(cfg);

            // Fallbacks (in case you rename things later)
            p = t.GetProperty("throughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(cfg);

            FieldInfo f = t.GetField("throughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(cfg);

            f = t.GetField("throughputTph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(cfg);

            f = t.GetField("throughputTonnesPerHour", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(cfg);

            f = t.GetField("throughput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(cfg);

            return 0f;
        }
    }

    // --- Purchasing helpers ---

    // Reads a cost value from MachineConfig using reflection so field/property names can evolve safely.
    private int GetCost(MachineConfig cfg)
    {
        if (cfg == null) return 0;

        Type t = cfg.GetType();

        // Property: Cost
        PropertyInfo p = t.GetProperty("Cost", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(cfg);

        // Property: cost
        p = t.GetProperty("cost", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(cfg);

        // Field: cost
        FieldInfo f = t.GetField("cost", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(cfg);

        // Field: Cost
        f = t.GetField("Cost", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(cfg);

        return 0;
    }

    private string GetMachineLabel(MachineOption option)
    {
        if (option == null) return "Machine";
        if (!string.IsNullOrWhiteSpace(option.displayName)) return option.displayName;
        if (option.config != null) return option.config.name;
        return "Machine";
    }

    private void Start()
    {
        // Ensure a consistent initial state even if the menu is left enabled in the editor.
        SetMenuVisible(false);
    }

    private void SetMenuVisible(bool isVisible)
    {
        if (hoverMenu == null) return;

        hoverMenu.SetActive(isVisible);

        // While testing UI, keep cursor visible/unlocked.
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

        // --- Purchase check (non-disruptive) ---
        // We only charge when installing the first machine, or when upgrading to a more expensive option.
        // Downgrades do not refund in v1.
        int newCost = GetCost(option.config);
        int oldCost = GetCost(CurrentConfig);
        int upgradeCost = Mathf.Max(0, newCost - oldCost);

        // If selecting the same option again, do nothing.
        if (index == currentIndex)
        {
            Debug.Log($"[MachineSlot] Option {index} already selected on {name}. No action taken.");
            return;
        }

        // If there is a cost to pay, require MoneyManager.
        if (upgradeCost > 0)
        {
            if (MoneyManager.Instance == null)
            {
                Debug.LogError($"[MachineSlot] Cannot purchase '{GetMachineLabel(option)}' because MoneyManager.Instance is null.");
                return;
            }

            // If we can't afford it, do NOT change the current machine.
            bool paid = MoneyManager.Instance.TryPurchase(upgradeCost, GetMachineLabel(option));
            if (!paid)
            {
                Debug.Log($"[MachineSlot] Not enough funds to purchase '{GetMachineLabel(option)}' (cost {upgradeCost}).");
                return;
            }
        }

        // Destroy any previous machine (only after successful purchase)
        if (currentMachineInstance != null)
        {
            Destroy(currentMachineInstance);
            currentMachineInstance = null;
        }

        Transform parent = spawnPoint != null ? spawnPoint : transform;

        currentMachineInstance = Instantiate(
            option.config.machinePrefab,
            parent.position,
            parent.rotation,
            parent
        );

        currentIndex = index;

        // Notify listeners (e.g., ThroughputAggregator) that this slot's selection changed.
        OnSelectionChanged?.Invoke(this);
    }

    public void SelectBasic()
    {
        SelectOption(0);
        SetMenuVisible(false);
    }

    public void SelectMedium()
    {
        SelectOption(1);
        SetMenuVisible(false);
    }

    public void SelectPremium()
    {
        SelectOption(2);
        SetMenuVisible(false);
    }

    private void OnMouseDown()
    {
        // If clicking UI (e.g., popup buttons), don't also toggle the slot menu.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (hoverMenu == null) return;

        bool newState = !hoverMenu.activeSelf;
        SetMenuVisible(newState);
        Debug.Log($"[MachineSlot] OnMouseDown fired on: {name}, menu state: {newState}");
    }
}
