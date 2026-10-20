using BehaviourTree;
using UnityEngine;

namespace Enemies
{
    // Behavior tree actions (leaves) of BossEnemy. They live in a partial class so they can keep using
    // the boss's private members (ConfigureAgent, PlanarDistance, cooldown timestamps) without exposing them.
    public partial class BossEnemy
    {
        private static readonly int IsChasing = Animator.StringToHash("isChasing");

        private static readonly int IsRushing = Animator.StringToHash("isRushing");

        private static readonly int HasAttacked = Animator.StringToHash("hasAttacked");
        // ---------- Actions (leaves). Each one's state lives in its OWN instance ----------

        private sealed class ChaseAction : ActionNode
        {
            private readonly BossEnemy _boss;
            public ChaseAction(BossEnemy boss) : base("Chase") { _boss = boss; }

            protected override void OnEnter()
            {
                _boss.ConfigureAgent(_boss._baseSpeed, _boss._baseAcceleration, false);
                _boss.animator.SetBool(IsChasing, true);
            }

            protected override void OnExit()
            {
                _boss.animator.SetBool(IsChasing, false);
            }

            protected override NodeState OnTick()
            {
                if (_boss.Target is null) return NodeState.Failure;

                if (_boss.PlanarDistance() <= _boss.MeleeRange)
                {
                    _boss.navMeshAgent.isStopped = true;
                    return NodeState.Success;
                }

                _boss.navMeshAgent.isStopped = false;
                _boss.navMeshAgent.SetDestination(_boss.Target.position);
                return NodeState.Running;
            }
        }

        private sealed class WaitAction : ActionNode
        {
            private readonly BossEnemy _boss;
            private float _remaining;
            public WaitAction(BossEnemy boss) : base("Wait") { _boss = boss; }

            protected override void OnEnter()
            {
                _boss.ConfigureAgent(_boss._baseSpeed, _boss._baseAcceleration, true);
                _remaining = _boss.WaitTime;
            }

            protected override NodeState OnTick()
            {
                _remaining -= Time.deltaTime;
                return _remaining > 0f ? NodeState.Running : NodeState.Success;
            }
        }

        // Windup -> hit -> recovery. Committed: once started, no higher-priority branch cuts it off.
        private sealed class AttackAction : ActionNode
        {
            private readonly BossEnemy _boss;
            private readonly AttackKind _kind;
            private float _elapsed;
            private bool _hit;
            private bool _hasFacedTarget;

            public AttackAction(BossEnemy boss, AttackKind kind)
                : base(kind == AttackKind.Melee ? "MeleeAttack" : "AreaAttack")
            {
                _boss = boss;
                _kind = kind;
            }

            public override bool IsInterruptible => false;

            protected override void OnEnter()
            {
                _boss.ConfigureAgent(_boss._baseSpeed, _boss._baseAcceleration, true);
                _elapsed = 0f;
                _hit = false;
                _hasFacedTarget =  false;
            }

            protected override NodeState OnTick()
            {
                // PHASE 1: Rotation (animation and time are frozen until it rotates)
                if (!_hasFacedTarget)
                {
                    if (_boss.Target is null) return NodeState.Failure;

                    var direction = _boss.Target.position - _boss.transform.position;
                    direction.y = 0f;
                    
                    if (Vector3.Angle(_boss.transform.forward, direction) > 5f)
                    {
                        _boss.RotateTowards(_boss.Target.position);
                        return NodeState.Running;
                    }

                    // Stopped rotating
                    _hasFacedTarget = true;
                    _boss.animator.SetTrigger(HasAttacked); 
                }

                // PHASE 2: Attack (Animation and time starts)
                var melee = _kind == AttackKind.Melee;
                var windup = melee ? _boss.MeleeWindup : _boss.AreaWindup;
                var recovery = melee ? _boss.MeleeRecovery : _boss.AreaRecovery;

                _elapsed += Time.deltaTime;

                if (!_hit && _elapsed >= windup)
                {
                    _hit = true;
                    
                    // TODO para os Colisores:
                    // _boss.WeaponCollider.enabled = true;
                    // _boss.AreaCollider.enabled = true;
                }

                return _elapsed >= windup + recovery ? NodeState.Success : NodeState.Running;
            }

            // The cooldown starts when the attack ends.
            protected override void OnExit()
            {
                if (_kind == AttackKind.Melee) _boss._lastMeleeTime = Time.time;
                else _boss._lastAreaTime = Time.time;
                
                // TODO para os Colisores:
                // _boss.WeaponCollider.enabled = false;
                // _boss.AreaCollider.enabled = false;
            }
        }

        private sealed class RushAction : ActionNode
        {
            private const float StuckGraceSeconds = 0.3f;

            private readonly BossEnemy _boss;
            private float _startTime;
            public RushAction(BossEnemy boss) : base("Rush") { _boss = boss; }

            public override bool IsInterruptible => false;

            protected override void OnEnter()
            {
                _startTime = Time.time;
                _boss.ConfigureAgent(_boss.RushSpeed, _boss.RushAcceleration, false); // leaves the "stopped" state left by Chase/Wait
                
                var direction = (_boss.Target.position - _boss.transform.position).normalized;
                var farDestination = _boss.transform.position + direction * 50f;
                
                _boss.navMeshAgent.SetDestination(farDestination);          // locked destination: does not chase if the player dodges
                _boss.animator.SetBool(IsRushing, true);
            }

            protected override void OnExit()
            {
                _boss._lastRushTime = Time.time;
                _boss.animator.SetBool(IsRushing, false);
                
                if (_boss.navMeshAgent.isOnNavMesh)
                {
                    _boss.navMeshAgent.ResetPath();
                }
            }

            protected override NodeState OnTick()
            {
                var agent = _boss.navMeshAgent;
                var elapsed = Time.time - _startTime;
                
                if (agent.pathPending) return NodeState.Running; 
                if (agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid) return NodeState.Failure;

                if (elapsed > StuckGraceSeconds && agent.velocity.sqrMagnitude < 0.5f || elapsed > _boss.RushMaxDuration || agent.remainingDistance <= agent.stoppingDistance) 
                {
                    return NodeState.Success;
                }

                return NodeState.Running;
            }
        }
    }
}