using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private GameObject[] pages;

    [Header("Navigation Buttons")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button startGameButton;

    private int currentPageIndex = 0;

    private void Start()
    {
        ShowPage(0);
    }

    public void NextPage()
    {
        if (currentPageIndex < pages.Length - 1)
        {
            ShowPage(currentPageIndex + 1);
        }
    }

    public void PreviousPage()
    {
        if (currentPageIndex > 0)
        {
            ShowPage(currentPageIndex - 1);
        }
    }

    private void ShowPage(int index)
    {
        currentPageIndex = index;

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
            {
                pages[i].SetActive(i == currentPageIndex);
            }
        }

        if (backButton != null)
            backButton.gameObject.SetActive(currentPageIndex > 0);

        if (nextButton != null)
            nextButton.gameObject.SetActive(currentPageIndex < pages.Length - 1);

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(currentPageIndex == pages.Length - 1);
    }
}