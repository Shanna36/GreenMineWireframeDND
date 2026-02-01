// Assets/Scripts/Events/EventPopupUI.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventPopupUI : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject root;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text timerText;
    public Button primaryButton;
    public TMP_Text primaryButtonText;
    public Button secondaryButton;
    public TMP_Text secondaryButtonText;

    private Action onPrimary;
    private Action onSecondary;

    public void Show(
        string title,
        string body,
        string primaryLabel,
        Action primaryAction,
        string secondaryLabel,
        Action secondaryAction)
    {
        if (root != null) root.SetActive(true);

        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;

        onPrimary = primaryAction;
        onSecondary = secondaryAction;

        if (primaryButtonText != null) primaryButtonText.text = primaryLabel;
        if (secondaryButtonText != null) secondaryButtonText.text = secondaryLabel;

        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.onClick.AddListener(() => onPrimary?.Invoke());
        }

        if (secondaryButton != null)
        {
            secondaryButton.onClick.RemoveAllListeners();
            secondaryButton.onClick.AddListener(() => onSecondary?.Invoke());
        }

        SetTimerVisible(false, 0f);
    }

    public void SetTimerVisible(bool visible, float secondsRemaining)
    {
        if (timerText != null)
        {
            timerText.gameObject.SetActive(visible);
            if (visible)
                timerText.text = $"Resolves in {Mathf.CeilToInt(secondsRemaining)}s";
        }
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        onPrimary = null;
        onSecondary = null;
    }
}