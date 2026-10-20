using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Grid
{
    public class GridUnit : MonoBehaviour
    {
        public enum UnitType {Empty,ThornTrap, StakesTrap, GroundEnemySpawner, RangedEnemySpawner}
        
        [System.Serializable]
        public struct UnitMapping
        {
            public UnitType type;
            public GameObject prefab;
        }

        public UnitType CurrentUnitType;
        
        [Header("Prefabs Configuration")]
        [SerializeField] private List<UnitMapping> unitMappings;
        
        private static readonly Dictionary<UnitType, GameObject> UnitTypeToPrefab = new();
        public static IReadOnlyDictionary<UnitType, GameObject> Prefabs => UnitTypeToPrefab;
        
        private void Awake()
        {
            if (UnitTypeToPrefab.Count != 0) return;
            foreach (var mapping in unitMappings.Where(mapping => mapping.prefab != null && !UnitTypeToPrefab.ContainsKey(mapping.type)))
            {
                UnitTypeToPrefab.Add(mapping.type, mapping.prefab);
            }
        }

        public void InitializeUnit()
        {
            if (CurrentUnitType == UnitType.Empty) return;
            
            if (UnitTypeToPrefab.TryGetValue(CurrentUnitType, out var prefabBase))
            {
                var unitInstance = Instantiate(prefabBase, transform.position, prefabBase.transform.rotation);
            }
            else
            {
                Debug.LogError($"O prefab para o tipo '{CurrentUnitType}' não foi configurado ou encontrado no dicionário!");
            }
        }
    }
}
