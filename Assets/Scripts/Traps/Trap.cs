using Telemetry;
using UnityEngine;
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
    
        private void Start()
        {
            _animator = GetComponent<Animator>();
            DamageHitbox.Damage = Damage;
        }
    
        public void OnTriggerEnter(Collider other)
        {
            var entityCollided = other.GetComponent<Entity>();
            if (entityCollided == null) return;
        
            if (entityCollided is PlayerClass) GameTelemetry.TrapStepped(DamageHitbox.Source);
            
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
