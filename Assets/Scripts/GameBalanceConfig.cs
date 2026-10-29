using Evolution.Rooms;
using UnityEngine;

// Tunable values of the adaptive system, editable in the Inspector.
[CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Echoes Of Blood/Game Balance Config")]
public class GameBalanceConfig : ScriptableObject
{
    [Header("Room Shape")]
    [Range(3, 9)] public int MinBlocksPerRoom = 5;

    [Header("Room Attributes (gene 0..1 -> value)")]
    public RoomAttributeRanges RoomRanges = new RoomAttributeRanges();

    [Header("Room Repair")]
    public RepairRules RepairRules = new RepairRules();

    [Header("Random")]
    [Tooltip("-1 = a new seed every run. Any other value makes the runs repeatable.")]
    public int Seed = -1;

    [Header("Debug")]
    [Tooltip("Prints each room's map and values in the Console when it loads.")]
    public bool LogRoomLayout = true;
}