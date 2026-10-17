using UnityEngine;

namespace Grid
{
    public class GridBlock : MonoBehaviour
    {
        public GridUnit.UnitType[] unitTypes = new  GridUnit.UnitType[9];
    
        public void InitializeBlock()
        {
            var gridUnits = GetComponentsInChildren<GridUnit>();
            if (gridUnits.Length < 9)
            {
                Debug.LogError(gridUnits.Length);
                return;
            }

            for (var i = 0; i < 9; i++)
            {
                gridUnits[i].CurrentUnitType = unitTypes[i];
                gridUnits[i].InitializeUnit();
            }
        }
    }
}
