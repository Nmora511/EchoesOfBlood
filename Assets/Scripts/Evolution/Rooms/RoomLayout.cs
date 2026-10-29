namespace Evolution.Rooms
{
    // Geometry of a room: 3x3 outer blocks, each with 3x3 cells, so a 9x9 grid of cells.
    //
    // Blocks are numbered row by row, top to bottom, left to right:
    //   0 1 2
    //   3 4 5      <- middle row: always active, entrance on the left of 3, exit on the right of 5
    //   6 7 8
    // Cells inside a block use the same numbering (0..8).
    // Gene index of a cell = 9 * block + cell.
    public static class RoomLayout
    {
        public const int BlocksPerSide = 3;
        public const int BlockCount = 9;
        public const int CellsPerBlock = 9;
        public const int CellCount = BlockCount * CellsPerBlock; // 81
        public const int GridSide = 9;                           // cells per side

        public const int EntranceBlock = 3;
        public const int ExitBlock = 5;

        // Entrance = middle-left cell of block 3; exit = middle-right cell of block 5.
        public static readonly int EntranceCell = GeneIndex(EntranceBlock, 3);
        public static readonly int ExitCell = GeneIndex(ExitBlock, 5);

        public static int GeneIndex(int block, int cell) => block * CellsPerBlock + cell;

        public static int BlockOf(int gene) => gene / CellsPerBlock;

        // Position of a cell in the 9x9 grid (row 0 = top, col 0 = left).
        public static void ToRowCol(int gene, out int row, out int col)
        {
            var block = BlockOf(gene);
            var cell = gene % CellsPerBlock;
            row = block / BlocksPerSide * BlocksPerSide + cell / BlocksPerSide;
            col = block % BlocksPerSide * BlocksPerSide + cell % BlocksPerSide;
        }

        public static int FromRowCol(int row, int col)
        {
            var block = row / BlocksPerSide * BlocksPerSide + col / BlocksPerSide;
            var cell = row % BlocksPerSide * BlocksPerSide + col % BlocksPerSide;
            return GeneIndex(block, cell);
        }

        public static bool IsActive(int gene, bool[] shape) => shape[BlockOf(gene)];
    }
}
