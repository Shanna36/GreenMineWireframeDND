using UnityEngine;

public class FrameRateCap : MonoBehaviour
{
    [SerializeField] private int targetFps = 60;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;          // don't let vsync override the cap
        Application.targetFrameRate = targetFps; // cap FPS
    }
}