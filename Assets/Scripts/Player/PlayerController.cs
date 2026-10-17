using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// TODO: Attack and Animations (All code is commented)
namespace Player
{
    public class Player : Entity
    {
        private static readonly int Speed = Animator.StringToHash("speed");
        private static readonly int HasAttacked = Animator.StringToHash("hasAttacked");
        private static readonly int HasDashed = Animator.StringToHash("hasDashed");
        private static readonly int HasDied = Animator.StringToHash("hasDied");


        public static Player PlayerInstance { get; private set; }

        [Header("Combat Settings")] 
        public float AttackDamage;
        public ScytheHitbox ScytheHitbox;
        public float AttackDuration = 0.67f;
        public float AttackSlideFriction = 4f;
        
        [Header("Movement Settings")]
        public float MoveSpeed = 5f;
        public float RotationSpeed = 15f;

        [Header("Dash Settings")]
        public float DashSpeed = 15f;
        public float DashDuration = 0.2f;
        public float DashCooldown = 1f;
        
        [Header("Gravity Settings")]
        public float Gravity = -15f;
        
        [Header("UI Settings")]
        public GameObject GameOverPanel;
        public Slider HealthSlider;
        public Slider DashSlider;
        
        private Vector3 _verticalVelocity;

        private Camera _mainCamera;
    
        private CharacterController _controller;

        private Renderer _playerRenderer;
        
        private Vector3 _inputDirection;
        private Vector2 _moveInput;

        private Vector3 _finalVelocity;

        private bool _isDashing;
        private float _dashTimer;
        private float _dashCooldownTimer;
        
        private Animator _animator;

        private bool _isAttacking = false;
        private float _attackCooldown = 1.2f;
        private LayerMask _enemyMask;
        
        private bool _isControlLocked = false;
        private Vector3 _forcedDirection;
        
        private void Awake()
        {
            if (PlayerInstance != null && PlayerInstance != this)
            {
                Destroy(gameObject);
                return;
            }
        
            PlayerInstance = this;

            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _mainCamera = Camera.main;
        }
        
        private new void Start()
        {
            base.Start();
            _enemyMask = LayerMask.GetMask("Enemy");
            
            ScytheHitbox.Damage = AttackDamage;
            
            HealthSlider.maxValue = HealthPoints;
            DashSlider.maxValue = DashCooldown;
            UpdateSliders();
        }

        protected override void Update()
        {
            base.Update();
            UpdateHorizontalVelocity();
            UpdateAttackStatus();
            if (!_isDashing)
            {
                ApplyGravity();
            }

            var totalMovement = _finalVelocity + _verticalVelocity + KnockbackVelocity;

            _controller.Move(totalMovement * Time.deltaTime);
            PushOutOfEnemies();
            UpdateSliders();
        }

        // Movement Handlers
        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
            _animator.SetFloat(Speed, _moveInput.magnitude);
        }

        public void OnDash(InputValue value)
        {
            if (value.isPressed && !_isDashing && _dashCooldownTimer <= 0f)
            {
                _isDashing = true;
                _dashTimer = DashDuration;
                _dashCooldownTimer = DashCooldown;
                _animator.SetTrigger(HasDashed);
            }
        }
        
        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity.y < 0)
            {
                _verticalVelocity.y = -2f; 
            }

            _verticalVelocity.y += Gravity * Time.deltaTime;
        }

        private void UpdateHorizontalVelocity()
        {
            if (_isControlLocked)
            {
                _finalVelocity = _forcedDirection * MoveSpeed;
                var targetRot = Quaternion.LookRotation(_forcedDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
                return; 
            }
            
            // Dash Handler
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= Time.deltaTime;
            }

            if (_isDashing)
            {
                _dashTimer -= Time.deltaTime;
                if (_dashTimer <= 0f)
                {
                    _isDashing = false;
                }
                else
                {
                    _finalVelocity = transform.forward * DashSpeed;
                    return;
                }
            }
            
            if (_isAttacking)
            {
                _finalVelocity = Vector3.Lerp(_finalVelocity, Vector3.zero, Time.deltaTime * AttackSlideFriction);
                return;
            }

            // Walking Handler
            // Fixes movimentation based on camera angle
            var camForward = _mainCamera.transform.forward;
            var camRight = _mainCamera.transform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            _inputDirection = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;
        
            var currentSpeed = 0f;

            if (_inputDirection != Vector3.zero) //&& (!isAttacking ))
            {
                var targetRotation = Quaternion.LookRotation(_inputDirection);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSpeed * Time.deltaTime);

                currentSpeed = MoveSpeed;
            }

            _finalVelocity = _inputDirection * currentSpeed;
            //animator.SetFloat("Speed", currentSpeed);
        }
        
        private void PushOutOfEnemies()
        {
            var overlaps = new Collider[25];            
            
            var count = Physics.OverlapSphereNonAlloc(
                transform.position, 2f, overlaps, _enemyMask);

            for (var i = 0; i < count; i++)
            {
                var col = overlaps[i];
                if (col == _controller) continue;

                if (!Physics.ComputePenetration(
                        _controller, transform.position, transform.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out var dir, out var dist)) continue;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) continue;
                _controller.Move(dir.normalized * dist);
            }
        }
        
        public void ForceWalk(Vector3 direction, float duration)
        {
            StartCoroutine(ForceWalkRoutine(direction, duration));
        }

        private IEnumerator ForceWalkRoutine(Vector3 direction, float duration)
        {
            _isControlLocked = true;
            _forcedDirection = direction.normalized;
    
            _animator.SetFloat(Speed, 1f);

            yield return new WaitForSeconds(duration);

            _isControlLocked = false;
            _animator.SetFloat(Speed, 0f);
            _moveInput = Vector2.zero; 
        }

        // Combat Handlers
        
        public void OnAttack(InputValue inputValue)
        {
            if (_isAttacking) return;
            
            _animator.SetTrigger(HasAttacked);
            _isAttacking = true;
        }

        private void UpdateAttackStatus()
        {
            if (!_isAttacking) return;
            
            _attackCooldown -= Time.deltaTime;
            
            if (_attackCooldown > 0f) return;
            
            _isAttacking = false;
            _attackCooldown = AttackDuration;
        }

        public override void Death()
        {
            UpdateSliders();
            _animator.SetTrigger(HasDied);
            StartCoroutine(DelayedGameOver());
            enabled = false;   
        }

        private IEnumerator DelayedGameOver()
        {
            yield return new WaitForSeconds(2f);
            GameOverPanel.SetActive(true);        
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        
        // UI Handlers

        private void UpdateSliders()
        {
            HealthSlider.value = HealthPoints;
            DashSlider.value = DashCooldown - _dashCooldownTimer;
        }
    }
}
