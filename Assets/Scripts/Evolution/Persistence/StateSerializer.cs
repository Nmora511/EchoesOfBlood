using System;
using System.Collections.Generic;
using System.Text;
using Evolution.PlayerModel;
using Evolution.Rooms;
using Evolution.Telemetry;

namespace Evolution.Persistence
{
    // Converts the EvolutionState to JSON and back.
    // Unity's JsonUtility can not do List of arrays, jagged arrays or private constructors, and the
    // core has no Unity reference anyway, so this is written by hand with JsonBuilder / JsonNode.
    //
    // Compact formats used in the file:
    //   cells  = string of 81 digits, one CellType per cell (0 empty, 1 thorn, 2 stakes, 3 ground, 4 ranged)
    //   shape  = string of 9 characters, '1' = block exists
    public static class StateSerializer
    {
        public static string ToJson(EvolutionState state)
        {
            var json = new JsonBuilder { ExactNumbers = true };
            json.BeginObject();
            json.Field("Version", state.Version);
            json.Field("ParticipantId", state.ParticipantId);
            json.Field("Seed", state.Seed);
            json.Field("NextSeed", state.NextSeed);
            json.Field("RunNumber", state.RunNumber);
            json.Field("TotalGenerations", state.TotalGenerations);
            json.Field("CreatedAt", state.CreatedAt);
            json.Field("UpdatedAt", state.UpdatedAt);

            WriteProfile(json, "Profile", state.Profile);

            json.BeginObject("CurrentPlan");
            json.Field("Fitness", state.CurrentPlanFitness);
            json.Field("Generation", state.CurrentPlanGeneration);
            json.BeginArray("Rooms");
            foreach (var room in state.CurrentPlan.Rooms)
            {
                json.BeginObject();
                json.Field("Shape", ShapeToText(room.Shape));
                WriteRoomGenome(json, room.Genome);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();

            json.BeginArray("Population");
            foreach (var run in state.Population)
            {
                json.BeginArray(null);
                foreach (var room in run.Rooms)
                {
                    json.BeginObject();
                    WriteRoomGenome(json, room);
                    json.EndObject();
                }
                json.EndArray();
            }
            json.EndArray();

            var layouts = new string[state.RecentLayouts.Count];
            for (var i = 0; i < layouts.Length; i++) layouts[i] = CellsToText(state.RecentLayouts[i]);
            json.Field("RecentLayouts", layouts);

            json.EndObject();
            return json.ToString();
        }

        // ranges: the current RoomAttributeRanges, used to decode the plan's attribute genes.
        public static EvolutionState FromJson(string text, RoomAttributeRanges ranges)
        {
            var root = JsonNode.Parse(text);
            if (root.Type != JsonNode.NodeType.Object) throw new FormatException("The save file is not a JSON object.");

            var version = root["Version"].AsInt(-1);
            if (version < 1 || version > EvolutionState.CurrentVersion)
                throw new FormatException($"Unknown save version {version}.");

            var state = new EvolutionState
            {
                Version = version,
                ParticipantId = root["ParticipantId"].AsString("dev"),
                Seed = root["Seed"].AsInt(),
                NextSeed = root["NextSeed"].AsInt(),
                RunNumber = root["RunNumber"].AsInt(1),
                TotalGenerations = root["TotalGenerations"].AsInt(),
                CreatedAt = root["CreatedAt"].AsString(),
                UpdatedAt = root["UpdatedAt"].AsString(),
                Profile = ReadProfile(root["Profile"])
            };

            var plan = root["CurrentPlan"];
            state.CurrentPlanFitness = plan["Fitness"].AsDouble();
            state.CurrentPlanGeneration = plan["Generation"].AsInt();
            state.CurrentPlan = new RunPlan();
            if (plan["Rooms"].Count != RunPlan.RoomCount) throw new FormatException("The saved plan does not have 5 rooms.");
            for (var i = 0; i < RunPlan.RoomCount; i++)
            {
                var room = plan["Rooms"][i];
                var genome = ReadRoomGenome(room);
                state.CurrentPlan.Rooms[i] = new RoomPlan
                {
                    Genome = genome,
                    Shape = TextToShape(room["Shape"].AsString()),
                    Settings = ranges.Decode(genome)
                };
            }

            foreach (var runNode in root["Population"].Items)
            {
                if (runNode.Count != RunGenome.RoomCount) throw new FormatException("A saved individual does not have 5 rooms.");
                var run = new RunGenome();
                for (var i = 0; i < RunGenome.RoomCount; i++) run.Rooms[i] = ReadRoomGenome(runNode[i]);
                state.Population.Add(run);
            }

            foreach (var layout in root["RecentLayouts"].Items)
            {
                state.RecentLayouts.Add(TextToCells(layout.AsString()));
            }

            return state;
        }

        // ---------------- Profile ----------------

        public static void WriteProfile(JsonBuilder json, string name, PlayerProfile profile)
        {
            json.BeginObject(name);
            json.Field("Weights", profile.Weights);
            json.Field("BaseLifetime", profile.BaseLifetime);
            json.Field("Delta", profile.Delta);
            json.Field("PlayerMaxHealth", profile.PlayerMaxHealth);
            json.Field("RoomsObserved", profile.RoomsObserved);
            json.Field("RunsObserved", profile.RunsObserved);
            json.EndObject();
        }

        private static PlayerProfile ReadProfile(JsonNode node)
        {
            if (!node.Exists) throw new FormatException("The save file has no Profile.");
            var profile = new PlayerProfile
            {
                Delta = node["Delta"].AsFloat(1f),
                PlayerMaxHealth = node["PlayerMaxHealth"].AsFloat(100f),
                RoomsObserved = node["RoomsObserved"].AsInt(),
                RunsObserved = node["RunsObserved"].AsInt()
            };
            // Copies into the arrays of the right size, so adding a DamageSource later does not break old saves.
            CopyInto(node["Weights"].AsFloatArray(), profile.Weights);
            CopyInto(node["BaseLifetime"].AsFloatArray(), profile.BaseLifetime);
            return profile;
        }

        // ---------------- Genomes ----------------

        private static void WriteRoomGenome(JsonBuilder json, RoomGenome genome)
        {
            json.Field("Cells", CellsToText(genome.Cells));
            json.Field("Attributes", genome.Attributes);
        }

        private static RoomGenome ReadRoomGenome(JsonNode node)
        {
            var genome = new RoomGenome { Cells = TextToCells(node["Cells"].AsString()) };
            CopyInto(node["Attributes"].AsFloatArray(), genome.Attributes);
            return genome;
        }

        public static string CellsToText(CellType[] cells)
        {
            var text = new StringBuilder(cells.Length);
            foreach (var cell in cells) text.Append((char)('0' + (int)cell));
            return text.ToString();
        }

        public static CellType[] TextToCells(string text)
        {
            if (text == null || text.Length != RoomLayout.CellCount)
                throw new FormatException($"A room must have {RoomLayout.CellCount} cells.");

            var cells = new CellType[RoomLayout.CellCount];
            for (var i = 0; i < cells.Length; i++)
            {
                var value = text[i] - '0';
                if (value < 0 || value >= CellTypes.Count) throw new FormatException($"Bad cell type '{text[i]}'.");
                cells[i] = (CellType)value;
            }
            return cells;
        }

        public static string ShapeToText(bool[] shape)
        {
            var text = new StringBuilder(shape.Length);
            foreach (var block in shape) text.Append(block ? '1' : '0');
            return text.ToString();
        }

        public static bool[] TextToShape(string text)
        {
            if (text == null || text.Length != RoomLayout.BlockCount)
                throw new FormatException($"A shape must have {RoomLayout.BlockCount} blocks.");

            var shape = new bool[RoomLayout.BlockCount];
            for (var i = 0; i < shape.Length; i++) shape[i] = text[i] == '1';
            return shape;
        }

        private static void CopyInto(float[] source, float[] target)
        {
            if (source == null) return;
            Array.Copy(source, target, Math.Min(source.Length, target.Length));
        }
    }
}
