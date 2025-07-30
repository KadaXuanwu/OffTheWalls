namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class RespawnSystem : SystemMainThreadFilter<RespawnSystem.Filter>,
        ISignalOnPlayerDeath {

        public struct Filter {
            public EntityRef Entity;
            public RespawnTimer* RespawnTimer;
            public Transform2D* Transform;
            public PlayerLink* PlayerLink;
        }

        public override void Update(Frame f, ref Filter filter) {
            // Update respawn timer
            filter.RespawnTimer->TimeRemaining -= f.DeltaTime;

            if (filter.RespawnTimer->TimeRemaining <= 0) {
                // Respawn the player
                RespawnPlayer(f, filter);
            }
        }

        private void RespawnPlayer(Frame f, Filter filter) {
            // Remove respawn timer component
            f.Remove<RespawnTimer>(filter.Entity);

            // Reset health
            if (f.Unsafe.TryGetPointer<CharacterStats>(filter.Entity, out CharacterStats* stats)) {
                CharacterSpec spec = f.FindAsset(stats->Spec);
                stats->CurrentHealth = spec.MaxHealth * stats->MaxHealthMultiplier;
            }

            // Find a spawn point (you can customize this logic)
            FPVector2 spawnPosition = GetSpawnPosition(f, filter.PlayerLink->Player);
            filter.Transform->Position = spawnPosition;

            // Re-enable physics and colliders
            if (f.Unsafe.TryGetPointer<PhysicsBody2D>(filter.Entity, out PhysicsBody2D* body)) {
                body->Enabled = true;
            }

            if (f.Unsafe.TryGetPointer<PhysicsCollider2D>(filter.Entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = true;
            }

            // Signal respawn
            f.Signals.OnPlayerRespawn(filter.Entity);
        }

        private FPVector2 GetSpawnPosition(Frame f, PlayerRef player) {
            // Simple spawn logic - spawn at player number position
            // You can implement more complex spawn point selection here
            return new FPVector2(player * 2, 2);
        }

        public void OnPlayerDeath(Frame f, DeathInfo deathInfo) {
            // Add respawn timer to the victim
            if (f.Exists(deathInfo.Victim)) {
                // Default 5 second respawn time
                FP respawnTime = 5;

                f.Add(deathInfo.Victim, new RespawnTimer {
                    TimeRemaining = respawnTime,
                    TotalRespawnTime = respawnTime
                });

                // Disable physics/colliders while dead
                if (f.Unsafe.TryGetPointer<PhysicsBody2D>(deathInfo.Victim, out PhysicsBody2D* body)) {
                    body->Enabled = false;
                }

                if (f.Unsafe.TryGetPointer<PhysicsCollider2D>(deathInfo.Victim, out PhysicsCollider2D* collider)) {
                    collider->Enabled = false;
                }

                // Update death stats
                if (f.Unsafe.TryGetPointer<PlayerStats>(deathInfo.Victim, out PlayerStats* victimStats)) {
                    victimStats->Deaths++;
                }
            }

            // Update killer stats
            if (f.Exists(deathInfo.Killer) && deathInfo.Killer != EntityRef.None) {
                if (f.Unsafe.TryGetPointer<PlayerStats>(deathInfo.Killer, out PlayerStats* killerStats)) {
                    killerStats->Kills++;
                }
            }

            // Process assists
            ProcessAssists(f, deathInfo);
        }

        private void ProcessAssists(Frame f, DeathInfo deathInfo) {
            if (!f.Unsafe.TryGetPointer<DamageTracker>(deathInfo.Victim, out DamageTracker* tracker)) {
                return;
            }

            var damageList = f.ResolveList(tracker->RecentDamage);
            FP currentTime = f.Number * f.DeltaTime;
            FP assistWindow = 10; // 10 seconds

            // Award assists to everyone who damaged the victim recently (except the killer)
            for (int i = 0; i < damageList.Count; i++) {
                DamageRecord record = damageList[i];

                // Skip if damage is too old
                if (currentTime - record.Timestamp > assistWindow) {
                    continue;
                }

                // Skip the killer (they get the kill, not assist)
                if (record.Attacker == deathInfo.Killer) {
                    continue;
                }

                // Award assist
                if (f.Exists(record.Attacker) &&
                    f.Unsafe.TryGetPointer<PlayerStats>(record.Attacker, out PlayerStats* assistStats)) {
                    assistStats->Assists++;
                }
            }

            // Clear damage records on death
            damageList.Clear();
        }
    }
}