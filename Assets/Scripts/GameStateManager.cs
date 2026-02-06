using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public bool IsGameOver => currentState == GameState.Lost;
    public enum GameState
    {
        Playing,
        Lost
    }

    [Header("References")]
    [Tooltip("Reference to the ThroughputAggregator that reports total line throughput")]
    public ThroughputAggregator throughputAggregator;

    [Header("Game Over UI (optional)")]
    [Tooltip("Optional: a GameObject (e.g., a Canvas/Panel) to enable on Game Over")]
    public GameObject gameOverRoot;

    [Tooltip("Optional: reuse the existing EventPopupUI to show a Game Over message")]
    public EventPopupUI gameOverPopup;

    [Tooltip("Optional: reference to EventManager so we can hide its active popup on Game Over")]
    public EventManager eventManager;

    [Header("Lose Condition")]
    [Tooltip("How many seconds the line can be stalled (0 throughput) before the player loses")]
    public float stallSecondsToLose = 20f;

    private GameState currentState = GameState.Playing;
    private float stallTimer = 0f;

    [Header("Debug")]
    public bool debugLog = false;

    private bool hasEverRun = false;

    private void Start()
    {
        // Safety: ensure we are not left paused from a previous Game Over run.
        Time.timeScale = 1f;
        currentState = GameState.Playing;
        stallTimer = 0f;
        hasEverRun = false;
    }

    private void Update()
    {
        if (debugLog) Debug.Log($"[GSM] timeScale={Time.timeScale} time={Time.time:0.00}");
        if (currentState != GameState.Playing)
            return;

        if (throughputAggregator == null)
            return;

        float totalTph = throughputAggregator.EffectiveThroughputTPH;

        // Arm the lose condition once the full line is installed OR once it has run.
        bool fullLineInstalled = false;
        if (throughputAggregator.machineSlots != null && throughputAggregator.machineSlots.Count > 0)
        {
            fullLineInstalled = true;
            foreach (var slot in throughputAggregator.machineSlots)
            {
                if (slot == null || !slot.HasMachineInstalled)
                {
                    fullLineInstalled = false;
                    break;
                }
            }
        }

        if (totalTph > 0f || fullLineInstalled)
        {
            hasEverRun = true;
        }

        if (!hasEverRun)
        {
            if (debugLog)
                Debug.Log($"[GameStateManager] Waiting for line to start or be fully assembled... EffectiveThroughputTPH={totalTph:0.###}, fullLineInstalled={fullLineInstalled}");
            return;
        }

        // If total throughput is zero or less, the line is stalled
        if (totalTph <= 0f)
        {
            stallTimer += Time.deltaTime;
            if (debugLog) Debug.Log($"[GameStateManager] Line stalled. t={stallTimer:0.00}/{stallSecondsToLose:0.00}s");

            if (stallTimer >= stallSecondsToLose)
            {
                Lose("Line stalled too long");
            }
        }
        else
        {
            if (stallTimer > 0f && debugLog) Debug.Log("[GameStateManager] Line recovered. Resetting stall timer.");
            // Line recovered, reset stall timer
            stallTimer = 0f;
        }
    }

    public void Lose(string reason)
    {
        if (currentState == GameState.Lost)
            return;

        currentState = GameState.Lost;

        Debug.Log($"GAME OVER: {reason}");

        // Ensure any existing event popup is hidden before showing Game Over
        if (eventManager != null && eventManager.popupUI != null)
        {
            eventManager.popupUI.Hide();
        }

        if (gameOverPopup != null)
        {
            gameOverPopup.Hide();
        }

        // If a dedicated root panel is provided, ensure it renders on top.
        if (gameOverRoot != null)
        {
            gameOverRoot.SetActive(true);
            gameOverRoot.transform.SetAsLastSibling();
        }
        else if (gameOverPopup != null)
        {
            // Reuse the popup system if no dedicated Game Over canvas exists yet.
            gameOverPopup.Show(
                "Game Over",
                reason,
                "OK",
                () => gameOverPopup.Hide(),
                "",
                null
            );
            gameOverPopup.transform.SetAsLastSibling();
        }

        // V1 behaviour: pause the game
        Time.timeScale = 0f;

        // Later: trigger Game Over UI, analytics, restart options, etc.
    }
}