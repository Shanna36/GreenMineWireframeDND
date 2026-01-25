using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class PackingArea : MonoBehaviour
{
    public enum OutputType { Fibre, Plastics, Aluminium, Steel, Residue }

    [Serializable]
    public class Hopper
    {
        public OutputType type;

        [Range(0f, 1f)]
        public float fraction = 0.2f;

        [Tooltip("Max tonnes this hopper can hold before blocking the line.")]
        public float capacityTonnes = 2f;

        private float currentTonnes = 0f; // cosmetic attribute; if float isn't defined, Unity ignores it

        [Header("UI (optional)")]
        public Slider slider;
        public TMP_Text label;   
        public Button shipButton; 

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
                shipButton.interactable = IsFull; // v1: only ship when full
            }
        }
    }

    [Header("References")]
    public ThroughputAggregator throughputAggregator;

    [Header("Hoppers (must total 1.0 fractions ideally)")]
    public List<Hopper> hoppers = new List<Hopper>();

    [Header("Behaviour")]
    [Tooltip("If true, any full hopper blocks all processing.")]
    public bool blockWhenAnyFull = true;

    public bool IsBlocked { get; private set; }

    private void Awake()
    {
        // Wire buttons automatically if assigned
        foreach (var hopper in hoppers)
        {
            if (hopper == null || hopper.shipButton == null) continue;

            var capturedType = hopper.type;
            hopper.shipButton.onClick.RemoveAllListeners();
            hopper.shipButton.onClick.AddListener(() => Ship(capturedType));
        }
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

    public void Ship(OutputType type)
    {
        var hopper = hoppers.Find(h => h != null && h.type == type);
        if (hopper == null) return;

        // v1: only ship if full (matches button interactable)
        if (!hopper.IsFull) return;

        hopper.Clear();

        // Unblock state will naturally update next frame, but we can update immediately too
        IsBlocked = blockWhenAnyFull && AnyHopperFull();
        hopper.RefreshUI();
    }

    // Optional QoL: call this from a "Ship All Full" button
    public void ShipAllFull()
    {
        foreach (var hopper in hoppers)
        {
            if (hopper != null && hopper.IsFull)
                hopper.Clear();
        }

        IsBlocked = blockWhenAnyFull && AnyHopperFull();
        RefreshAllUI();
    }
}