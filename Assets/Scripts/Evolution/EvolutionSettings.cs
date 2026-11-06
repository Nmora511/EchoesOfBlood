using System;

namespace Evolution
{
    // Every tunable number of the evolutionary system. GameBalanceConfig holds one of these,
    // so all of it shows up in the Inspector.
    [Serializable]
    public class EvolutionSettings
    {
        public RoomGaSettings RoomGa = new RoomGaSettings();
        public RoomFitnessSettings RoomFitness = new RoomFitnessSettings();
        public DifficultyModelSettings Model = new DifficultyModelSettings();
        public ProfileSettings Profile = new ProfileSettings();
        public BaseStats BaseStats = new BaseStats();
    }

    [Serializable]
    public class RoomGaSettings
    {
        public int PopulationSize = 30;
        public int Generations = 50;          // the "x generations" run between two runs
        public int Elitism = 2;
        public int TournamentSize = 3;
        public float CrossoverRate = 0.8f;

        // Chance that a room of the child mixes both parents block by block, instead of being copied whole from one.
        public float BlockCrossoverRate = 0.5f;

        // Chance, per active cell, of changing its type (about 1/81).
        public float CellMutationRate = 0.0125f;

        // Chance, per room, of swapping two active cells (moves a spawner or trap without changing the counts).
        public float SwapMutationRate = 0.2f;

        // Chance, per attribute gene, of adding gaussian noise with this standard deviation.
        public float AttributeMutationRate = 0.2f;
        public float AttributeSigma = 0.1f;

        // Random individuals that replace the worst ones before each evolution, to keep diversity
        // (the target moves with the player, so an old converged population can get stuck).
        public int RandomImmigrants = 3;

        // Chance that a random cell is empty in random individuals.
        public float RandomEmptyChance = 0.75f;
    }

    [Serializable]
    public class RoomFitnessSettings
    {
        // Target health fraction the player should lose in rooms 1..5 (before multiplying by delta).
        // One hit of a base enemy takes 20% of the player's 100 HP, so values below ~0.2 mean "almost no hit".
        public float[] TargetCurve = { 0.20f, 0.30f, 0.40f, 0.50f, 0.60f };

        // A room predicted to take more than this fraction of health is predicted to kill the player.
        public float LethalThreshold = 1f;
        public float LethalPenalty = 1f;      // per unit of predicted damage above the threshold

        // Weight of the novelty bonus (0..1 distance to the rooms of the last runs).
        public float NoveltyWeight = 0.05f;
        public int RecentLayoutsKept = 15;    // 3 runs of 5 rooms
    }

    [Serializable]
    public class DifficultyModelSettings
    {
        // Traps farther than 1 cell from the safe path count this much compared to the ones near it.
        public float TrapFarWeight = 0.3f;

        // How many enemies can really attack the player at the same time. With many enemies alive,
        // the effective number of attackers saturates at this value (they block each other, the player
        // only fights a few at once). Effective = C * (1 - (1 - 1/C)^alive), from the occupancy problem.
        // Values below 1 are treated as 1.
        public float CrowdCapacity = 3f;
    }

    [Serializable]
    public class ProfileSettings
    {
        // Weight of the newest room in the moving averages (0.3 = recent rooms matter more).
        public float Alpha = 0.3f;

        // A source is only recalibrated when its exposure in the played room was at least this much,
        // so a room with a single far trap does not throw the trap weight around.
        public float MinExposure = 0.05f;
        public float MaxWeight = 1.5f;

        // Initial "fraction of the potential damage that lands", per source.
        public float DefaultEnemyWeight = 0.02f;
        public float DefaultArrowWeight = 0.02f;
        public float DefaultTrapWeight = 0.3f;

        // Initial lifetime (seconds) of an enemy with health multiplier 1.
        public float DefaultGroundLifetime = 10f;
        public float DefaultRangedLifetime = 12f;

        // Delta (global difficulty scale): goes down when the player dies in a room,
        // up when all 5 rooms were survived with at least ComfortMargin of health on average.
        public float DeltaStep = 0.1f;
        public float DeltaMin = 0.5f;
        public float DeltaMax = 2f;
        public float ComfortMargin = 0.5f;
    }

    // Values of the prefabs the model needs. Keep them equal to the prefabs.
    // If they drift, the calibrated weights absorb the difference, but the weights stop being "hit rates".
    [Serializable]
    public class BaseStats
    {
        public float PlayerMaxHealth = 100f;   // Player.prefab HealthPoints
        public float GroundDamage = 20f;       // Ground_Enemy.prefab AttackDamage
        public float ArrowDamage = 10f;        // Ranged_Enemy.prefab AttackDamage (copied to the arrow)
        public float ThornDamage = 10f;        // thorn_trap.prefab Trap.Damage
        public float StakesDamage = 10f;       // stake_trap.prefab Trap.Damage
        public float FirstSpawnDelay = 5f;     // EnemySpawner waits this long before the first spawn
    }
}
