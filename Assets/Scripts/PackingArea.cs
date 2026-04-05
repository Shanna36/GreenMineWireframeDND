using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Packing/dispatch area.
/// - Fills hopper UI bars based on ThroughputAggregator output per second.
/// - Blocks processing when hoppers are full (optional).
/// - When a hopper is shipped, it delegates payout logic to MoneyManager.
///
/// NOTE: Money is owned by MoneyManager (single source of truth).
///Changed trigger behaviour to make character travel to each material's hopper 13/3/26
/// /// </summary>
public class PackingArea : MonoBehaviour
{
    public enum OutputType { Fibre, Plastics, Aluminium, Steel, Residue }

    public static PackingArea Instance { get; private set; }

    [Header("Contamination (V1)")]
    [Tooltip("Multiplier applied to shipment value during contamination events. 1 = normal, 0.8 = -20% value.")]
    public float contaminationMultiplier = 1f;

    [Serializable]
    public class Hopper
    {
        public OutputType type;

        [Range(0f, 1f)]
        public float fraction = 0.2f;

        [Tooltip("Max tonnes this hopper can hold before blocking the line.")]
        public float capacityTonnes = 2f;

        [Header("UI (optional)")]
        [Tooltip("Legacy horizontal bar support (optional). You can leave this empty when using Image fill bars.")]
        public Slider slider;

        [Tooltip("Preferred: assign the Image that should fill (set its Image Type to Filled).")]
        public Image fillImage;

        [Header("Near Full Glow")]
        [Tooltip("When the hopper is this full (fraction of capacity), start pulsing the bar colour.")]
        [Range(0.5f, 1f)]
        public float nearFullThreshold = 0.9f;

        [Tooltip("Colour used for the pulse when near full.")]
        public Color nearFullColor = Color.yellow;

        [Tooltip("Speed of the pulse animation.")]
        public float pulseSpeed = 3f;

        [Tooltip("Optional text (e.g., '0 / 2 t').")]
        public TMP_Text label;

        public Button shipButton;

        [Header("Per-Hopper Shipping Zone")]
        [Tooltip("Optional trigger collider for this specific hopper. When assigned, the ship button only activates while the player is near this hopper.")]
        public Collider shippingZoneTrigger;

        [SerializeField]
        private float currentTonnes = 0f;

        public float CurrentTonnes => currentTonnes;

        public bool IsFull => capacityTonnes > 0f && currentTonnes >= capacityTonnes - 1e-6f;

        public void AddTonnes(float tonnes)
        {
            if (capacityTonnes <= 0f) return;
            currentTonnes = Mathf.Min(capacityTonnes, currentTonnes + Mathf.Max(0f, tonnes));
        }

        public void Clear()
        {
            currentTonnes = 0f;
        }

        public void RefreshUI()
        {
            // Legacy Slider support (horizontal bars)
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = capacityTonnes > 0f ? capacityTonnes : 1f;
                slider.value = currentTonnes;
            }

            // Preferred Image fill support (works for vertical or horizontal Filled Images)
            if (fillImage != null)
            {
                float denom = capacityTonnes > 0f ? capacityTonnes : 1f;
                float fill = Mathf.Clamp01(currentTonnes / denom);
                fillImage.fillAmount = fill;

                // Normal colour is green. When nearly full, pulse toward yellow.
                if (fill >= nearFullThreshold)
                {
                    float pulse = Mathf.PingPong(Time.time * pulseSpeed, 1f);
                    fillImage.color = Color.Lerp(Color.green, nearFullColor, pulse);
                }
                else
                {
                    fillImage.color = Color.green;
                }
            }

            if (label != null)
            {
                label.text = $"{type}: {currentTonnes:0.##} / {capacityTonnes:0.##} t";
            }

            if (shipButton != null)
            {
                // Allow shipping whenever there is material in the hopper.
                // Events can temporarily disable shipping (e.g. transport breakdown).
                bool globallyDisabled = PackingArea.Instance != null && PackingArea.Instance.IsShippingDisabled;
                bool playerOk = true;
                if (PackingArea.Instance != null && PackingArea.Instance.requirePlayerInZoneToShip)
                {
                    playerOk = PackingArea.Instance.IsPlayerInHopperZone(this);
                }

                shipButton.interactable = currentTonnes > 0f && !globallyDisabled && playerOk;
            }
        }
    }

    [Header("References")]
    public ThroughputAggregator throughputAggregator;

    [Header("Hoppers (fractions should roughly total 1.0)")]
    public List<Hopper> hoppers = new List<Hopper>();

    [Header("Behaviour")]
    [Tooltip("If true, any full hopper blocks all processing.")]
    public bool blockWhenAnyFull = true;

    [Header("Shipping Zone")]
    [Tooltip("If true, each ship button only activates when the player is near that hopper's assigned shipping zone collider.")]
    [SerializeField] private bool requirePlayerInZoneToShip = true;

    [Tooltip("Tag used to identify the player object for hopper shipping zone checks.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Optional explicit player root transform. Leave empty to find by tag at runtime.")]
    [SerializeField] private Transform playerRoot;

    private Collider[] cachedPlayerColliders = Array.Empty<Collider>();

    [Tooltip("If you don't have contamination flowing through the sim yet, keep this at 0.")]
    [Range(0f, 1f)]
    public float defaultContaminationRate = 0f;

    [Header("Costs")]
    [SerializeField] private int dumpCostPerDump = 500;

    public bool IsBlocked { get; private set; }

    // When true, all ship buttons are disabled regardless of hopper fullness.
    // Used by the Events system (e.g. transport breakdown) to create a temporary dispatch bottleneck.
    private bool shippingDisabled = false;

    // If shippingDisabled is time-bound, this stores when shipping should be re-enabled.
    // (Time.time in seconds)
    private float shippingDisabledUntil = -1f;

    /// <summary>
    /// True when shipping is currently disabled by an event.
    /// </summary>
    public bool IsShippingDisabled => shippingDisabled;

    private void OnEnable()
    {
        CachePlayerReferences();
        RefreshAllUI();
    }

    private void Awake()
    {
        // Scene-specific singleton: replace any stale instance from prior scenes.
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }
        Instance = this;

        // Wire buttons (if assigned)
        foreach (var hopper in hoppers)
        {
            if (hopper == null || hopper.shipButton == null) continue;

            var capturedType = hopper.type;
            hopper.shipButton.onClick.RemoveAllListeners();
            hopper.shipButton.onClick.AddListener(() => Ship(capturedType));
        }

        RefreshAllUI();
    }

    private void Update()
    {
        if (throughputAggregator == null) return;

        // Auto-clear any timed shipping disable.
        if (shippingDisabled && shippingDisabledUntil >= 0f && Time.time >= shippingDisabledUntil)
        {
            SetShippingDisabled(false);
        }

        // Block rule
        IsBlocked = blockWhenAnyFull && AnyHopperFull();

        if (!IsBlocked)
        {
            float tonnesThisFrame = throughputAggregator.TonnesPerRealSecond * Time.deltaTime;

            if (tonnesThisFrame > 0f)
            {
                foreach (var hopper in hoppers)
                {
                    if (hopper == null) continue;
                    hopper.AddTonnes(tonnesThisFrame * hopper.fraction);
                }
            }
        }

        RefreshAllUI();
    }

    private bool AnyHopperFull()
    {
        foreach (var hopper in hoppers)
        {
            if (hopper != null && hopper.IsFull) return true;
        }
        return false;
    }

    private void RefreshAllUI()
    {
        foreach (var hopper in hoppers)
        {
            hopper?.RefreshUI();
        }
    }

    /// <summary>
    /// Disable or enable all shipping buttons.
    /// Used by events (e.g. transport breakdown) to temporarily prevent shipping.
    /// </summary>
    public void SetShippingDisabled(bool disabled)
    {
        shippingDisabled = disabled;
        if (!disabled)
        {
            shippingDisabledUntil = -1f;
        }

        // Refresh immediately so buttons reflect the new state.
        RefreshAllUI();
    }

    /// <summary>
    /// Disable shipping for a fixed duration in seconds.
    /// Calling this again extends the disable window.
    /// </summary>
    public void DisableShippingForSeconds(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        if (seconds <= 0f)
        {
            SetShippingDisabled(false);
            return;
        }

        shippingDisabled = true;
        shippingDisabledUntil = Mathf.Max(shippingDisabledUntil, Time.time + seconds);

        // Refresh immediately so buttons disable right away.
        RefreshAllUI();
    }

    /// <summary>
    /// Ship the specified hopper's contents.
    /// Clears the hopper and delegates payout to MoneyManager.
    /// </summary>
    public void Ship(OutputType type)
    {
         Debug.Log("SHIP BUTTON CLICKED: " + type);
        var hopper = hoppers.Find(h => h != null && h.type == type);
        if (hopper == null) return;

        // If shipping is disabled (e.g. a logistics event), do nothing.
        if (shippingDisabled)
        {
            Debug.Log($"Ship blocked: shipping is temporarily disabled (event/logistics). ({type})");
            return;
        }

        // If we require the player to be near this hopper's own shipping zone, block shipping when they're outside it.
        if (requirePlayerInZoneToShip && !IsPlayerInHopperZone(hopper))
        {
            Debug.Log($"Ship blocked: player is not near the assigned hopper shipping zone. ({type})");
            return;
        }

        // Allow shipping whenever there is material in the hopper
        if (hopper.CurrentTonnes <= 0f) return;

        float tonnes = hopper.CurrentTonnes;
        hopper.Clear();

        // Update block state immediately
        IsBlocked = blockWhenAnyFull && AnyHopperFull();
        RefreshAllUI();

        // Delegate money
        if (MoneyManager.Instance == null)
        {
            Debug.LogError("Ship pressed but MoneyManager.Instance is null. Ensure a MoneyManager exists in the scene.");
            return;
        }

        // Map hopper type to your MaterialType enum.
        // If your MaterialType names differ, update the switch in GetMaterialType.
        MaterialType material = GetMaterialType(type);

        // In v1 we don't yet have contamination per-hopper; use a default.
        MoneyManager.Instance.CreditShipment(material, tonnes, defaultContaminationRate * contaminationMultiplier);
    }

    /// <summary>
    /// Attempt to pay the dump fee for residue. Returns true if paid (or free).
    /// (You can call this from a Dump button.)
    /// </summary>
    public bool DumpResidue()
    {
        if (dumpCostPerDump <= 0) return true;

        if (MoneyManager.Instance == null)
        {
            Debug.LogError("DumpResidue called but MoneyManager.Instance is null. Ensure a MoneyManager exists in the scene.");
            return false;
        }

        return MoneyManager.Instance.TrySpend(dumpCostPerDump, PayType.Dump, "Dump");
    }

    public bool TryDumpResidue() => DumpResidue();

    /// <summary>
    /// UI Button wrapper for dumping residue.
    /// Unity's Button OnClick only lists void-returning methods in the inspector,
    /// so this wrapper lets us call the bool-returning DumpResidue() from UI.
    /// </summary>
    public void DumpResidueClicked()
    {
        bool paid = DumpResidue();
        if (!paid)
        {
            Debug.Log("DumpResidueClicked: Dump failed (insufficient funds or missing MoneyManager).");
            return;
        }

        // Clear the residue hopper when dumping
        var residue = hoppers.Find(h => h != null && h.type == OutputType.Residue);
        if (residue != null)
        {
            residue.Clear();
        }

        RefreshAllUI();
    }

    // --- UI Button wrappers for shipping ---
    // Unity Button OnClick cannot pass enum parameters, so these
    // wrappers allow each material button to call Ship() correctly.

    public void ShipFibreClicked()
    {
        Ship(OutputType.Fibre);
    }

    public void ShipPlasticsClicked()
    {
        Ship(OutputType.Plastics);
    }

    public void ShipAluminiumClicked()
    {
        Ship(OutputType.Aluminium);
    }

    public void ShipSteelClicked()
    {
        Ship(OutputType.Steel);
    }

    public void ShipResidueClicked()
    {
        Ship(OutputType.Residue);
    }


    private void CachePlayerReferences()
    {
        if (playerRoot == null)
        {
            var playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
            {
                playerRoot = playerObject.transform;
            }
        }

        if (playerRoot != null)
        {
            cachedPlayerColliders = playerRoot.GetComponentsInChildren<Collider>(true);
        }
        else
        {
            cachedPlayerColliders = Array.Empty<Collider>();
        }
    }

    public bool IsPlayerInHopperZone(Hopper hopper)
    {
        if (!requirePlayerInZoneToShip)
            return true;

        if (hopper == null)
            return false;

        if (hopper.shippingZoneTrigger == null)
            return true;

        if (playerRoot == null || cachedPlayerColliders == null || cachedPlayerColliders.Length == 0)
        {
            CachePlayerReferences();
        }

        if (hopper.shippingZoneTrigger == null || playerRoot == null)
            return false;

        foreach (var playerCollider in cachedPlayerColliders)
        {
            if (playerCollider == null) continue;
            if (hopper.shippingZoneTrigger.bounds.Intersects(playerCollider.bounds))
                return true;
        }

        return hopper.shippingZoneTrigger.bounds.Contains(playerRoot.position);
    }

    private MaterialType GetMaterialType(OutputType type)
    {
        // IMPORTANT: Adjust these mappings to match your project's MaterialType enum.
        switch (type)
        {
            case OutputType.Fibre:
                return MaterialType.Fibre;
            case OutputType.Plastics:
                return MaterialType.Plastics;
            case OutputType.Aluminium:
                return MaterialType.Aluminium;
            case OutputType.Steel:
                return MaterialType.Steel;
            case OutputType.Residue:
            default:
                return MaterialType.Residue;
        }
    }

    /// <summary>
    /// Set contamination multiplier directly (used by events).
    /// </summary>
    public void SetContaminationMultiplier(float multiplier)
    {
        contaminationMultiplier = Mathf.Clamp(multiplier, 0f, 1f);
    }

    /// <summary>
    /// Reset contamination back to normal.
    /// </summary>
    public void ClearContamination()
    {
        contaminationMultiplier = 1f;
    }
}