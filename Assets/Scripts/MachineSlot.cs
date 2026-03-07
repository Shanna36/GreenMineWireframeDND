using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;

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
    // Expose the selected option index (0 = Basic, 1 = Medium, 2 = Premium)
    public int CurrentIndex => currentIndex;



    // Fired whenever the player selects/changes the machine option for this slot.
    public event Action<MachineSlot> OnSelectionChanged;

    // Fired when operational state changes (e.g., safety event locks a machine).
    public event Action<MachineSlot, bool> OnOperationalChanged;


    [Header("Visuals (optional)")]
    [Tooltip("Optional warning effect to toggle during maintenance degrade (e.g., a flashing yellow particle system GameObject).")]
    public GameObject warningEffect;

    [Tooltip("Optional placeholder visual (e.g., the cube/ghost base) to hide once a machine is installed.")]
    [SerializeField] private GameObject placeholderVisual;

    // --- Event state (maintenance/breakdown) ---

    [SerializeField, Tooltip("If false, this slot is considered stopped by an event (throughput = 0).")]
    private bool isOperational = true;

    [SerializeField, Range(0f, 1f), Tooltip("Multiplier applied to throughput due to events/maintenance (1=normal, 0.7=degraded).")]
    private float throughputMultiplier = 1f;

    // --- Throughput Aggregator helpers ---

    // True if a valid MachineConfig is currently selected for this slot.
    public bool HasMachineInstalled => CurrentConfig != null;

    public bool IsOperational => isOperational;

    /// <summary>
    /// Sets whether this machine is operational (throughput forced to 0 when false).
    /// Used by safety/breakdown events.
    /// </summary>
    public void SetOperational(bool operational)
    {
        if (isOperational == operational) return;
        isOperational = operational;

        // Keep multiplier consistent with operational state.
        if (!isOperational)
            throughputMultiplier = 0f;
        else if (throughputMultiplier <= 0f)
            throughputMultiplier = 1f;

        OnOperationalChanged?.Invoke(this, isOperational);
        OnSelectionChanged?.Invoke(this);
    }

    public float ThroughputMultiplier => throughputMultiplier;

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
    // Applies event modifiers via throughputMultiplier/isOperational.
    public float CurrentThroughputTPH
    {
        get
        {
            var cfg = CurrentConfig;
            if (cfg == null) return 0f;

            if (!isOperational) return 0f;

            Type t = cfg.GetType();
            float mult = Mathf.Max(0f, throughputMultiplier);

            // Prefer the standardized property name.
            PropertyInfo p = t.GetProperty("ThroughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float))
                return Mathf.Max(0f, (float)p.GetValue(cfg)) * mult;

            // Fallbacks
            p = t.GetProperty("throughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float))
                return Mathf.Max(0f, (float)p.GetValue(cfg)) * mult;

            FieldInfo f = t.GetField("throughputTPH", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
                return Mathf.Max(0f, (float)f.GetValue(cfg)) * mult;

            f = t.GetField("throughputTph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
                return Mathf.Max(0f, (float)f.GetValue(cfg)) * mult;

            f = t.GetField("throughputTonnesPerHour", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
                return Mathf.Max(0f, (float)f.GetValue(cfg)) * mult;

            f = t.GetField("throughput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
                return Mathf.Max(0f, (float)f.GetValue(cfg)) * mult;

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
        UpdatePlaceholderVisual();
    }

    private void SetMenuVisible(bool isVisible)
    {
        if (hoverMenu == null) return;

        hoverMenu.SetActive(isVisible);

        // While testing UI, keep cursor visible/unlocked.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void UpdatePlaceholderVisual()
    {
        if (placeholderVisual == null) return;

        // Show placeholder only when no machine is installed.
        placeholderVisual.SetActive(!HasMachineInstalled);
    }

    private void SnapInstanceToSpawn(GameObject instance, Transform spawn)
    {
        if (instance == null || spawn == null) return;

        // Preserve the prefab's authored transforms.
        Vector3 authoredLocalScale = instance.transform.localScale;
        Quaternion authoredWorldRotation = instance.transform.rotation;

        Debug.Log(
            $"[MachineSlot] SnapInstanceToSpawn: instance='{instance.name}', spawn='{spawn.name}', " +
            $"spawnWorldPos={spawn.position}, spawnWorldRot={spawn.rotation.eulerAngles}"
        );
        Debug.Log($"[MachineSlot] SnapInstanceToSpawn: authoredLocalScale={authoredLocalScale}");
        Debug.Log($"[MachineSlot] SnapInstanceToSpawn: authoredWorldRotation={authoredWorldRotation.eulerAngles}");

        // Parent while preserving current WORLD transform.
        instance.transform.SetParent(spawn, worldPositionStays: true);

        // Snap position to the spawn point, but keep the prefab's authored WORLD rotation.
        instance.transform.position = spawn.position;
        instance.transform.rotation = authoredWorldRotation;

        // Restore authored scale (in case parenting affected it).
        instance.transform.localScale = authoredLocalScale;

        // If the prefab has a Rigidbody on the ROOT, set its pose directly to avoid jitter.
        // IMPORTANT: Keep authored rotation (do not force spawn rotation).
        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = spawn.position;
            rb.rotation = authoredWorldRotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        // Note: If the visible mesh is offset on a child, fix that in the prefab (preferred),
        // rather than trying to correct child transforms here.
    }

    /// <summary>
    /// Spawn one of the machine options into this slot.
    /// This can be called from UI buttons with the appropriate index.
    /// </summary>
    public void SelectOption(int index)
    {
        Debug.Log($"[MachineSlot] SelectOption({index}) on {name}");
        // Extra debug to confirm the full decision chain for spawning.
        Debug.Log($"[MachineSlot] State before selection: currentIndex={currentIndex}, HasMachineInstalled={HasMachineInstalled}, CurrentConfig={(CurrentConfig ? CurrentConfig.name : "NULL")}");
        Debug.Log($"[MachineSlot] options={(options == null ? "NULL" : options.Length.ToString())}, spawnPoint={(spawnPoint ? spawnPoint.name : "NULL")}");

        if (options == null || options.Length == 0)
        {
            Debug.LogWarning($"[MachineSlot] EARLY RETURN: options missing/empty on {name}. options={(options == null ? "NULL" : options.Length.ToString())}");
            return;
        }

        if (index < 0 || index >= options.Length)
        {
            Debug.LogWarning($"[MachineSlot] EARLY RETURN: index out of range. index={index}, optionsLength={options.Length} on {name}.");
            return;
        }

        MachineOption option = options[index];
        Debug.Log($"[MachineSlot] Selected option: index={index}, displayName='{option?.displayName}', config={(option != null && option.config != null ? option.config.name : "NULL")}");

        if (option.config == null)
        {
            Debug.LogWarning($"[MachineSlot] EARLY RETURN: Option {index} on {name} has no MachineConfig assigned.");
            return;
        }

        if (option.config.machinePrefab == null)
        {
            Debug.LogWarning($"[MachineSlot] EARLY RETURN: MachineConfig '{option.config.name}' has no prefab assigned (machinePrefab is NULL).");
            return;
        }

        // If selecting the same option again, do nothing.
        if (index == currentIndex)
        {
            Debug.Log($"[MachineSlot] Option {index} already selected on {name}. No action taken.");
            Debug.Log($"[MachineSlot] EARLY RETURN: re-select of current option. currentIndex={currentIndex}.");
            UpdatePlaceholderVisual();
            SetMenuVisible(false);
            return;
        }

        // --- Purchase check (v1) ---
        // Charge only the upgrade delta (no refund on downgrades).
        Debug.Log($"[MachineSlot] Purchase check: newConfig={option.config.name}, newCost={GetCost(option.config)}, oldConfig={(CurrentConfig ? CurrentConfig.name : "NULL")}, oldCost={GetCost(CurrentConfig)}");
        int newCost = GetCost(option.config);
        int oldCost = GetCost(CurrentConfig);
        int upgradeCost = Mathf.Max(0, newCost - oldCost);

        if (upgradeCost > 0)
        {
            if (MoneyManager.Instance == null)
            {
                Debug.LogError($"[MachineSlot] EARLY RETURN: Cannot purchase '{GetMachineLabel(option)}' because MoneyManager.Instance is null. upgradeCost={upgradeCost}");
                return;
            }

            bool paid = MoneyManager.Instance.TryPurchase(upgradeCost, GetMachineLabel(option));
            if (!paid)
            {
                Debug.LogWarning($"[MachineSlot] EARLY RETURN: Not enough funds to purchase '{GetMachineLabel(option)}' (upgradeCost {upgradeCost}).");
                return;
            }
        }

        // Destroy any previous machine (only after successful purchase)
        if (currentMachineInstance != null)
        {
            Destroy(currentMachineInstance);
            currentMachineInstance = null;
        }

        Debug.Log($"[MachineSlot] Spawning prefab '{option.config.machinePrefab.name}'. Parent target = {(spawnPoint != null ? spawnPoint.name : "<this transform>")}");

        Transform parent = spawnPoint != null ? spawnPoint : transform;

        // Instantiate without parenting first, then snap/parent in a controlled way.
        currentMachineInstance = Instantiate(option.config.machinePrefab);

        if (currentMachineInstance == null)
        {
            Debug.LogError($"[MachineSlot] Instantiate returned NULL for prefab '{option.config.machinePrefab.name}' on {name}.");
            return;
        }

        Debug.Log($"[MachineSlot] Spawned instance '{currentMachineInstance.name}' (activeSelf={currentMachineInstance.activeSelf}) before snap. WorldPos={currentMachineInstance.transform.position} WorldScale={currentMachineInstance.transform.lossyScale}");

        SnapInstanceToSpawn(currentMachineInstance, parent);

        // Post-snap diagnostics: rendering + transforms
        var renderers = currentMachineInstance.GetComponentsInChildren<Renderer>(includeInactive: true);
        Debug.Log($"[MachineSlot] After snap: parent={currentMachineInstance.transform.parent?.name}, localPos={currentMachineInstance.transform.localPosition}, localRot={currentMachineInstance.transform.localRotation.eulerAngles}, localScale={currentMachineInstance.transform.localScale}. RenderersFound={renderers.Length}");
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[MachineSlot] Spawned instance '{currentMachineInstance.name}' has NO Renderer components in children. It may be an empty root/pivot prefab or children are missing/disabled.");
        }
        else
        {
            // Log the first renderer as a quick sanity check.
            var r0 = renderers[0];
            Debug.Log($"[MachineSlot] First renderer: '{r0.name}' enabled={r0.enabled} activeInHierarchy={r0.gameObject.activeInHierarchy} layer={r0.gameObject.layer}");
        }

        currentIndex = index;
        UpdatePlaceholderVisual();

        // Installing/changing a machine returns it to normal operation.
        isOperational = true;
        throughputMultiplier = 1f;

        OnSelectionChanged?.Invoke(this);

        SetMenuVisible(false);
    }

    // Wrapper methods for UI buttons (these are likely what your buttons are wired to)
    public void SelectBasic() { SelectOption(0); }
    public void SelectMedium() { SelectOption(1); }
    public void SelectPremium() { SelectOption(2); }

    private void OnMouseDown()
    {
        // If clicking UI (e.g., popup buttons), don't also toggle the slot menu.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (hoverMenu == null) return;

        SetMenuVisible(!hoverMenu.activeSelf);
    }

    /// <summary>
    /// Enables/disables a warning effect to indicate a degraded state.
    /// The particle system handles its own animation.
    /// </summary>
    public void SetWarningState(bool enabled)
    {
        Debug.LogWarning($"[MachineSlot] {name} SetWarningState({enabled}) effect={(warningEffect ? warningEffect.name : "NULL")}");

        if (warningEffect == null)
            return;

        warningEffect.SetActive(enabled);
    }

    // --- Event API (called by EventManager handlers) ---

    public void ApplyThroughputMultiplier(float multiplier)
    {
        throughputMultiplier = Mathf.Clamp01(multiplier);

        // Defensive: if throughput is driven to zero by an event, treat as non-operational.
        if (throughputMultiplier <= 0f)
        {
            isOperational = false;
        }

        OnSelectionChanged?.Invoke(this);
    }

    public void StopOperational()
    {
        isOperational = false;
        throughputMultiplier = 0f;
        SetWarningState(false);
        OnOperationalChanged?.Invoke(this, isOperational);
        OnSelectionChanged?.Invoke(this);
    }

    public void RestoreOperational()
    {
        isOperational = true;
        throughputMultiplier = 1f;
        SetWarningState(false);
        OnOperationalChanged?.Invoke(this, isOperational);
        OnSelectionChanged?.Invoke(this);
    }
}
