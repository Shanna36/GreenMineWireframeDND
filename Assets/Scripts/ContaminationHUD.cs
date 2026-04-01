using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ContaminationHUD : MonoBehaviour
{
    public TMP_Text contaminationText;
    public GameObject textContainer;
    public Image purityIcon;
    public GameObject normalIconObject;
    public GameObject riskIconObject;
    public Sprite normalIcon;
    public Sprite riskIcon;

    public string normalText = "Contamination:";
    public string contaminatedText = "Contamination: HIGH";
    [SerializeField] private float contaminatedThreshold = 0.999f;

    private float lastMultiplier = -1f;

    private void Start()
    {
        RefreshFromCurrentState();
    }

    private void Awake()
    {
        if (contaminationText == null)
            contaminationText = GetComponent<TMP_Text>();

        if (textContainer == null && contaminationText != null)
            textContainer = contaminationText.gameObject;

        if (purityIcon == null)
            purityIcon = GetComponent<Image>();
    }

    private void Update()
    {
        if (PackingArea.Instance == null)
            return;

        float multiplier = PackingArea.Instance.contaminationMultiplier;

        if (Mathf.Approximately(multiplier, lastMultiplier))
            return;

        lastMultiplier = multiplier;

        bool isContaminated = multiplier < contaminatedThreshold;
        Debug.Log($"ContaminationHUD update | multiplier: {multiplier} | contaminated: {isContaminated}");
        UpdateVisuals(isContaminated);
    }

    private void UpdateVisuals(bool isContaminated)
    {
        if (textContainer != null)
            textContainer.SetActive(contaminationText != null);

        if (contaminationText != null)
            contaminationText.text = isContaminated ? contaminatedText : normalText;

        if (normalIconObject != null || riskIconObject != null)
        {
            if (normalIconObject != null)
                normalIconObject.SetActive(!isContaminated);

            if (riskIconObject != null)
                riskIconObject.SetActive(isContaminated);
        }
        else if (purityIcon != null)
        {
            Sprite targetSprite = isContaminated ? riskIcon : normalIcon;
            purityIcon.sprite = targetSprite;
            purityIcon.overrideSprite = targetSprite;
            purityIcon.enabled = targetSprite != null;
            purityIcon.SetAllDirty();
        }
    }

    public void RefreshFromCurrentState()
    {
        if (PackingArea.Instance == null)
            return;

        float multiplier = PackingArea.Instance.contaminationMultiplier;
        lastMultiplier = multiplier;

        bool isContaminated = multiplier < contaminatedThreshold;
        Debug.Log($"ContaminationHUD refresh | multiplier: {multiplier} | contaminated: {isContaminated}");
        UpdateVisuals(isContaminated);
    }
}
