using Evolution;
using Evolution.Rooms;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("Evolution (GA, fitness, player model)")]
    public EvolutionSettings EvolutionSettings = new EvolutionSettings();

    [Header("Random")]
    [Tooltip("Seed of a NEW participant. -1 = a different seed each new participant. " +
             "Once a participant exists, its saved seed is used and this value is ignored.")]
    public int Seed = -1;

    [Header("Participant / Save")]
    [Tooltip("ID of the person playing. Each ID has its own folder in <persistentDataPath>/Participants, " +
             "with its own population, profile and validation log.")]
    public string ParticipantId = "dev";

    [Tooltip("On Play, moves this participant's folder to <folder>_old_<date> and starts from zero. " +
             "Useful for tests. Turn it OFF for the real playtests.")]
    public bool ResetProgressOnStart = false;

    [Header("Telemetry")]
    [Tooltip("An enemy closer than this (meters) counts as 'near the player'.")]
    public float TelemetryNearRadius = 4f;

    [Tooltip("Health fraction below which the player counts as 'low health' (0.3 = 30%).")]
    [Range(0.05f, 0.9f)] public float LowHealthThreshold = 0.3f;

    [Tooltip("Below this speed (m/s) the player counts as standing still.")]
    public float StillSpeed = 0.5f;

    [Tooltip("Prints a summary of each room's telemetry in the Console when the room ends.")]
    public bool LogTelemetry = true;

    [Tooltip("When a run ends, writes runs/run_NNN.json and the CSVs (runs, rooms, generations) in the participant folder.")]
    [FormerlySerializedAs("SaveTelemetryFiles")]
    public bool WriteValidationLog = true;

    [Tooltip("Prints predicted vs observed per room and the convergence of the GA in the Console after each run.")]
    public bool LogEvolution = true;
    
    [Header("Debug")]
    [Tooltip("Prints each room's map and values in the Console when it loads.")]
    public bool LogRoomLayout = true;
}