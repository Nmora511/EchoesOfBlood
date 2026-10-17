using System;
using UnityEngine;

namespace Grid
{
    [System.Serializable]
    public class GridRow
    {
        public GridUnit.UnitType[] columns = new GridUnit.UnitType[9];
        
        public GridUnit.UnitType this[int index]
        {
            get => columns[index];
            set => columns[index] = value;
        }
    }
    
    public class Grid : MonoBehaviour
    {
        [SerializeField] 
        private GridRow[] gridRows = new GridRow[9];
     
        public GridRow[] GridTypes => gridRows;
        
        private GridBlock[] _gridBlocks;
        private void Start()
        {
            _gridBlocks = GetComponentsInChildren<GridBlock>();
            if (_gridBlocks.Length < 9)
            {
                Debug.LogError($"Grid does not contain 9 blocks, {gameObject.name}");
                return;
            }
            
            DefineRoom();
            
            for (var i=0; i < 9; i++)
            {
                if (GridTypes[i] == null || GridTypes[i].columns == null || GridTypes[i].columns.Length == 0)
                {
                    _gridBlocks[i].enabled = false;
                    continue;
                }
                _gridBlocks[i].enabled = true;
                _gridBlocks[i].unitTypes = GridTypes[i].columns;
                _gridBlocks[i].InitializeBlock();
            }
        }

        private void DefineRoom()
        {
            var E = GridUnit.UnitType.Empty;
            var T = GridUnit.UnitType.ThornTrap;
            var S = GridUnit.UnitType.StakesTrap;
            var G = GridUnit.UnitType.GroundEnemySpawner;
            
            var rawLayout = new GridUnit.UnitType[][]
            {
                new [] { G, E, E, E, E, E, E, T, E },
                new [] { E, S, E, E, E, E, E, S, E },
                new [] { E, E, T, E, G, E, E, E, E },
                new [] { E, E, E, E, E, E, E, E, E },
                new [] { E, G, E, S, E, T, E, E, E },
                new [] { E, E, E, E, E, G, S, E, T },
                new [] { E, T, E, E, E, E, G, E, E },
                new [] { E, E, E, S, E, E, E, E, E },
                new [] { T, E, E, E, E, E, E, E, G }
            };

            InitiateGridTypes(rawLayout);
        }
        
        private void InitiateGridTypes(GridUnit.UnitType[][] rawLayout)
        {
            for (var i = 0; i < 9; i++)
            {
                gridRows[i] ??= new GridRow();
                gridRows[i].columns = rawLayout[i];
            }
        }
    }
}