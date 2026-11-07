using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evolution.Cycle;
using Evolution.Persistence;
using Evolution.Rooms;
using Evolution.Telemetry;

// Controls the scene sequence of a run (5x Default_Room, then Boss_Room) and, between runs,
// the evolutionary cycle: telemetry -> profile -> evolution -> next run, saved to disk.
//
// Save rules (see PENDENCIAS.md):
// - During a run nothing is written to disk. The telemetry stays in memory.
// - When the run ends (death or victory) the cycle runs in a Task and then writes, at once,
//   the validation log and the state (profile, delta, population and the plan of the NEXT run).
// - If the game is closed in the middle of a run, nothing was written: reopening loads the same plan
//   and the player starts the same run again from room 1.
public class RunManager : MonoBehaviour
{
    public const string RoomScene = "Default_Room";
    public const string BossScene = "Boss_Room";
    public const int RoomsPerRun = 5;

    public static RunManager Instance { get; private set; }

    public GameBalanceConfig Config { get; private set; }

    // 0..4 = regular rooms, 5 = boss room.
    public int RoomIndex { get; private set; }
    public int RunNumber => _state.RunNumber;
    public bool IsBossRoom => RoomIndex >= RoomsPerRun;
    public int Seed => _state.Seed;
    public string ParticipantFolder => _folder;

    // Telemetry of the run being played, and of the last finished run.
    public RunTelemetry CurrentRunTelemetry { get; private set; }
    public RunTelemetry LastRunTelemetry { get; private set; }

    // Result of the last cycle (predicted vs observed, convergence...), null before the first one.
    public RunReport LastReport { get; private set; }

    // True while the evolution of the last run is still running in the background.
    public bool IsEvolving => _evolution != null && !_evolution.IsCompleted;

    // 0..1, for a progress bar or a "Evolving..." text if you want one.
    public float EvolutionProgress =>
        _evolution == null ? 1f : Mathf.Clamp01((float)_generationsDone / Mathf.Max(1, _cycle.Evolution.RoomGa.Generations));

    // The room the current Default_Room must build (null in the boss room).
    public RoomPlan CurrentRoom => IsBossRoom ? null : _state.CurrentPlan.Rooms[RoomIndex];

    private EvolutionState _state;
    private CycleSettings _cycle;
    private string _folder;

    private Task<RunReport> _evolution;
    private int _generationsDone;
    private bool _runEnded;
    private bool _startingRun;

    // Unity calls this method automatically on Play, before any scene is loaded.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("RunManager");
        go.AddComponent<RunManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Config = Resources.Load<GameBalanceConfig>("GameBalanceConfig");
        if (Config == null)
        {
            Debug.LogWarning("[RunManager] Assets/Resources/GameBalanceConfig not found, using default values.");
            Config = ScriptableObject.CreateInstance<GameBalanceConfig>();
        }

        _cycle = new CycleSettings
        {
            Evolution = Config.EvolutionSettings,
            Ranges = Config.RoomRanges,
            Rules = Config.RepairRules,
            MinBlocksPerRoom = Config.MinBlocksPerRoom
        };

        // Application.persistentDataPath can only be read on the main thread, so the folder is resolved here.
        _folder = Path.Combine(Application.persistentDataPath, "Participants",
            StateStore.SafeFolderName(Config.ParticipantId));

        LoadOrCreateState();
        CurrentRunTelemetry = new RunTelemetry(RunNumber, Seed);

        // Allows pressing Play directly in Boss_Room to test the boss.
        RoomIndex = SceneManager.GetActiveScene().name == BossScene ? RoomsPerRun : 0;
    }

    private void Update()
    {
        // Shows the result as soon as the evolution finishes, even if the player is still on the Game Over screen.
        if (_evolution != null && _evolution.IsCompleted) FinishEvolution();
    }

    // Called by RoomManager when the room ends (timer over or player died).
    public void OnRoomEnded(RoomTelemetry telemetry)
    {
        if (telemetry == null) return;

        CurrentRunTelemetry.AddRoom(telemetry);
        if (Config.LogTelemetry) Debug.Log($"[Telemetry] {telemetry.Summary()}");

        // Death ends the run right away, so the evolution runs while the Game Over screen is shown.
        if (telemetry.Outcome == RoomOutcome.Died) OnRunEnded(false);
    }

    // Called by the exit door.
    public void LoadNext()
    {
        if (IsBossRoom)
        {
            OnRunEnded(true);
            StartNewRun();
            return;
        }

        RoomIndex++;
        SceneManager.LoadScene(IsBossRoom ? BossScene : RoomScene);
    }

    // Called when the player dies (won = false) or leaves the boss room (won = true).
    // Safe to call more than once: only the first call of a run counts.
    public void OnRunEnded(bool won)
    {
        if (_runEnded) return;
        _runEnded = true;

        Debug.Log($"[RunManager] Run {RunNumber} ended. Won: {won}. Died/Ended in room {RoomIndex + 1}.");
        CurrentRunTelemetry.Finish(won, RoomIndex);
        LastRunTelemetry = CurrentRunTelemetry;

        if (CurrentRunTelemetry.Rooms.Count == 0)
        {
            // Happens when Play was pressed directly in Boss_Room: nothing to learn from.
            Debug.Log("[RunManager] No room telemetry in this run. It does not count: the same run will be played again.");
            return;
        }

        // Everything the Task needs is captured here, on the main thread. The Task works on a COPY of the
        // state, so the game never reads a half-evolved state. If anything fails, the copy is thrown away.
        var working = _state.Clone(_cycle.Ranges);
        var run = CurrentRunTelemetry;
        var cycle = _cycle;
        var folder = _folder;
        var writeLog = Config.WriteValidationLog;
        _generationsDone = 0;

        _evolution = Task.Run(() =>
        {
            var report = RunCycle.CompleteRun(working, run, cycle,
                stats => Interlocked.Exchange(ref _generationsDone, stats.Generation));

            // The log first and the state last: the state file is the "commit". If the game closes between
            // the two, the run is played again and run_NNN.json / the CSV lines of this run appear twice
            // (same run number, keep the last one).
            if (writeLog)
            {
                try
                {
                    new ValidationLog(folder).Write(report);
                }
                catch (Exception e)
                {
                    report.LogError = e.Message;
                }
            }

            StateStore.Save(folder, report.State);
            return report;
        });
    }

    // Called by the restart button (game over) and after beating the boss.
    // Waits for the evolution (usually well under a second) and then loads room 1 of the next run.
    public void StartNewRun()
    {
        if (_startingRun) return;
        StartCoroutine(StartNewRunWhenReady());
    }

    private IEnumerator StartNewRunWhenReady()
    {
        _startingRun = true;

        var task = _evolution;
        if (task != null)
        {
            if (!task.IsCompleted) Debug.Log("[Evolution] Waiting for the evolution to finish...");
            while (!task.IsCompleted) yield return null; // also works with Time.timeScale = 0
            if (_evolution == task) FinishEvolution();  // Update may have done it already
        }

        RoomIndex = 0;
        _runEnded = false;
        CurrentRunTelemetry = new RunTelemetry(RunNumber, Seed);
        _startingRun = false;
        SceneManager.LoadScene(RoomScene);
    }

    private void FinishEvolution()
    {
        var task = _evolution;
        _evolution = null;

        if (task.IsFaulted || task.IsCanceled)
        {
            // Nothing was saved and _state is untouched: the same run is played again.
            Debug.LogError("[Evolution] The evolution failed, the same run will be played again.");
            if (task.Exception != null) Debug.LogException(task.Exception.GetBaseException());
            return;
        }

        var report = task.Result;
        _state = report.State;
        LastReport = report;

        Debug.Log($"[Evolution] {report.Summary()}");
        if (report.LogError != null) Debug.LogWarning($"[Evolution] Could not write the validation log: {report.LogError}");

        if (Config.LogEvolution)
        {
            foreach (var room in report.Rooms)
            {
                Debug.Log($"[Evolution] Room {room.RoomIndex + 1}: target {room.Target:0.00}, predicted {room.PredictedAtPlayedTime:0.00}, " +
                          $"observed {room.Observed:0.00} (error {room.Error:+0.00;-0.00}, {room.Outcome}, {room.TimeSurvived:0}/{room.PlannedTime:0}s)");
            }

            var generations = report.Evolution.Generations;
            var step = Mathf.Max(1, generations.Count / 5);
            for (var i = 0; i < generations.Count; i += step) Debug.Log($"[Evolution] {generations[i]}");
            Debug.Log($"[Evolution] {generations[generations.Count - 1]}");
            Debug.Log($"[Evolution] Profile: {report.ProfileAfter}");
            Debug.Log($"[Evolution] Saved to {_folder}");
        }
    }

    // Closing the game while the evolution is running: wait for it, so the finished run is not lost.
    private void OnApplicationQuit()
    {
        if (_evolution == null || _evolution.IsCompleted) return;
        try
        {
            _evolution.Wait(TimeSpan.FromSeconds(10));
        }
        catch (AggregateException)
        {
            // Already reported nowhere else, but the state on disk is still the previous, valid one.
        }
    }

    private void LoadOrCreateState()
    {
        if (Config.ResetProgressOnStart)
        {
            try
            {
                var archived = StateStore.Archive(_folder);
                if (archived != null) Debug.Log($"[RunManager] Reset progress: old data moved to {archived}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunManager] Could not move the old data: {e.Message}");
            }
        }

        try
        {
            _state = StateStore.Load(_folder, Config.RoomRanges, out var warning);
            if (warning != null) Debug.LogWarning($"[RunManager] {warning}");
        }
        catch (Exception e)
        {
            // Both the save and its backup are broken. Keep the files for inspection and start a new participant.
            Debug.LogError($"[RunManager] {e.Message} Starting from zero; the broken files were moved away.");
            try { StateStore.Archive(_folder); } catch (Exception) { /* nothing else to do */ }
            _state = null;
        }

        if (_state != null)
        {
            Debug.Log($"[RunManager] Participant '{_state.ParticipantId}': loaded run {_state.RunNumber} " +
                      $"(generation {_state.TotalGenerations}, seed {_state.Seed}) from {_folder}");
            return;
        }

        // First time this participant plays: create, evolve with the default profile and save the plan
        // of run 1 BEFORE it starts (it takes a fraction of a second, on the main thread).
        var seed = Config.Seed >= 0 ? Config.Seed : Environment.TickCount & int.MaxValue;
        _state = RunCycle.CreateState(Config.ParticipantId, seed, _cycle, out var initial);
        Debug.Log($"[Evolution] Run 1 evolved with the default profile: generations 0->{_state.TotalGenerations} " +
                  $"in {initial.ElapsedMs:0} ms, fitness {initial.Generations[0].Best:0.000} -> {_state.CurrentPlanFitness:0.000}");
        try
        {
            StateStore.Save(_folder, _state);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RunManager] Could not save the new participant: {e.Message}");
        }
        Debug.Log($"[RunManager] New participant '{_state.ParticipantId}', seed {seed}. Saved to {_folder}");
    }
}
