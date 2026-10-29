using Evolution.Rooms;
using UnityEngine;
using PlayerClass = Player.Player;

namespace Enemies
{
    public class EnemySpawner : MonoBehaviour
    {
        public float SpawnInterval = 5f;
        
        [Tooltip("Enemies alive at the same time for this spawner. A new one spawns when one dies. -1 = no limit.")]
        public int MaxAlive = 3;
        
        public GameObject EnemyPrefab;
        
        private SpawnerSettings _settings;
        private bool _isConfigured;
        private bool _isStopped;
        private float _spawnTimer;
        private int _aliveCount;
        
        // Called by the GridBlock right after the spawner is created, with the values decoded from the genome.
        public void Configure(SpawnerSettings settings)
        {
            _settings = settings;
            _isConfigured = true;
            SpawnInterval = settings.SpawnInterval;
            MaxAlive = settings.MaxAlive;
        }
        
        private void Update()
        {
            if (_isStopped) return;

            if (_spawnTimer > 0f)
            {
                _spawnTimer -= Time.deltaTime;
                return;
            }

            if (MaxAlive > -1 && _aliveCount >= MaxAlive) return;

            Spawn();
            _spawnTimer = SpawnInterval;
        }
        
        private void Spawn()
        {
            var spawnRotation = Quaternion.identity;

            if (PlayerClass.PlayerInstance is not null)
            {
                var directionToPlayer = PlayerClass.PlayerInstance.transform.position - transform.position;
                directionToPlayer.y = 0f;

                if (directionToPlayer.sqrMagnitude > 0.001f)
                {
                    spawnRotation = Quaternion.LookRotation(directionToPlayer.normalized);
                }
            }

            var unitInstance = Instantiate(EnemyPrefab, transform.position, spawnRotation);
            var enemy = unitInstance.GetComponent<FsmEnemy>();
            enemy.CurrentState = FsmEnemy.EnemyState.Chasing;

            // Applied before the enemy's Start, so FsmEnemy copies AttackDamage to its hitbox.
            if (_isConfigured)
            {
                enemy.HealthPoints *= _settings.HealthMultiplier;
                enemy.AttackDamage *= _settings.DamageMultiplier;
                enemy.AttackCooldown = _settings.AttackCooldown;
            }

            _aliveCount++;
            enemy.Died += OnEnemyDied;
        }
        
        private void OnEnemyDied()
        {
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
        }
        
        public void StopSpawning()
        {
            _isStopped = true;
        }
    }
}
