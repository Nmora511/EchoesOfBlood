using Enemies;
using Telemetry;
using UnityEngine;

namespace Player
{
    public class ScytheHitbox : MonoBehaviour
    {
        public float Damage;
        
        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("Enemy")) return;
            var enemy = other.GetComponent<Enemy>();
            if (enemy == null) return;
            
            var healthBefore = enemy.HealthPoints;
            enemy.OnHit(Damage, transform.position);
            var dealt = healthBefore - Mathf.Max(enemy.HealthPoints, 0f);
            if (dealt > 0f) GameTelemetry.PlayerHitEnemy(dealt);
        }
    }
}