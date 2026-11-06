using System;
using System.Collections.Generic;
using System.Diagnostics;
using Evolution.Core;

namespace Evolution.Rooms
{
    [Serializable]
    public class RoomEvolutionResult
    {
        public List<Individual<RunGenome>> Population;   // last generation, best first
        public RunGenome Best;
        public RunPrediction BestPrediction;
        public List<GenerationStats> Generations = new List<GenerationStats>();
        public double ElapsedMs;
    }

    // One evolution step of the room GA, done between two runs:
    // the current generation is the initial population, it evolves for Settings.RoomGa.Generations
    // generations against the shapes of the next run, and the best individual becomes the next run.
    public static class RoomEvolution
    {
        // The 5 shapes of the next run (generated BEFORE the GA and fixed during it).
        public static bool[][] CreateShapes(Random rng, int minBlocks)
        {
            var shapes = new bool[RunGenome.RoomCount][];
            for (var i = 0; i < shapes.Length; i++) shapes[i] = RoomShape.Generate(rng, minBlocks);
            return shapes;
        }

        // Random, repaired population (first run of a player, or control group).
        public static List<RunGenome> CreatePopulation(RoomEvolutionContext context, Random rng)
        {
            var ga = context.Settings.RoomGa;
            var population = new List<RunGenome>(ga.PopulationSize);
            for (var i = 0; i < ga.PopulationSize; i++) population.Add(CreateRandom(context, rng));
            return population;
        }

        public static RoomEvolutionResult Evolve(IList<RunGenome> current, RoomEvolutionContext context, Random rng,
            Action<GenerationStats> reportGeneration = null)
        {
            var watch = Stopwatch.StartNew();
            var settings = context.Settings.RoomGa;
            var fitness = new RunFitness(context);
            var operators = new RunOperators(context.Shapes, context.Rules, settings);

            // The shapes changed since the last evolution: repair copies of the individuals against the new ones.
            // Cells of inactive blocks are kept (dormant genes).
            var start = new List<Individual<RunGenome>>(current.Count);
            foreach (var genome in current)
            {
                var copy = genome.Clone();
                copy.Repair(context.Shapes, context.Rules, rng);
                start.Add(new Individual<RunGenome>(copy, fitness.Evaluate(copy)));
            }

            // Random immigrants replace the worst individuals.
            start.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
            var immigrants = Math.Min(settings.RandomImmigrants, Math.Max(0, start.Count - settings.Elitism));
            for (var i = 0; i < immigrants; i++)
            {
                var random = CreateRandom(context, rng);
                start[start.Count - 1 - i] = new Individual<RunGenome>(random, fitness.Evaluate(random));
            }

            var initial = new List<RunGenome>(start.Count);
            foreach (var individual in start) initial.Add(individual.Genome);

            var result = new RoomEvolutionResult();
            var ga = new GeneticAlgorithm<RunGenome>
            {
                Fitness = fitness.Evaluate,
                Crossover = operators.Crossover,
                Mutate = operators.Mutate,
                Clone = g => g.Clone(),
                Distance = (a, b) => Distance(a, b, context.Shapes),
                Elitism = settings.Elitism,
                TournamentSize = settings.TournamentSize,
                CrossoverRate = settings.CrossoverRate
            };

            result.Population = ga.Evolve(initial, settings.Generations, rng, stats =>
            {
                result.Generations.Add(stats);
                reportGeneration?.Invoke(stats);
            });

            result.Best = result.Population[0].Genome;
            result.BestPrediction = fitness.Predict(result.Best);
            result.ElapsedMs = watch.Elapsed.TotalMilliseconds;
            return result;
        }

        // Adds the layouts of a played run to the novelty archive, keeping only the most recent ones.
        public static void RememberLayouts(List<CellType[]> recent, RunPlan played, int keep)
        {
            foreach (var room in played.Rooms)
            {
                recent.Add((CellType[])room.Genome.Cells.Clone());
            }
            while (recent.Count > keep) recent.RemoveAt(0);
        }

        // Mean layout distance of the 5 rooms (0..1), used for the diversity statistic.
        public static double Distance(RunGenome a, RunGenome b, bool[][] shapes)
        {
            var sum = 0.0;
            for (var i = 0; i < RunGenome.RoomCount; i++)
            {
                sum += RunGenome.LayoutDistance(a.Rooms[i].Cells, b.Rooms[i].Cells, shapes[i]);
            }
            return sum / RunGenome.RoomCount;
        }

        private static RunGenome CreateRandom(RoomEvolutionContext context, Random rng)
        {
            var genome = RunGenome.CreateRandom(rng, context.Settings.RoomGa.RandomEmptyChance);
            genome.Repair(context.Shapes, context.Rules, rng);
            return genome;
        }
    }
}
