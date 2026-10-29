using System;
using System.Collections.Generic;

namespace Evolution.Rooms
{
    [Serializable]
    public class RepairRules
    {
        public int MinSpawners = 1;
        public int MaxSpawners = 6;

        // Cells within this distance (in cells, counting diagonals) from a door are always empty.
        public int DoorClearance = 1;
    }

    // Fixes a room genome IN PLACE so the room is always playable.
    // Only the cells of active blocks are touched; the others are kept as they are (dormant genes).
    public static class RoomRepair
    {
        public static void Repair(RoomGenome genome, bool[] shape, RepairRules rules, Random rng)
        {
            ClampAttributes(genome);
            ClearDoorAreas(genome, shape, rules.DoorClearance);
            LimitSpawners(genome, shape, rules, rng);
            ClearTrapsFromPath(genome, shape);
        }

        public static bool IsReserved(int gene, int doorClearance)
        {
            RoomLayout.ToRowCol(gene, out var row, out var col);
            return NearCell(row, col, RoomLayout.EntranceCell, doorClearance) ||
                   NearCell(row, col, RoomLayout.ExitCell, doorClearance);
        }

        // Shortest path of cells from the entrance to the exit that avoids traps,
        // walking only on active blocks. Returns null when there is none.
        public static List<int> FindSafePath(RoomGenome genome, bool[] shape)
        {
            return FindPath(shape, gene => !CellTypes.IsTrap(genome.Cells[gene]));
        }

        private static void ClampAttributes(RoomGenome genome)
        {
            for (var i = 0; i < genome.Attributes.Length; i++)
            {
                genome.Attributes[i] = Math.Max(0f, Math.Min(1f, genome.Attributes[i]));
            }
        }

        private static void ClearDoorAreas(RoomGenome genome, bool[] shape, int doorClearance)
        {
            for (var gene = 0; gene < RoomLayout.CellCount; gene++)
            {
                if (RoomLayout.IsActive(gene, shape) && IsReserved(gene, doorClearance))
                {
                    genome.Cells[gene] = CellType.Empty;
                }
            }
        }

        private static void LimitSpawners(RoomGenome genome, bool[] shape, RepairRules rules, Random rng)
        {
            var spawners = new List<int>();
            var freeCells = new List<int>();

            for (var gene = 0; gene < RoomLayout.CellCount; gene++)
            {
                if (!RoomLayout.IsActive(gene, shape)) continue;
                if (CellTypes.IsSpawner(genome.Cells[gene])) spawners.Add(gene);
                else if (genome.Cells[gene] == CellType.Empty && !IsReserved(gene, rules.DoorClearance)) freeCells.Add(gene);
            }

            // Too many: remove random spawners.
            while (spawners.Count > rules.MaxSpawners)
            {
                var i = rng.Next(spawners.Count);
                genome.Cells[spawners[i]] = CellType.Empty;
                spawners.RemoveAt(i);
            }

            // Too few: add ground spawners on random free cells.
            while (spawners.Count < rules.MinSpawners && freeCells.Count > 0)
            {
                var i = rng.Next(freeCells.Count);
                genome.Cells[freeCells[i]] = CellType.GroundEnemySpawner;
                spawners.Add(freeCells[i]);
                freeCells.RemoveAt(i);
            }
        }

        private static void ClearTrapsFromPath(RoomGenome genome, bool[] shape)
        {
            if (FindSafePath(genome, shape) != null) return;

            // No trap-free path: take the shortest path ignoring traps and empty the traps on it.
            var path = FindPath(shape, gene => true);
            if (path == null) return; // only happens if the middle row is inactive

            foreach (var gene in path)
            {
                if (CellTypes.IsTrap(genome.Cells[gene])) genome.Cells[gene] = CellType.Empty;
            }
        }

        // Breadth-first search on the 9x9 grid (4 directions).
        private static List<int> FindPath(bool[] shape, Func<int, bool> walkable)
        {
            var start = RoomLayout.EntranceCell;
            var goal = RoomLayout.ExitCell;
            var previous = new int[RoomLayout.CellCount];
            for (var i = 0; i < previous.Length; i++) previous[i] = -2; // -2 = not visited

            var queue = new Queue<int>();
            queue.Enqueue(start);
            previous[start] = -1;

            int[] dRow = { -1, 1, 0, 0 };
            int[] dCol = { 0, 0, -1, 1 };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal) break;

                RoomLayout.ToRowCol(current, out var row, out var col);
                for (var d = 0; d < 4; d++)
                {
                    var r = row + dRow[d];
                    var c = col + dCol[d];
                    if (r < 0 || r >= RoomLayout.GridSide || c < 0 || c >= RoomLayout.GridSide) continue;

                    var next = RoomLayout.FromRowCol(r, c);
                    if (previous[next] != -2) continue;
                    if (!RoomLayout.IsActive(next, shape) || !walkable(next)) continue;

                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (previous[goal] == -2) return null;

            var path = new List<int>();
            for (var gene = goal; gene != -1; gene = previous[gene]) path.Add(gene);
            path.Reverse();
            return path;
        }

        private static bool NearCell(int row, int col, int targetGene, int distance)
        {
            RoomLayout.ToRowCol(targetGene, out var targetRow, out var targetCol);
            return Math.Abs(row - targetRow) <= distance && Math.Abs(col - targetCol) <= distance;
        }
    }
}
