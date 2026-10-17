using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    public class ArrowHitbox : AttackHitbox
    {
        public float MaxLifetime = 10f;
        
        private new void OnTriggerEnter(Collider other)
        {
            base.OnTriggerEnter(other);

            if (other.isTrigger && !other.CompareTag("Player")) return;
            HandleHit(other);
        }
    }
}
