using System;
using System.Collections.Generic;

namespace Evolution.Rooms
{
    // Generates the shape of a room: which of the 9 outer blocks exist.
    // The middle row (3, 4, 5) is always active so there is always a way from the entrance to the exit.
    // Every other block touches the middle row, so any combination stays connected.
    public static class RoomShape
    {
        public static bool[] Generate(Random rng, int minBlocks = 5, int maxBlocks = RoomLayout.BlockCount)
        {
            // Wrong ranges security
            minBlocks = Math.Max(3, Math.Min(minBlocks, RoomLayout.BlockCount));
            maxBlocks = Math.Max(minBlocks, Math.Min(maxBlocks, RoomLayout.BlockCount));

            var shape = new bool[RoomLayout.BlockCount];
            shape[3] = shape[4] = shape[5] = true;

            var optional = new List<int> { 0, 1, 2, 6, 7, 8 };
            Shuffle(optional, rng);

            var totalSize = rng.Next(minBlocks, maxBlocks + 1);
            for (var i = 0; i < totalSize - 3; i++)
            {
                shape[optional[i]] = true;
            }

            return shape;
        }

        public static bool[] Full()
        {
            var shape = new bool[RoomLayout.BlockCount];
            for (var i = 0; i < shape.Length; i++) shape[i] = true;
            return shape;
        }

        public static string Describe(bool[] shape)
        {
            var text = "";
            for (var row = 0; row < RoomLayout.BlocksPerSide; row++)
            {
                for (var col = 0; col < RoomLayout.BlocksPerSide; col++)
                {
                    text += shape[row * RoomLayout.BlocksPerSide + col] ? "#" : ".";
                }
                if (row < RoomLayout.BlocksPerSide - 1) text += "/";
            }
            return text;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
