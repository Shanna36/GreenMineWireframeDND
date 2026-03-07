using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public bool IsGameOver => currentState == GameState.Lost;

    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    [Header("References")]
    [Tooltip("Reference to the ThroughputAggregator that reports total line throughput")]
    public ThroughputAggregator throughputAggregator;

    [Header("Game Over UI")]
    [Tooltip("A GameObject (e.g., a Canvas/Panel) to enable on Game Over")]
    public GameObject gameOverRoot;

    [Tooltip("Reference to EventManager so we can hide its active popup on Game Over")]
    public EventManager eventManager;

    [Header("Lose Condition")]
    [Tooltip("How many seconds the line can be stalled (0 throughput) before the player loses")]
    public float stallSecondsToLose = 20f;

    [Header("Win Condition")]
    [Tooltip("Number of premium machines required to win the game")]
    public int premiumMachinesToWin = 3;

    private GameState currentState = GameState.Playing;
    private float stallTimer = 0f;

    [Header("Debug")]
    public bool debugLog = true;

    private bool hasEverRun = false;

    // Throttled diagnostics (unscaled) so I can see early-return causes even if the Console is noisy.
    private float _nextDiagUnscaledTime = 0f;

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
        {
            if (Time.unscaledTime >= _nextDiagUnscaledTime)
            {
                _nextDiagUnscaledTime = Time.unscaledTime + 1f;
                Debug.LogWarning("[GSM] throughputAggregator reference is NULL. Lose condition cannot run.");
            }
            return;
        }

        float totalTph = throughputAggregator.EffectiveThroughputTPH;

        // Hard-stall rule: if any required machine slot is missing a machine or is non-operational,
        // treat the whole line as stalled (throughput = 0). This matches the V1 "full line" model.
        if (throughputAggregator.machineSlots != null && throughputAggregator.machineSlots.Count > 0)
        {
            foreach (var slot in throughputAggregator.machineSlots)
            {
                if (slot == null || !slot.HasMachineInstalled || !slot.IsOperational)
                {
                    totalTph = 0f;
                    break;
                }
            }
        }

        if (Time.unscaledTime >= _nextDiagUnscaledTime)
        {
            _nextDiagUnscaledTime = Time.unscaledTime + 1f;
            int slotCount = (throughputAggregator.machineSlots != null) ? throughputAggregator.machineSlots.Count : 0;
            Debug.LogWarning($"[GSM] diag: state={currentState} hasEverRun={hasEverRun} totalTph={totalTph:0.###} stallTimer={stallTimer:0.00} timeScale={Time.timeScale} slots={slotCount}");
        }

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
            if (Time.unscaledTime >= _nextDiagUnscaledTime)
            {
                _nextDiagUnscaledTime = Time.unscaledTime + 1f;
                Debug.LogWarning($"[GSM] Not armed yet. totalTph={totalTph:0.###} fullLineInstalled={fullLineInstalled} machineSlotsCount={(throughputAggregator.machineSlots != null ? throughputAggregator.machineSlots.Count : 0)}");
            }
            return;
        }

        // Win condition: player installs enough premium machines
        if (throughputAggregator.machineSlots != null)
        {
            int premiumCount = 0;

            foreach (var slot in throughputAggregator.machineSlots)
            {
                if (slot != null && slot.CurrentIndex == 2) // 2 = Premium
                {
                    premiumCount++;
                }
            }

            if (premiumCount >= premiumMachinesToWin)
            {
                Win();
                return;
            }
        }

        // If total throughput is zero or less, the line is stalled
        if (totalTph <= 0f)
        {
            // Use unscaled time so the stall/lose timer still runs even if UI pauses timeScale.
            stallTimer += Time.unscaledDeltaTime;
            if (debugLog) Debug.Log($"[GameStateManager] Line stalled. t={stallTimer:0.00}/{stallSecondsToLose:0.00}s");

            if (stallTimer >= stallSecondsToLose)
            {
                Lose("Line stalled too long");
            }
        }
        else
        {
            if (stallTimer > 0f && debugLog) Debug.Log("[GameStateManager] Line recovered. Resetting stall timer.");
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

        if (gameOverRoot != null)
        {
            gameOverRoot.SetActive(true);
            gameOverRoot.transform.SetAsLastSibling();
        }

        // Pause the game
        Time.timeScale = 0f;
    }

    public void Win()
    {
        if (currentState == GameState.Won)
            return;

        currentState = GameState.Won;

        Debug.Log("YOU WIN! Three premium machines installed.");

        // Hide any active event popup
        if (eventManager != null && eventManager.popupUI != null)
        {
            eventManager.popupUI.Hide();
        }

        if (gameOverRoot != null)
        {
            gameOverRoot.SetActive(true);
            gameOverRoot.transform.SetAsLastSibling();
        }

        // Pause the game
        Time.timeScale = 0f;
    }
}