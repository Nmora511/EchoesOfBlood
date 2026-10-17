using UnityEngine;
using Enemies;
using TMPro;
using UnityEngine.UI;
using DoorClass = Door.Door;
using PlayerClass = Player.Player;

public class RoomManager : MonoBehaviour
{
    [Header("Room Settings")]
    public float SurviveTime = 120f;
    
    public TextMeshProUGUI CountdownText;
    
    private bool _isRoomCompleted = false;

    private void Update()
    {
        if (_isRoomCompleted) return;

        CountdownText.text = $"SOBREVIVA - {FormatTime(SurviveTime)}!";
        
        if (SurviveTime > 0 && PlayerClass.PlayerInstance is not null && PlayerClass.PlayerInstance.HealthPoints > 0)
        {
            SurviveTime -= Time.deltaTime;
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
            return;
        }
        
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
            // Opcional: Pode verificar se quer abrir só as saídas com:
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