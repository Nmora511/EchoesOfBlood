using UnityEngine;

namespace Enemies
{
    public class MeleeEnemy : FsmEnemy
    {
        private static readonly int HasAttacked = Animator.StringToHash("hasAttacked");
        
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
