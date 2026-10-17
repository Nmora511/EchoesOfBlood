using UnityEngine;

namespace Enemies
{
    public class AttackHitbox : MonoBehaviour
    {
        public float Damage;
        
        protected void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("Player")) return;
            other.GetComponent<Player.Player>().OnHit(Damage, transform.position);
        }
    }
}
