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
/// </summary>
public class PackingArea : MonoBehaviour
{
    public enum OutputType { Fibre, Plastics, Aluminium, Steel, Residue }

    public static PackingArea Instance { get; private set; }

    [Serializable]
    public class Hopper
    {
        public OutputType type;

        [Range(0f, 1f)]
        public float fraction = 0.2f;

        [Tooltip("Max tonnes this hopper can hold before blocking the line.")]
        public float capacityTonnes = 2f;

        [Header("UI (optional)")]
        public Slider slider;
        public TMP_Text label;
        public Button shipButton;

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
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = capacityTonnes > 0f ? capacityTonnes : 1f;
                slider.value = currentTonnes;
            }

            if (label != null)
            {
                label.text = $"{type}: {currentTonnes:0.##} / {capacityTonnes:0.##} t";
            }

            if (shipButton != null)
            {
                // v1 behaviour: only allow shipping when full.
                shipButton.interactable = IsFull;
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

    [Tooltip("If you don't have contamination flowing through the sim yet, keep this at 0.")]
    [Range(0f, 1f)]
    public float defaultContaminationRate = 0f;

    [Header("Costs")]
    [SerializeField] private int dumpCostPerDump = 500;

    public bool IsBlocked { get; private set; }

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
    /// Ship the specified hopper's contents.
    /// Clears the hopper and delegates payout to MoneyManager.
    /// </summary>
    public void Ship(OutputType type)
    {
        var hopper = hoppers.Find(h => h != null && h.type == type);
        if (hopper == null) return;

        // v1: only ship if full (matches button interactable)
        if (!hopper.IsFull) return;

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
        MoneyManager.Instance.CreditShipment(material, tonnes, defaultContaminationRate);
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

        return MoneyManager.Instance.TrySpend(dumpCostPerDump, TransactionType.Dump, "Dump");
    }

    public bool TryDumpResidue() => DumpResidue();

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
}

/// <summary>
/// Transaction tags used for UI/logging.
/// </summary>
public enum TransactionType
{
    Ship,
    Dump,
    Purchase
}
