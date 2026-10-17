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
    
        [Header("Vision & Senses Settings")]
        public float ViewRadius = 20f; 
        [Range(0, 360)]
        public float ViewAngle = 150f; 
        public float ProximityRadius = 3f; 
        public LayerMask ObstacleLayer;

        [Header("Advanced AI Feel")]
        public Vector3 EyeOffset = new Vector3(0, 1f, 0);
        public float InvestigationOvershoot = 1.5f;

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
        }
        
    }
}
