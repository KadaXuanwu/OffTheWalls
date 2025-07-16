namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class DamageTrackingSystem : SystemMainThreadFilter<DamageTrackingSystem.Filter>,
        ISignalOnComponentAdded<DamageTracker> {

        public struct Filter {
            public EntityRef Entity;
            public DamageTracker* DamageTracker;
        }

        public override void Update(Frame f, ref Filter filter) {
            // Clean up old damage records
            var damageList = f.ResolveList(filter.DamageTracker->RecentDamage);
            FP currentTime = f.Number * f.DeltaTime;
            FP damageTimeout = 10; // 10 seconds

            // Remove old damage records
            for (int i = damageList.Count - 1; i >= 0; i--) {
                if (currentTime - damageList[i].Timestamp > damageTimeout) {
                    damageList.RemoveAt(i);
                }
            }
        }

        public void OnAdded(Frame f, EntityRef entity, DamageTracker* component) {
            // Initialize the damage list
            component->RecentDamage = f.AllocateList<DamageRecord>();
        }

        public static void RecordDamage(Frame f, EntityRef victim, EntityRef attacker, FP damage) {
            if (!f.Unsafe.TryGetPointer<DamageTracker>(victim, out DamageTracker* tracker)) {
                return;
            }

            var damageList = f.ResolveList(tracker->RecentDamage);

            // Check if we already have a record from this attacker
            bool found = false;
            for (int i = 0; i < damageList.Count; i++) {
                if (damageList[i].Attacker == attacker) {
                    // Update existing record
                    DamageRecord updated = damageList[i];
                    updated.Damage += damage;
                    updated.Timestamp = f.Number * f.DeltaTime;
                    damageList[i] = updated;
                    found = true;
                    break;
                }
            }

            // Add new record if not found
            if (!found) {
                damageList.Add(new DamageRecord {
                    Attacker = attacker,
                    Damage = damage,
                    Timestamp = f.Number * f.DeltaTime
                });
            }
        }
    }
}