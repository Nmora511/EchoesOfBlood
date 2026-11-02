using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evolution.Rooms;
using Evolution.Telemetry;

// Controls the scene sequence of a run: 5x Default_Room, then Boss_Room.
public class RunManager : MonoBehaviour
{
    public const string RoomScene = "Default_Room";
    public const string BossScene = "Boss_Room";
    public const int RoomsPerRun = 5;

    public static RunManager Instance { get; private set; }

    public GameBalanceConfig Config { get; private set; }
    
    // 0..4 = regular rooms, 5 = boss room.
    public int RoomIndex { get; private set; }
    public int RunNumber { get; private set; } = 1;
    public bool IsBossRoom => RoomIndex >= RoomsPerRun;
    public int Seed { get; private set; }

    // Telemetry of the run being played, and of the last finished run.
    public RunTelemetry CurrentRunTelemetry { get; private set; }
    public RunTelemetry LastRunTelemetry { get; private set; }

// The room the current Default_Room must build (null in the boss room).
    public RoomPlan CurrentRoom => IsBossRoom ? null : _plan.Rooms[RoomIndex];

    private RunPlan _plan;
    private System.Random _rng;
    private string _telemetryFolder;
    
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

        Seed = Config.Seed >= 0 ? Config.Seed : System.Environment.TickCount;
        _rng = new System.Random(Seed);
        Debug.Log($"[RunManager] Seed: {Seed}");

        // One folder per Play session, so a new session never overwrites the files of an old one.
        _telemetryFolder = Path.Combine(Application.persistentDataPath, "Telemetry",
            $"session_{DateTime.Now:yyyyMMdd_HHmmss}");
        
        CreatePlan();
        CurrentRunTelemetry = new RunTelemetry(RunNumber, Seed);
        
        // Allows pressing Play directly in Boss_Room to test the boss.
        RoomIndex = SceneManager.GetActiveScene().name == BossScene ? RoomsPerRun : 0;
    }

    // Called by RoomManager when the room timer ends and the player survived.
    public void OnRoomEnded(RoomTelemetry telemetry)
    {
        RecordRoom(telemetry);
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
    public void OnRunEnded(bool won)
    {
        Debug.Log($"[RunManager] Run {RunNumber} ended. Won: {won}. Died/Ended in room {RoomIndex + 1}.");
        CurrentRunTelemetry.Finish(won, RoomIndex);
        SaveRunTelemetry();
        LastRunTelemetry = CurrentRunTelemetry;
        // TODO Stage 5: evolution (room GA and boss GE) runs here before the next run.
    }

    // Called by the restart button (game over) and after beating the boss.
    public void StartNewRun()
    {
        RunNumber++;
        RoomIndex = 0;
        CreatePlan();
        CurrentRunTelemetry = new RunTelemetry(RunNumber, Seed);
        SceneManager.LoadScene(RoomScene);
    }
    
    private void CreatePlan()
    {
        _plan = RunPlan.CreateRandom(_rng, Config.RoomRanges, Config.RepairRules, Config.MinBlocksPerRoom);
    }
    
    private void RecordRoom(RoomTelemetry telemetry)
    {
        if (telemetry == null) return;

        CurrentRunTelemetry.AddRoom(telemetry);
        if (Config.LogTelemetry) Debug.Log($"[Telemetry] {telemetry.Summary()}");

        // Saved after every room, so the data is not lost if Play is stopped in the middle of a run.
        SaveRunTelemetry();
    }

    private void SaveRunTelemetry()
    {
        if (!Config.SaveTelemetryFiles || CurrentRunTelemetry == null) return;

        try
        {
            Directory.CreateDirectory(_telemetryFolder);
            var path = Path.Combine(_telemetryFolder, $"run_{CurrentRunTelemetry.RunNumber:000}.json");
            File.WriteAllText(path, CurrentRunTelemetry.ToJson());
            Debug.Log($"[Telemetry] Saved {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Telemetry] Could not save the telemetry file: {e.Message}");
        }
    }
}
