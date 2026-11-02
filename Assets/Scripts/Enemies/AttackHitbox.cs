using Evolution.Telemetry;
using UnityEngine;

namespace Enemies
{
    public class AttackHitbox : MonoBehaviour
    {
        public float Damage;
        
        [Tooltip("What this hitbox counts as in the telemetry. FsmEnemy sets it on Start.")]
        public DamageSource Source = DamageSource.Unknown;
        
        protected void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("Player")) return;
            other.GetComponent<Player.Player>().OnHit(Damage, transform.position, Source);
        }
    }
}
