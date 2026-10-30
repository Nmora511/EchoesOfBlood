using System.Linq;
using Evolution.Rooms;
using UnityEngine;

namespace Grid
{
    // Builds the room from the RoomPlan given by RunManager:
    // turns blocks on/off according to the shape, fills the cells from the genome and closes the borders with walls.
    // Everything happens in Awake, so the NavMesh (built in DynamicNavMesh.Start) already sees the final room.   
    public class Grid : MonoBehaviour
    {
        [Header("Walls")]
        [Tooltip("Optional. A 1x1x1 cube-like prefab, scaled to fit each wall. Empty = plain Unity cube.")]
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private bool buildWalls = true;
        [SerializeField] private float wallHeight = 2f;
        [SerializeField] private float wallThickness = 0.5f;
        
        private GridBlock[] _blocks;
        
        private void Awake()
        {
            _blocks = GetComponentsInChildren<GridBlock>(true).OrderBy(GridSort.Row).ThenBy(GridSort.Column).ToArray();
            if (_blocks.Length != RoomLayout.BlockCount)
            {
                Debug.LogError($"Grid must contain {RoomLayout.BlockCount} blocks, found {_blocks.Length}, {gameObject.name}");
                return;
            }
            
            var room = RunManager.Instance.CurrentRoom;
            if (room == null)
            {
                Debug.LogError("Grid: RunManager has no room for this scene.");
                return;
            }
            
            if (RunManager.Instance.Config.LogRoomLayout)
            {
                Debug.Log($"[Grid] Room {RunManager.Instance.RoomIndex + 1} shape {RoomShape.Describe(room.Shape)}\n" +
                          room.Describe() + JsonUtility.ToJson(room.Settings, true));
            }
            
            for (var b = 0; b < RoomLayout.BlockCount; b++)
            {
                _blocks[b].gameObject.SetActive(room.Shape[b]);
                if (room.Shape[b])
                {
                    _blocks[b].InitializeBlock(room, b);
                }
            }
            
            if (buildWalls)
            {
                BuildWalls(room.Shape);
            }
        }
        
        private void BuildWalls(bool[] shape)
        {
            var cellSize = _blocks[4].CellSize;
            var blockSize = cellSize * RoomLayout.BlocksPerSide;
            var parent = new GameObject("Walls").transform;
            parent.SetParent(transform, true);

            for (var b = 0; b < RoomLayout.BlockCount; b++)
            {
                if (!shape[b]) continue;

                var row = b / RoomLayout.BlocksPerSide;
                var col = b % RoomLayout.BlocksPerSide;
                var center = _blocks[b].Center;

                TryWall(parent, shape, center, blockSize, cellSize, row - 1, col, Vector3.forward, isDoorSide: false);
                TryWall(parent, shape, center, blockSize, cellSize, row + 1, col, Vector3.back, isDoorSide: false);
                TryWall(parent, shape, center, blockSize, cellSize, row, col - 1, Vector3.left, b == RoomLayout.EntranceBlock);
                TryWall(parent, shape, center, blockSize, cellSize, row, col + 1, Vector3.right, b == RoomLayout.ExitBlock);
            }
        }
        
        // Places a wall on one side of a block when there is no active block on that side.
        private void TryWall(Transform parent, bool[] shape, Vector3 center, float blockSize, float cellSize,
            int neighborRow, int neighborCol, Vector3 side, bool isDoorSide)
        {
            var isNeighborValid = neighborRow is >= 0 and < RoomLayout.BlocksPerSide &&
                         neighborCol is >= 0 and < RoomLayout.BlocksPerSide;
            var isNeighborActivated = isNeighborValid && shape[neighborRow * RoomLayout.BlocksPerSide + neighborCol];
            if (isNeighborActivated) return;

            var sideCenter = center + side * (blockSize / 2f);
            var along = Vector3.Cross(Vector3.up, side); // direction the wall runs in

            if (!isDoorSide)
            {
                CreateWall(parent, sideCenter, along, blockSize + wallThickness);
                return;
            }

            // Door side: leave the middle cell open for the door.
            CreateWall(parent, sideCenter + along * cellSize, along, cellSize + wallThickness);
            CreateWall(parent, sideCenter - along * cellSize, along, cellSize + wallThickness);
        }
        
        private void CreateWall(Transform parent, Vector3 position, Vector3 along, float length)
        {
            var wall = wallPrefab != null ? Instantiate(wallPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent, true);
            wall.transform.position = position + Vector3.up * (wallHeight / 2f);
            wall.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
            wall.transform.localScale = new Vector3(wallThickness, wallHeight, length);
        }
        
        // Sorting keys shared by Grid and GridBlock: row 0 is the top (largest z), column 0 is the left (smallest x).
        public static class GridSort
        {
            public static float Row(Component c) => -Mathf.Round(c.transform.position.z * 10f);
            public static float Column(Component c) => Mathf.Round(c.transform.position.x * 10f);
        }
    }
}