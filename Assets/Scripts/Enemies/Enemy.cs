using System;
using UnityEngine;
using UnityEngine.AI;

namespace Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class Enemy : Entity
    {
        private static readonly int HasDied = Animator.StringToHash("hasDied");

        [Header("Target Reference")]
        public Transform Target;

        // Raised when the enemy dies. To listen: enemy.Died += MyMethod;
        public event Action Died;
        
        protected NavMeshAgent navMeshAgent;
        protected Animator animator;

        private Collider _enemyCollider;
        private Rigidbody _enemyRigidbody;
        
        protected new virtual void Start()
        {
            base.Start();
            navMeshAgent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            _enemyCollider = GetComponent<Collider>();
            _enemyRigidbody = GetComponent<Rigidbody>();
        }

        public override void Death()
        {
            animator.SetTrigger(HasDied);
            enabled = false;
            navMeshAgent.enabled = false;
            _enemyRigidbody.useGravity = false;
            _enemyCollider.enabled = false;
            Died?.Invoke();
        }
        
        protected void RotateTowards(Vector3 targetPosition)
        {
            var direction = targetPosition - transform.position;
            direction.y = 0f; 

            // Using 0.001f instead of checking for Vector3.zero prevents micro-jitters 
            if (direction.sqrMagnitude < 0.001f) return;
            var lookRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

        }
    }
}
