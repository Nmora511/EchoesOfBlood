using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    public class ArrowHitbox : AttackHitbox
    {
        public float MaxLifetime = 10f;
        
        private const float EmbedDepth = 0.1f;
        
        private bool _hasHit = false;
        private float _lifetime = 0f;
        
        private Rigidbody _rigidbody;
        private Collider _collider;
        
        private void Start()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
        }

        private void Update()
        {
            if (!_hasHit) return;
            
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }
        
        private new void OnTriggerEnter(Collider other)
        {
            if (_hasHit) return;
            if (other.isTrigger || other.CompareTag("Enemy")) return;

            // 1. Move the arrow back to the surface it entered through
            SnapToEntryPoint(other);

            // 2. Now transform.position is in front of the target, so the
            //    knockback direction (arrow -> player) matches the flight direction
            base.OnTriggerEnter(other);

            // 3. Stick
            HandleHit(other);
        }

        private void SnapToEntryPoint(Collider other)
        {
            var flightDir = transform.up;

            // Cast from behind the arrow against this one collider to find where it entered
            var ray = new Ray(transform.position - flightDir * 3f, flightDir);

            if (other.Raycast(ray, out var hit, 6f))
            {
                transform.position = hit.point + flightDir * EmbedDepth;
            }
        }

        private void HandleHit(Collider other)
        {
            if (_hasHit) return;

            _rigidbody.isKinematic = true;
            _collider.enabled = false;
            transform.SetParent(other.transform, true);
            _lifetime = MaxLifetime;

            _hasHit = true;
        }
    }
}
