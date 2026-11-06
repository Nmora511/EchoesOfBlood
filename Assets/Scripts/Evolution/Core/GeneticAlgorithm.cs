using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Evolution.Core
{
    // Generic genetic algorithm: evaluation, tournament selection, crossover, mutation and elitism.
    // It knows nothing about rooms or bosses: everything specific comes in as functions.
    // The same class will be used by the room GA and the boss GE.
    public sealed class GeneticAlgorithm<T>
    {
        public Func<T, double> Fitness;                 // higher is better
        public Func<T, T, Random, T> Crossover;         // returns a NEW genome, must not change the parents
        public Action<T, Random> Mutate;                // changes the genome in place (and repairs it)
        public Func<T, T> Clone;
        public Func<T, T, double> Distance;             // optional, only used for the diversity statistic

        public int Elitism = 2;
        public int TournamentSize = 3;
        public double CrossoverRate = 0.8;
        public int DiversitySamples = 30;               // pairs sampled to estimate the diversity

        // Evolves the population for the given number of generations and returns the last
        // generation sorted from best to worst. onGeneration is called once per generation (including 0).
        public List<Individual<T>> Evolve(IList<T> initial, int generations, Random rng,
            Action<GenerationStats> onGeneration = null)
        {
            if (Fitness == null || Crossover == null || Mutate == null || Clone == null)
                throw new InvalidOperationException("Fitness, Crossover, Mutate and Clone must be set.");
            if (initial == null || initial.Count == 0)
                throw new ArgumentException("The initial population is empty.", nameof(initial));

            var watch = Stopwatch.StartNew();
            var population = new List<Individual<T>>(initial.Count);
            foreach (var genome in initial) population.Add(new Individual<T>(genome, Fitness(genome)));
            Sort(population);
            onGeneration?.Invoke(Stats(0, population, watch));

            for (var generation = 1; generation <= generations; generation++)
            {
                watch.Restart();
                var next = new List<Individual<T>>(population.Count);

                // Elitism: the best ones go to the next generation unchanged (fitness already known).
                var eliteSize = Math.Min(Elitism, population.Count);
                for (var i = 0; i < eliteSize; i++)
                {
                    next.Add(new Individual<T>(Clone(population[i].Genome), population[i].Fitness));
                }

                // Tournament
                while (next.Count < population.Count)
                {
                    var mother = Tournament(population, rng);
                    var father = Tournament(population, rng);

                    var child = rng.NextDouble() < CrossoverRate
                        ? Crossover(mother.Genome, father.Genome, rng)
                        : Clone(mother.Genome);

                    Mutate(child, rng);
                    next.Add(new Individual<T>(child, Fitness(child)));
                }

                population = next;
                Sort(population);
                onGeneration?.Invoke(Stats(generation, population, watch));
            }

            return population;
        }

        private Individual<T> Tournament(List<Individual<T>> population, Random rng)
        {
            var best = population[rng.Next(population.Count)];
            for (var i = 1; i < TournamentSize; i++)
            {
                var other = population[rng.Next(population.Count)];
                if (other.Fitness > best.Fitness) best = other;
            }
            return best;
        }

        // Best first.
        private static void Sort(List<Individual<T>> population)
        {
            population.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
        }

        private GenerationStats Stats(int generation, List<Individual<T>> population, Stopwatch watch)
        {
            var sum = 0.0;
            foreach (var individual in population) sum += individual.Fitness;

            var stats = new GenerationStats
            {
                Generation = generation,
                Best = population[0].Fitness,
                Worst = population[population.Count - 1].Fitness,
                Mean = sum / population.Count
            };

            if (Distance != null && population.Count > 1)
            {
                // Uses its own Random so measuring diversity does not change the evolution's random sequence.
                var sampler = new Random(generation);
                var total = 0.0;
                for (var i = 0; i < DiversitySamples; i++)
                {
                    var a = sampler.Next(population.Count);
                    var b = (a + 1 + sampler.Next(population.Count - 1)) % population.Count;
                    total += Distance(population[a].Genome, population[b].Genome);
                }
                stats.Diversity = total / DiversitySamples;
            }

            stats.ElapsedMs = watch.Elapsed.TotalMilliseconds;
            return stats;
        }
    }
}
