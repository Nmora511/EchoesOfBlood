using System.Collections.Generic;
using System.Linq;
using Evolution.Rooms;
using UnityEngine;

namespace Grid
{
    public class GridUnit : MonoBehaviour
    {
        
        [System.Serializable]
        public struct UnitMapping
        {
            public CellType type;
            public GameObject prefab;
        }

        public CellType CurrentUnitType;
        
        [Header("Prefabs Configuration")]
        [SerializeField] private List<UnitMapping> unitMappings;
        
        private static readonly Dictionary<CellType, GameObject> UnitTypeToPrefab = new();
        public static IReadOnlyDictionary<CellType, GameObject> Prefabs => UnitTypeToPrefab;
        
        private void Awake()
        {
            RegisterPrefabs();
        }
        
        // Called from Awake and again before instantiating: the Grid may run before this unit's Awake.
        private void RegisterPrefabs()
        {
            if (UnitTypeToPrefab.Count != 0) return;
            foreach (var mapping in unitMappings.Where(mapping => mapping.prefab != null && !UnitTypeToPrefab.ContainsKey(mapping.type)))
            {
                UnitTypeToPrefab.Add(mapping.type, mapping.prefab);
            }
        }

        // Instantiates the prefab for CurrentUnitType and returns it (null for an empty cell).
        public GameObject InitializeUnit()
        {
            if (CurrentUnitType == CellType.Empty) return null;
            
            RegisterPrefabs();
            if (UnitTypeToPrefab.TryGetValue(CurrentUnitType, out var prefabBase))
            {
                return Instantiate(prefabBase, transform.position, prefabBase.transform.rotation);
            }
           
            Debug.LogError($"O prefab para o tipo '{CurrentUnitType}' não foi configurado ou encontrado no dicionário!");
            return null;
        }
    }
}
