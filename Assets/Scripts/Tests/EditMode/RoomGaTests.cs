using System;
using System.Collections.Generic;
using Evolution.PlayerModel;
using Evolution.Rooms;
using Evolution.Telemetry;
using NUnit.Framework;

namespace Evolution.Tests
{
    // EditMode tests of the room GA core. Run them in Window > General > Test Runner > EditMode.
    public class RoomGaTests
    {
        private static RoomEvolutionContext CreateContext(int seed, EvolutionSettings settings = null)
        {
            var rng = new Random(seed);
            settings ??= new EvolutionSettings();
            return new RoomEvolutionContext
            {
                Shapes = RoomEvolution.CreateShapes(rng, 5),
                Ranges = new RoomAttributeRanges(),
                Rules = new RepairRules(),
                Settings = settings,
                Profile = PlayerProfile.CreateDefault(settings.Profile, settings.BaseStats)
            };
        }

        private static void AssertPlayable(RoomGenome room, bool[] shape, RepairRules rules)
        {
            Assert.That(RoomRepair.FindSafePath(room, shape), Is.Not.Null, "no trap-free path between the doors");

            var spawners = 0;
            for (var gene = 0; gene < RoomLayout.CellCount; gene++)
            {
                if (!RoomLayout.IsActive(gene, shape)) continue;
                if (CellTypes.IsSpawner(room.Cells[gene])) spawners++;
                if (RoomRepair.IsReserved(gene, rules.DoorClearance))
                    Assert.That(room.Cells[gene], Is.EqualTo(CellType.Empty), "door area is not empty");
            }
            Assert.That(spawners, Is.InRange(rules.MinSpawners, rules.MaxSpawners));

            foreach (var value in room.Attributes) Assert.That(value, Is.InRange(0f, 1f));
        }

        [Test]
        public void CrossoverAndMutation_AlwaysProducePlayableRooms()
        {
            var rng = new Random(1);
            for (var i = 0; i < 300; i++)
            {
                var context = CreateContext(i);
                var operators = new RunOperators(context.Shapes, context.Rules, context.Settings.RoomGa);
                var mother = RunGenome.CreateRandom(rng);
                var father = RunGenome.CreateRandom(rng);
                mother.Repair(context.Shapes, context.Rules, rng);
                father.Repair(context.Shapes, context.Rules, rng);

                var child = operators.Crossover(mother, father, rng);
                operators.Mutate(child, rng);

                for (var room = 0; room < RunGenome.RoomCount; room++)
                    AssertPlayable(child.Rooms[room], context.Shapes[room], context.Rules);
            }
        }

        [Test]
        public void Mutation_DoesNotChangeDormantGenes()
        {
            var rng = new Random(2);
            var context = CreateContext(2);
            var operators = new RunOperators(context.Shapes, context.Rules, context.Settings.RoomGa);
            var run = RunGenome.CreateRandom(rng);
            var before = run.Clone();

            for (var i = 0; i < 200; i++) operators.Mutate(run, rng);

            for (var room = 0; room < RunGenome.RoomCount; room++)
            for (var gene = 0; gene < RoomLayout.CellCount; gene++)
            {
                if (RoomLayout.IsActive(gene, context.Shapes[room])) continue;
                Assert.That(run.Rooms[room].Cells[gene], Is.EqualTo(before.Rooms[room].Cells[gene]));
            }
        }

        [Test]
        public void RoomCrossover_CopiesWholeRoomsFromTheParents()
        {
            var rng = new Random(3);
            var context = CreateContext(3);
            context.Settings.RoomGa.BlockCrossoverRate = 0f; // only whole-room crossover
            var operators = new RunOperators(context.Shapes, context.Rules, context.Settings.RoomGa);

            var mother = RunGenome.CreateRandom(rng);
            var father = RunGenome.CreateRandom(rng);
            mother.Repair(context.Shapes, context.Rules, rng);
            father.Repair(context.Shapes, context.Rules, rng);

            var child = operators.Crossover(mother, father, rng);
            for (var room = 0; room < RunGenome.RoomCount; room++)
            {
                var cells = child.Rooms[room].Cells;
                var fromMother = cells.AsSpanEquals(mother.Rooms[room].Cells);
                var fromFather = cells.AsSpanEquals(father.Rooms[room].Cells);
                Assert.That(fromMother || fromFather, $"room {room} is not a copy of a parent");
            }
        }

        [Test]
        public void Prediction_GrowsWithMoreAndStrongerEnemies()
        {
            var settings = new EvolutionSettings();
            var profile = PlayerProfile.CreateDefault(settings.Profile, settings.BaseStats);
            var model = new RoomDifficultyModel(settings.BaseStats, settings.Model);
            var ranges = new RoomAttributeRanges();
            var shape = RoomShape.Full();

            var weak = EmptyRoom(0.5f);
            weak.Cells[RoomLayout.FromRowCol(0, 4)] = CellType.GroundEnemySpawner;

            var more = weak.Clone();
            more.Cells[RoomLayout.FromRowCol(8, 4)] = CellType.GroundEnemySpawner;

            var stronger = weak.Clone();
            stronger[RoomAttribute.GroundDamage] = 1f;

            var dWeak = model.Predict(weak, shape, ranges.Decode(weak), profile).Total;
            var dMore = model.Predict(more, shape, ranges.Decode(more), profile).Total;
            var dStronger = model.Predict(stronger, shape, ranges.Decode(stronger), profile).Total;

            Assert.That(dWeak, Is.GreaterThan(0f));
            Assert.That(dMore, Is.GreaterThan(dWeak));
            Assert.That(dStronger, Is.GreaterThan(dWeak));
        }

        [Test]
        public void Fitness_IsHigherWhenThePredictionIsCloserToTheTarget()
        {
            var rng = new Random(4);
            var context = CreateContext(4);
            context.Settings.RoomFitness.NoveltyWeight = 0f;
            var run = RunGenome.CreateRandom(rng);
            run.Repair(context.Shapes, context.Rules, rng);

            // Make the target exactly the current prediction: fitness must be ~0 (the maximum).
            var fitness = new RunFitness(context);
            var prediction = fitness.Predict(run);
            var curve = new float[RunGenome.RoomCount];
            for (var k = 0; k < curve.Length; k++) curve[k] = prediction.Rooms[k].Total;
            context.Settings.RoomFitness.TargetCurve = curve;
            context.Settings.RoomFitness.LethalThreshold = float.MaxValue;

            var perfect = fitness.Evaluate(run);
            Assert.That(perfect, Is.EqualTo(0).Within(1e-4));

            // Same run with a stronger first room: farther from the target, lower fitness.
            var harder = run.Clone();
            harder.Rooms[0][RoomAttribute.GroundDamage] = 1f;
            harder.Rooms[0][RoomAttribute.RangedDamage] = 1f;
            harder.Rooms[0][RoomAttribute.SurviveTime] = 1f;
            Assert.That(fitness.Evaluate(harder), Is.LessThan(perfect));
        }

        [Test]
        public void Calibration_WithAlphaOne_ReproducesTheObservedDamage()
        {
            var settings = new EvolutionSettings();
            settings.Profile.Alpha = 1f;
            var profile = PlayerProfile.CreateDefault(settings.Profile, settings.BaseStats);
            var model = new RoomDifficultyModel(settings.BaseStats, settings.Model);
            var ranges = new RoomAttributeRanges();

            var genome = EmptyRoom(0.5f);
            genome.Cells[RoomLayout.FromRowCol(0, 4)] = CellType.GroundEnemySpawner;
            genome.Cells[RoomLayout.FromRowCol(8, 4)] = CellType.RangedEnemySpawner;
            var plan = new RoomPlan { Genome = genome, Shape = RoomShape.Full(), Settings = ranges.Decode(genome) };

            // The player survived the whole room, lost 35% of health to ground enemies and 10% to arrows.
            var telemetry = new RoomTelemetry(1, 0, plan.Settings.SurviveTime, 100f);
            telemetry.RecordFrame(plan.Settings.SurviveTime, 100f, false, 0f, false);
            telemetry.RecordDamage(DamageSource.GroundEnemy, 35f);
            telemetry.RecordDamage(DamageSource.Arrow, 10f);
            telemetry.Finish(RoomOutcome.Survived, 55f);

            ProfileUpdater.UpdateFromRoom(profile, plan, telemetry, model, settings.Profile);

            var prediction = model.Predict(genome, plan.Shape, plan.Settings, profile);
            Assert.That(prediction.Damage[(int)DamageSource.GroundEnemy], Is.EqualTo(0.35f).Within(1e-3));
            Assert.That(prediction.Damage[(int)DamageSource.Arrow], Is.EqualTo(0.10f).Within(1e-3));
            Assert.That(profile.RoomsObserved, Is.EqualTo(1));
        }

        [Test]
        public void Delta_GoesDownOnDeath_AndUpOnAComfortableRun()
        {
            var settings = new EvolutionSettings();
            var profile = PlayerProfile.CreateDefault(settings.Profile, settings.BaseStats);

            var died = new RunTelemetry(1, 0);
            died.AddRoom(FinishedRoom(RoomOutcome.Died, 0f));
            ProfileUpdater.UpdateFromRun(profile, died, settings.Profile);
            Assert.That(profile.Delta, Is.LessThan(1f));

            var before = profile.Delta;
            var easy = new RunTelemetry(2, 0);
            for (var i = 0; i < RunPlan.RoomCount; i++) easy.AddRoom(FinishedRoom(RoomOutcome.Survived, 90f));
            ProfileUpdater.UpdateFromRun(profile, easy, settings.Profile);
            Assert.That(profile.Delta, Is.GreaterThan(before));
        }

        [Test]
        public void Evolution_ImprovesFitness_AndIsRepeatableWithTheSameSeed()
        {
            RoomEvolutionResult Run()
            {
                var context = CreateContext(5);
                var rng = new Random(5);
                var population = RoomEvolution.CreatePopulation(context, rng);
                return RoomEvolution.Evolve(population, context, rng);
            }

            var first = Run();
            var second = Run();

            Assert.That(first.Generations[first.Generations.Count - 1].Best,
                Is.GreaterThan(first.Generations[0].Best));
            Assert.That(first.BestPrediction.Error, Is.LessThan(0.25f), "best run is far from the target curve");
            Assert.That(second.Population[0].Fitness, Is.EqualTo(first.Population[0].Fitness));

            var context5 = CreateContext(5);
            for (var room = 0; room < RunGenome.RoomCount; room++)
                AssertPlayable(first.Best.Rooms[room], context5.Shapes[room], context5.Rules);
        }

        [Test]
        public void Novelty_IsZeroForARoomAlreadyPlayed_AndPlanKeepsTheGenome()
        {
            var rng = new Random(6);
            var context = CreateContext(6);
            var run = RunGenome.CreateRandom(rng);
            run.Repair(context.Shapes, context.Rules, rng);

            var plan = RunPlan.FromGenome(run, context.Shapes, context.Ranges);
            RoomEvolution.RememberLayouts(context.RecentLayouts, plan, 15);

            Assert.That(new RunFitness(context).Predict(run).Novelty, Is.EqualTo(0f));
            for (var room = 0; room < RunGenome.RoomCount; room++)
            {
                Assert.That(plan.Rooms[room].Genome.Cells.AsSpanEquals(run.Rooms[room].Cells));
                Assert.That(plan.Rooms[room].Shape, Is.EqualTo(context.Shapes[room]));
            }
        }

        // ---------- helpers ----------

        private static RoomGenome EmptyRoom(float attributeValue)
        {
            var genome = new RoomGenome();
            for (var i = 0; i < genome.Attributes.Length; i++) genome.Attributes[i] = attributeValue;
            return genome;
        }

        private static RoomTelemetry FinishedRoom(RoomOutcome outcome, float finalHealth)
        {
            var room = new RoomTelemetry(1, 0, 60f, 100f);
            room.RecordFrame(60f, finalHealth, false, 0f, false);
            room.Finish(outcome, finalHealth);
            return room;
        }
    }

    internal static class ArrayExtensions
    {
        public static bool AsSpanEquals(this CellType[] a, CellType[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
