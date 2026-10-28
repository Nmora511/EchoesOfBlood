using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Controls the scene sequence of a run: 5x Default_Room, then Boss_Room.
public class RunManager : MonoBehaviour
{
    public const string RoomScene = "Default_Room";
    public const string BossScene = "Boss_Room";
    public const int RoomsPerRun = 5;

    public static RunManager Instance { get; private set; }

    // 0..4 = regular rooms, 5 = boss room.
    public int RoomIndex { get; private set; }
    public int RunNumber { get; private set; } = 1;
    public bool IsBossRoom => RoomIndex >= RoomsPerRun;

    private readonly List<string> _roomResults = new();

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

        // Allows pressing Play directly in Boss_Room to test the boss.
        RoomIndex = SceneManager.GetActiveScene().name == BossScene ? RoomsPerRun : 0;
    }

    // Called by RoomManager when the room timer ends and the player survived.
    public void OnRoomCompleted()
    {
        _roomResults.Add($"Room number {RoomIndex + 1}: completed");
        // TODO Stage 3: the room's RoomTelemetry goes here.
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
        // TODO Stage 5: evolution (room GA and boss GE) runs here before the next run.
    }

    // Called by the restart button (game over) and after beating the boss.
    public void StartNewRun()
    {
        RunNumber++;
        RoomIndex = 0;
        _roomResults.Clear();
        SceneManager.LoadScene(RoomScene);
    }
}
