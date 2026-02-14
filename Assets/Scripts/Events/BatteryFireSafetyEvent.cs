using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Note: This script is intentionally standalone so we don't have to refactor your existing EventManager.
// Drop it on an empty "SafetyEventController" object in the scene.
// It will trigger ONE guaranteed battery-fire event within the first ~5 minutes of play.
public class BatteryFireSafetyEvent : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The MachineSlot representing the Magnet Separator.")]
    public MachineSlot magnetSeparatorSlot;

    [Tooltip("Optional: A transform at/near the Magnet Separator used for distance checks.")]
    public Transform magnetTransform;

    [Header("Player")]
    [Tooltip("Player avatar transform used for the 'must be near the machine' requirement.")]
    public Transform player;

    [Tooltip("How close the player must be to the Magnet to enable the Put Out Fire button.")]
    public float interactDistance = 2.5f;

    [Header("Timing")]
    [Tooltip("Seconds the player has to put out the fire before escalation.")]
    public float countdownSeconds = 8f;

    [Tooltip("Fire will be triggered once, at a random time between these (seconds after start).")]
    public Vector2 triggerWindowSeconds = new Vector2(60f, 240f);

    [Header("Costs")]
    [Tooltip("Cost to repair the magnet if the player fails the countdown.")]
    public int repairCost = 400;

    [Header("UI")]
    [Tooltip("Root panel for the big warning UI.")]
    public GameObject warningPanel;

    [Tooltip("Button the player presses to put out the fire.")]
    public Button putOutFireButton;

    [Tooltip("Countdown TMP text element.")]
    public TMP_Text countdownTMP;

    [Tooltip("Status / escalation message TMP text element.")]
    public TMP_Text statusTMP;

    [Tooltip("Optional smaller 'heads-up' UI shown at start (recommended).")]
    public GameObject safetyTipPanel;

    [Tooltip("How long to show the safety tip at the beginning.")]
    public float safetyTipSeconds = 4f;

    [Header("Visuals")]
    [Tooltip("Flames / red glow VFX object to enable during the fire.")]
    public GameObject fireVFX;

    private bool _hasTriggered;
    private bool _isActive;
    private float _timeLeft;

    private void Start()
    {
        if (safetyTipPanel != null)
        {
            safetyTipPanel.SetActive(true);
            Invoke(nameof(HideSafetyTip), Mathf.Max(0.5f, safetyTipSeconds));
        }

        SetWarningUI(false);
        SetFireVFX(false);

        if (putOutFireButton != null)
        {
            putOutFireButton.onClick.RemoveListener(PutOutFire);
            putOutFireButton.onClick.AddListener(PutOutFire);
        }

        float min = Mathf.Min(triggerWindowSeconds.x, triggerWindowSeconds.y);
        float max = Mathf.Max(triggerWindowSeconds.x, triggerWindowSeconds.y);
        float delay = UnityEngine.Random.Range(min, max);
        Invoke(nameof(TriggerFireOnce), delay);
    }

    private void Update()
    {
#if UNITY_EDITOR
        // Dev hotkeys (Editor only). Supports both legacy Input and the new Input System.
        bool fPressed = false;
        bool gPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            fPressed = Keyboard.current.fKey.wasPressedThisFrame;
            gPressed = Keyboard.current.gKey.wasPressedThisFrame;
        }
#else
        fPressed = Input.GetKeyDown(KeyCode.F);
        gPressed = Input.GetKeyDown(KeyCode.G);
#endif

        // Press F to force trigger fire event
        if (!_hasTriggered && fPressed)
        {
            Debug.Log("[BatteryFireSafetyEvent] Dev hotkey F pressed -> triggering fire.");
            TriggerFireOnce();
        }

        // Press G to instantly escalate (fail)
        if (_isActive && gPressed)
        {
            Debug.Log("[BatteryFireSafetyEvent] Dev hotkey G pressed -> escalating.");
            Escalate();
        }
#endif

        if (!_isActive) return;

        _timeLeft -= Time.deltaTime;

        UpdateInteractable();
        UpdateCountdownText();

        if (_timeLeft <= 0f)
        {
            Escalate();
        }
    }


    private void HideSafetyTip()
    {
        if (safetyTipPanel != null) safetyTipPanel.SetActive(false);
    }

    /// <summary>
    /// Dev helper: forces the fire event to trigger immediately (still only triggers once).
    /// Called by EventManager debug hotkey.
    /// </summary>
    public void DebugForceFire()
    {
        TriggerFireOnce();
    }

    private void TriggerFireOnce()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        if (magnetSeparatorSlot == null || !magnetSeparatorSlot.HasMachineInstalled)
        {
            Debug.LogWarning("[BatteryFireSafetyEvent] Magnet slot missing or not installed — skipping fire event.");
            return;
        }

        _isActive = true;
        _timeLeft = Mathf.Max(1f, countdownSeconds);

        SetFireVFX(true);
        SetWarningUI(true);
        UpdateInteractable();
        UpdateCountdownText();
    }

    private void PutOutFire()
    {
        if (!_isActive) return;
        if (!IsPlayerInRange()) return;

        _isActive = false;
        SetFireVFX(false);
        SetWarningUI(false);
        if (statusTMP != null)
            statusTMP.text = string.Empty;
    }

    private void Escalate()
    {
        if (!_isActive) return;
        _isActive = false;

        if (magnetSeparatorSlot != null)
        {
            magnetSeparatorSlot.SetOperational(false);
        }

        if (putOutFireButton != null)
        {
            putOutFireButton.onClick.RemoveListener(PutOutFire);
            putOutFireButton.onClick.AddListener(RepairMagnet);
            putOutFireButton.interactable = true;

            var tmpLabel = putOutFireButton.GetComponentInChildren<TMP_Text>();
            if (tmpLabel != null) tmpLabel.text = $"Repair ({repairCost} coins)";
        }

        SetFireVFX(true);
        SetWarningUI(true);
        if (statusTMP != null)
            statusTMP.text = "Fire spread! Magnet is offline until repaired.";
    }

    private void RepairMagnet()
    {
        if (magnetSeparatorSlot == null) return;
        if (MoneyManager.Instance == null)
        {
            Debug.LogError("[BatteryFireSafetyEvent] MoneyManager.Instance is null — cannot repair.");
            return;
        }

        bool paid = MoneyManager.Instance.TrySpend(repairCost);
        if (!paid)
        {
            if (statusTMP != null)
                statusTMP.text = "Not enough coins to repair!";
            return;
        }

        magnetSeparatorSlot.SetOperational(true);

        if (putOutFireButton != null)
        {
            putOutFireButton.onClick.RemoveListener(RepairMagnet);
            putOutFireButton.onClick.AddListener(PutOutFire);

            var tmpLabel = putOutFireButton.GetComponentInChildren<TMP_Text>();
            if (tmpLabel != null) tmpLabel.text = "Put Out Fire";
        }

        SetFireVFX(false);
        SetWarningUI(false);
        if (statusTMP != null)
            statusTMP.text = string.Empty;
    }

    private void SetWarningUI(bool on)
    {
        if (warningPanel != null) warningPanel.SetActive(on);
    }

    private void SetFireVFX(bool on)
    {
        if (fireVFX != null) fireVFX.SetActive(on);
    }

    private void UpdateInteractable()
    {
        if (putOutFireButton == null) return;

        bool isRepairMode = (magnetSeparatorSlot != null && !magnetSeparatorSlot.IsOperational);
        putOutFireButton.interactable = isRepairMode || IsPlayerInRange();
    }

    private bool IsPlayerInRange()
    {
        if (player == null) return true;

        Transform target = magnetTransform != null ? magnetTransform : (magnetSeparatorSlot != null ? magnetSeparatorSlot.transform : null);
        if (target == null) return true;

        float d = Vector3.Distance(player.position, target.position);
        return d <= Mathf.Max(0.1f, interactDistance);
    }

    private void UpdateCountdownText()
    {
        if (!_isActive) return;

        string s = $"{Mathf.CeilToInt(_timeLeft)}s";

        if (countdownTMP != null)
            countdownTMP.text = s;
    }

    private void SetCountdownMessage(string message)
    {
        if (countdownTMP != null)
            countdownTMP.text = message;
    }
}
