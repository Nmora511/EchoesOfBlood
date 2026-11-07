using System;
using System.Collections.Generic;
using Evolution.PlayerModel;
using Evolution.Rooms;

namespace Evolution.Persistence
{
    // Everything that must survive closing the game, for ONE participant.
    // It is only written when a run ends (never in the middle of a run), so reopening the game
    // after quitting mid-run loads the same plan and the player starts the same run again from room 1.
    [Serializable]
    public class EvolutionState
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string ParticipantId = "dev";

        // Seed chosen when the participant was created (identifies the experiment).
        public int Seed;

        // Seed of the next evolution step. Every step creates new Random(NextSeed) and, at the end,
        // draws the following NextSeed from it. System.Random can not be saved, so this is how a
        // closed and reopened game continues with exactly the same random sequence.
        public int NextSeed;

        public int RunNumber = 1;          // the run that will be played next (= CurrentPlan)
        public int TotalGenerations;       // generations evolved so far (0 = initial random population)

        public PlayerProfile Profile;

        // The current generation of the room GA, best first. The next evolution starts from it.
        public List<RunGenome> Population = new List<RunGenome>();

        // Novelty archive: layouts of the last rooms played, most recent last.
        public List<CellType[]> RecentLayouts = new List<CellType[]>();

        // The run to be played, and where it came from.
        public RunPlan CurrentPlan;
        public double CurrentPlanFitness;
        public int CurrentPlanGeneration;

        public string CreatedAt;
        public string UpdatedAt;

        // Deep copy, used so the evolution (in a Task) never touches the state the game is reading.
        public EvolutionState Clone(RoomAttributeRanges ranges) =>
            StateSerializer.FromJson(StateSerializer.ToJson(this), ranges);
    }
}
