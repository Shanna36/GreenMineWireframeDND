using UnityEngine;
using TMPro;

public class ContaminationHUD : MonoBehaviour
{
    public TMP_Text contaminationText;

    public string normalText = "Contamination: Normal";
    public string contaminatedTextFormat = "Contamination: HIGH (-{0}% value)";

    private float lastMultiplier = -1f;

    private void Awake()
    {
        if (contaminationText == null)
            contaminationText = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (PackingArea.Instance == null)
            return;

        float multiplier = PackingArea.Instance.contaminationMultiplier;

        if (Mathf.Approximately(multiplier, lastMultiplier))
            return;

        lastMultiplier = multiplier;

        if (multiplier >= 0.999f)
            contaminationText.text = normalText;
        else
            contaminationText.text = string.Format(contaminatedTextFormat, Mathf.RoundToInt((1f - multiplier) * 100f));
    }
}
