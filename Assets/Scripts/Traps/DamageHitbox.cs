using Evolution.Telemetry;
using UnityEngine;
using PlayerClass = Player.Player;

namespace Traps
{
    public class DamageHitbox : MonoBehaviour
    {
        public float Damage;
        
        [Tooltip("What this hitbox counts as in the telemetry. GridBlock sets it when the trap is created.")]
        public DamageSource Source = DamageSource.Unknown;
        
        private void OnTriggerStay(Collider other)
        {
            var entityCollided = other.GetComponent<Entity>();
            if (entityCollided == null) return;
            if (entityCollided is PlayerClass player)
            {
                player.OnHit(Damage, transform.position, Source);
            }
            else
            {
                entityCollided.OnHit(Damage, transform.position);
            }
        }
    }
}