using Telemetry;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using PlayerClass = Player.Player;

namespace Traps
{
    public class Trap : MonoBehaviour
    {
        private static readonly int WasActivated = Animator.StringToHash("wasActivated");
        private static readonly int WasDeactivated = Animator.StringToHash("wasDeactivated");

        public float Damage;
        public DamageHitbox DamageHitbox;
        
        private Animator _animator;
        private int _entitiesOnTrap = 0;
        
        private int _playerContacts = 0;
        
        public const string DefaultAreaName = "Trap";
    
        [Header("Navigation")]
        [Tooltip("NavMesh area of the trap (create it in Window > AI > Navigation > Areas, cost 1). Empty = no area.")]
        public string NavMeshAreaName = DefaultAreaName;
        
        private static bool _warnedMissingArea;
        
        private void Awake()
        {
            MarkNavMeshArea();
        }
        
        private void MarkNavMeshArea()
        {
            if (string.IsNullOrEmpty(NavMeshAreaName)) return;

            var area = NavMesh.GetAreaFromName(NavMeshAreaName);
            if (area < 0)
            {
                if (_warnedMissingArea) return;
                Debug.LogWarning($"[Trap] NavMesh area '{NavMeshAreaName}' does not exist, enemies will never learn to avoid traps. " +
                                 "Create it in Window > AI > Navigation > Areas.");
                _warnedMissingArea = true;
                return;
            }
            
            var volume = gameObject.AddComponent<NavMeshModifierVolume>();
            volume.area = area;
            foreach (var box in GetComponents<BoxCollider>())
            {
                if (box.isTrigger) continue;
                volume.center = box.center;
                volume.size = new Vector3(box.size.x, box.size.y + 4f, box.size.z);
                break;
            }
        }
        
        private void Start()
        {
            _animator = GetComponent<Animator>();
            DamageHitbox.Damage = Damage;
        }
    
        public void OnTriggerEnter(Collider other)
        {
            var entityCollided = other.GetComponent<Entity>();
            if (entityCollided == null) return;
        
            if (entityCollided is PlayerClass)
            {
                if (_playerContacts == 0) GameTelemetry.TrapStepped(DamageHitbox.Source);
                _playerContacts++;
            }
            
            if (_entitiesOnTrap == 0)
            {
                _animator.SetTrigger(WasActivated);
                _animator.ResetTrigger(WasDeactivated);
            }
        
            _entitiesOnTrap++;

        }
    
        public void OnTriggerExit(Collider other)
        {
            var entityCollided = other.GetComponent<Entity>();
            if (entityCollided == null) return;
        
            if (entityCollided is PlayerClass) _playerContacts = Mathf.Max(0, _playerContacts - 1);
            
            _entitiesOnTrap--;
        
            if (_entitiesOnTrap < 0) 
            {
                _entitiesOnTrap = 0;
            }

            if (_entitiesOnTrap == 0)
            {
                _animator.SetTrigger(WasDeactivated);
                _animator.ResetTrigger(WasActivated);
            }
        }
    }
}
