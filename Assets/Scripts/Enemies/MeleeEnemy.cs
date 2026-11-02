using Evolution.Telemetry;
using UnityEngine;

namespace Enemies
{
    public class MeleeEnemy : FsmEnemy
    {
        private static readonly int HasAttacked = Animator.StringToHash("hasAttacked");
        
        protected override DamageSource AttackSource => DamageSource.GroundEnemy;
        
        private void Awake()
        {
            Attacked += Attack;
        }

        private void Attack()
        {
            animator.SetTrigger(HasAttacked);
        }
    }
}
