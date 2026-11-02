using System;
using System.Collections.Generic;

namespace Evolution.Telemetry
{
    // The telemetry of one whole run: the rooms played (completed or the one where the player died).
    [Serializable]
    public class RunTelemetry
    {
        public int RunNumber;
        public int Seed;
        public bool Won;
        public bool IsFinished;
        public int EndedInRoom;        // 1..5 = regular room, 6 = boss room
        public List<RoomTelemetry> Rooms = new List<RoomTelemetry>();

        public RunTelemetry(int runNumber, int seed)
        {
            RunNumber = runNumber;
            Seed = seed;
        }

        public void AddRoom(RoomTelemetry room)
        {
            if (room != null && !IsFinished) Rooms.Add(room);
        }

        public void Finish(bool won, int roomIndex)
        {
            if (IsFinished) return;
            Won = won;
            EndedInRoom = roomIndex + 1;
            IsFinished = true;
        }

        public string ToJson()
        {
            var json = new JsonBuilder();
            json.BeginObject();
            json.Field("RunNumber", RunNumber);
            json.Field("Seed", Seed);
            json.Field("Won", Won);
            json.Field("EndedInRoom", EndedInRoom);
            json.BeginArray("Rooms");
            foreach (var room in Rooms) json.RawValue(room.ToJson());
            json.EndArray();
            json.EndObject();
            return json.ToString();
        }
    }
}
