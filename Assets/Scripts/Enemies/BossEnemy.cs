using System;
using System.Collections.Generic;
using BehaviourTree;
using UnityEngine;

namespace Enemies
{
    // Boss driven by a Behaviour Tree. The tree comes from a GE genome (BossGrammar) or, if the genome
    // is empty, from a hand-built tree (baseline).
    //
    // Golden rule for the GE: EVERY leaf can be called in any order and at any time.
    // That is why each action configures the whole NavMeshAgent in OnEnter (stopped? speed? acceleration?)
    // and does not depend on any other action having run before.
    public partial class BossEnemy : Enemy, IBossLeaves
    {
        [Header("Evolution")]
        [Tooltip("GE's Genome. Empty = uses baseline tree.")]
        public int[] Genome;
        public bool LogTree = true;

        [Header("Distances")]
        public float MeleeRange = 2.5f;
        public float AreaAttackRange = 5f;
        public float RushRange = 15f;

        [Header("Melee")]
        public float MeleeWindup = 0.4f;
        public float MeleeRecovery = 0.6f;
        public float MeleeCooldown = 1.5f;

        [Header("Area Attack")]
        public float AreaWindup = 0.8f;
        public float AreaRecovery = 0.8f;
        public float AreaCooldown = 8f;

        [Header("Rush")]
        public float RushSpeed = 12f;
        public float RushAcceleration = 20f;
        public float RushCooldown = 5f;
        public float RushMaxDuration = 2f;
        public float RushStopDistance = 1.5f;

        [Header("Wait")]
        public float WaitTime = 2f;

        private Node _tree;
        private float _baseSpeed;
        private float _baseAcceleration;
        private float _maxHealth;
        private float _lastMeleeTime;
        private float _lastAreaTime;
        private float _lastRushTime;

        private new void Start()
        {
            base.Start();

            _baseSpeed = navMeshAgent.speed;
            _baseAcceleration = navMeshAgent.acceleration;
            _maxHealth = HealthPoints;

            navMeshAgent.stoppingDistance = Mathf.Min(navMeshAgent.stoppingDistance, MeleeRange * 0.8f);

            // Cooldowns start ready.
            _lastMeleeTime = -MeleeCooldown;
            _lastAreaTime = -AreaCooldown;
            _lastRushTime = -RushCooldown;

            BuildTree();
        }

        public new void Update()
        {
            base.Update();
            if (_tree == null || Target is null || HealthPoints <= 0f) return;
            _tree.Evaluate();
        }

        // Called by the GP: the boss receives the genome and builds ITS OWN tree.
        public void SetGenome(int[] genome)
        {
            Genome = genome;
            BuildTree();
        }

        [ContextMenu("Log tree")]
        private void LogCurrentTree()
        {
            Debug.Log(_tree != null ? _tree.Describe() : "No current tree (Play the Game).");
        }

        private void BuildTree()
        {
            _tree?.Abort();
            _tree = Genome is { Length: > 0 }
                ? BossGrammar.BuildBossTree(Genome, this)
                : BuildBaselineTree();

            if (LogTree) Debug.Log("Boss tree: " + _tree.Describe());
        }

        // Hand-built tree: serves as a baseline to compare against the evolved ones.
        private Node BuildBaselineTree()
        {
            return new Selector(new List<Node>
            {
                Branch(Cond("CanArea"), Cond("InArea"), CreateAction("AreaAttack"), CreateAction("Wait")),
                Branch(Cond("CanMelee"), Cond("InMelee"), CreateAction("MeleeAttack"), CreateAction("Wait")),
                Branch(Cond("CanRush"), Cond("InRushRange"), new Inverter(Cond("InArea")), CreateAction("Rush"), CreateAction("Wait")),
                CreateAction("Chase")
            });

            Node Branch(params Node[] nodes) => new Sequence(new List<Node>(nodes));
            Node Cond(string n) => new ConditionNode(n, GetCondition(n));
        }
        
        public Node CreateAction(string actionName)
        {
            return actionName switch
            {
                "Chase" => new ChaseAction(this),
                "Wait" => new WaitAction(this),
                "MeleeAttack" => new AttackAction(this, AttackKind.Melee),
                "AreaAttack" => new AttackAction(this, AttackKind.Area),
                "Rush" => new RushAction(this),
                _ => throw new ArgumentException("Unknown Action: " + actionName)
            };
        }

        public Func<bool> GetCondition(string conditionName)
        {
            return conditionName switch
            {
                "InMelee" => () => PlanarDistance() <= MeleeRange,
                "InArea" => () => PlanarDistance() <= AreaAttackRange,
                "InRushRange" => () => PlanarDistance() <= RushRange,
                "CanMelee" => () => Time.time >= _lastMeleeTime + MeleeCooldown,
                "CanArea" => () => Time.time >= _lastAreaTime + AreaCooldown,
                "CanRush" => () => Time.time >= _lastRushTime + RushCooldown,
                "HealthLow" => () => HealthPoints <= _maxHealth * 0.5f,
                _ => throw new ArgumentException("Unknown Condition: " + conditionName)
            };
        }

        // ---------- Helpers ----------
        
        private float PlanarDistance()
        {
            if (Target is null) return float.PositiveInfinity;
            var d = Target.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private void ConfigureAgent(float speed, float acceleration, bool stopped)
        {
            navMeshAgent.speed = speed;
            navMeshAgent.acceleration = acceleration;
            navMeshAgent.isStopped = stopped;
        }

        private enum AttackKind { Melee, Area }

        private void DealHit(AttackKind kind)
        {
            var range = kind == AttackKind.Melee ? MeleeRange : AreaAttackRange;
            if (PlanarDistance() > range) return; // the player escaped during the windup

            // TODO: apply the damage here (e.g. your Player's OnHit, with knockback from transform.position).
        }
    }
}