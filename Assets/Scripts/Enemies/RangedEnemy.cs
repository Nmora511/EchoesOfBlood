using UnityEngine;

namespace Enemies
{
    public class RangedEnemy : FsmEnemy
    {
        public Rigidbody ArrowObject;
        
        public float ArrowForce;

        private LayerMask _playerLayerMask;

        private void Awake()
        {
            _playerLayerMask= LayerMask.GetMask("Player");
        }
        
        public void Shoot()
        {
            var shootRay =  new Ray(transform.position, transform.forward);

            var targetPoint = Physics.Raycast(shootRay, out var hit, 75,_playerLayerMask) ? hit.point : shootRay.GetPoint(75);
            
            var shootDirection = targetPoint - transform.position;
            
            var spawnPosition = transform.position + transform.forward + Vector3.up * 0.5f;
            var spawnRotation = Quaternion.LookRotation(shootDirection) * Quaternion.Euler(90f, 0f, 0f);

            var currentArrow = Instantiate(ArrowObject, spawnPosition, spawnRotation);

            currentArrow.AddForce(shootDirection.normalized *  ArrowForce, ForceMode.Impulse);
        }
    }
}
