using UnityEngine;
using Enemies;
using Evolution.Telemetry;
using Telemetry;
using TMPro;
using UnityEngine.UI;
using DoorClass = Door.Door;
using PlayerClass = Player.Player;

public class RoomManager : MonoBehaviour
{
    [Header("Room Settings")]
    public float SurviveTime = 120f;
    
    [Tooltip("When checked, uses the SurviveTime above instead of the one from the genome (useful for quick tests).")]
    public bool OverrideSurviveTime = false;
        
    public TextMeshProUGUI CountdownText;
    
    private bool _isRoomCompleted = false;

    private void Start()
    {
        var room = RunManager.Instance.CurrentRoom;
        if (!OverrideSurviveTime && room != null)
        {
            SurviveTime = room.Settings.SurviveTime;
        }
        
        // The player's Awake already ran (all Awakes run before any Start), and nothing hit it yet,
        // so its current HealthPoints is the max health of this room.
        var player = PlayerClass.PlayerInstance;
        GameTelemetry.BeginRoom(
            RunManager.Instance.RunNumber,
            RunManager.Instance.RoomIndex,
            room,
            SurviveTime,
            player != null ? player.HealthPoints : 0f,
            RunManager.Instance.Config);
    }
    
    private void Update()
    {
        if (_isRoomCompleted) return;

        var roomNumber = RunManager.Instance.RoomIndex + 1;
        CountdownText.text = $"SALA {roomNumber}/{RunManager.RoomsPerRun} - SOBREVIVA - {FormatTime(SurviveTime)}!";
        
        if (SurviveTime > 0 && PlayerClass.PlayerInstance is not null && PlayerClass.PlayerInstance.HealthPoints > 0)
        {
            SurviveTime -= Time.deltaTime;
            GameTelemetry.Tick(Time.deltaTime);
        }
        else
        {
            FinishRoom();
        }
    }

    private void FinishRoom()
    {
        _isRoomCompleted = true;

        if (PlayerClass.PlayerInstance is null || PlayerClass.PlayerInstance.HealthPoints <= 0)
        {
            RunManager.Instance.OnRoomEnded(GameTelemetry.EndRoom(RoomOutcome.Died));
            return;
        }
        
        // Ends the telemetry before killing the enemies below, so those deaths are not counted as kills.
        RunManager.Instance.OnRoomEnded(GameTelemetry.EndRoom(RoomOutcome.Survived));
        
        var enemySpawners = FindObjectsByType<EnemySpawner>();
        foreach (var spawner in enemySpawners)
        {
            spawner.StopSpawning();
        }
        
        var fsmEnemies = FindObjectsByType<FsmEnemy>();
        foreach (var enemy in fsmEnemies)
        {
            enemy.Death();
        }

        var doors = FindObjectsByType<DoorClass>();
        foreach (var door in doors)
        {
            if (door.DoorType == DoorClass.DoorTypeEnum.Exit)
            {
                door.Open();
            }
        }
    }

    private string FormatTime(float time)
    {
        var minutes = (int)time / 60;
        var seconds = (int)time % 60;

        return $"{minutes:D2}:{seconds:D2}";
    }
}