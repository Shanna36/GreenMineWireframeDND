using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuLoader : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string menuSceneName = "Splash Screen";
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private string gameSceneName = "MainScene";

    public void LoadMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    public void LoadTutorial()
    {
        SceneManager.LoadScene(tutorialSceneName);
    }

    public void LoadGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ReloadCurrentScene()
    {
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.name);
    }

    public void QuitGame()
    {
        Debug.Log("QuitGame called.");
        Application.Quit();
    }
}