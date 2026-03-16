using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterText : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [TextArea(3, 8)]
    [SerializeField] private string fullText;
    [SerializeField] private float charactersPerSecond = 30f;

    private Coroutine typingCoroutine;
    private bool isTyping;

    private void OnEnable()
    {
        StartTyping();
    }

    public void StartTyping()
    {
        if (targetText == null)
            return;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeTextRoutine());
    }

    public void ShowAllText()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        targetText.text = fullText;
        isTyping = false;
    }

    private IEnumerator TypeTextRoutine()
    {
        isTyping = true;
        targetText.text = "";

        float delay = 1f / charactersPerSecond;

        foreach (char c in fullText)
        {
            targetText.text += c;
            yield return new WaitForSeconds(delay);
        }

        isTyping = false;
        typingCoroutine = null;
    }

    public bool IsTyping()
    {
        return isTyping;
    }
}