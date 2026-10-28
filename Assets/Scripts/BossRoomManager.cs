using Enemies;
using UnityEngine;
using DoorClass = Door.Door;

// Lives on an empty GameObject inside Boss_Room.
// When the boss dies, it opens the exit door; walking through it ends the run as a win.
public class BossRoomManager : MonoBehaviour
{
    private BossEnemy _boss;

    private void Start()
    {
        _boss = FindAnyObjectByType<BossEnemy>();
        if (_boss == null)
        {
            Debug.LogError("BossRoomManager: no BossEnemy in scene.");
            return;
        }

        _boss.Died += OnBossDied;
    }

    private void OnDestroy()
    {
        if (_boss != null) _boss.Died -= OnBossDied;
    }

    private void OnBossDied()
    {
        foreach (var door in FindObjectsByType<DoorClass>())
        {
            if (door.DoorType == DoorClass.DoorTypeEnum.Exit)
            {
                door.Open();
            }
        }
    }
}