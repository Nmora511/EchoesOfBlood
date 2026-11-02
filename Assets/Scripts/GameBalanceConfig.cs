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

    [Header("Telemetry")]
    [Tooltip("An enemy closer than this (meters) counts as 'near the player'.")]
    public float TelemetryNearRadius = 4f;

    [Tooltip("Health fraction below which the player counts as 'low health' (0.3 = 30%).")]
    [Range(0.05f, 0.9f)] public float LowHealthThreshold = 0.3f;

    [Tooltip("Below this speed (m/s) the player counts as standing still.")]
    public float StillSpeed = 0.5f;

    [Tooltip("Prints a summary of each room's telemetry in the Console when the room ends.")]
    public bool LogTelemetry = true;

    [Tooltip("Writes the run's telemetry as JSON in <persistentDataPath>/Telemetry after every room.")]
    public bool SaveTelemetryFiles = true;
    
    [Header("Debug")]
    [Tooltip("Prints each room's map and values in the Console when it loads.")]
    public bool LogRoomLayout = true;
}