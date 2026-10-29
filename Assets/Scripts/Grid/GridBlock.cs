using System;
using UnityEngine;
using System.Linq;
using Enemies;
using Evolution.Rooms;
using Traps;


namespace Grid
{
    public class GridBlock : MonoBehaviour
    {
        // Center of the block in world space (average of its 9 cells).
        public Vector3 Center
        {
            get
            {
                var units = Units;
                var sum = units.Aggregate(Vector3.zero, (current, unit) => current + unit.transform.position);
                return sum / units.Length;
            }
        }
        
        // Distance between two neighboring cells.
        public float CellSize
        {
            get
            {
                var units = Units;
                return Vector3.Distance(units[0].transform.position, units[1].transform.position);
            }
        }

        // Cells sorted top to bottom, left to right, so cell i is always gene cell i.
        private GridUnit[] Units =>
            GetComponentsInChildren<GridUnit>(true).OrderBy(Grid.GridSort.Row).ThenBy(Grid.GridSort.Column).ToArray();
        
        public void InitializeBlock(RoomPlan room, int blockIndex)
        {
            var units = Units;
            if (units.Length < RoomLayout.CellsPerBlock)
            {
                Debug.LogError($"GridBlock {name} has {units.Length} cells, expected {RoomLayout.CellsPerBlock}");
                return;
            }

            for (var i = 0; i < RoomLayout.CellsPerBlock; i++)
            {
                var cellType = room.CellAt(blockIndex, i);
                units[i].CurrentUnitType = cellType;

                var instance = units[i].InitializeUnit();
                if (instance != null)
                {
                    Configure(instance, cellType, room.Settings);
                }
            }
        }

        // Applies the room's attribute genes to the object just created in a cell.
        // It runs before the object's Start, so Trap and FsmEnemy copy the new values to their hitboxes.
        private static void Configure(GameObject instance, CellType cellType, RoomSettings settings)
        {
            switch (cellType)
            {
                case CellType.ThornTrap:
                    MultiplyTrapDamage(instance, settings.ThornDamageMultiplier);
                    break;
                case CellType.StakesTrap:
                    MultiplyTrapDamage(instance, settings.StakesDamageMultiplier);
                    break;
                case CellType.GroundEnemySpawner:
                    instance.GetComponent<EnemySpawner>().Configure(settings.Ground);
                    break;
                case CellType.RangedEnemySpawner:
                    instance.GetComponent<EnemySpawner>().Configure(settings.Ranged);
                    break;
                case CellType.Empty:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cellType), cellType, null);
            }
        }

        private static void MultiplyTrapDamage(GameObject instance, float multiplier)
        {
            var trap = instance.GetComponentInChildren<Trap>();
            if (trap != null) trap.Damage *= multiplier;
        }
    }
}
