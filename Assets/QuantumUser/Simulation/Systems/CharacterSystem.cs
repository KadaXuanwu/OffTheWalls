namespace Quantum {
    using System;
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class CharacterSystem : SystemMainThreadFilter<CharacterSystem.Filter>,
    ISignalOnComponentAdded<CharacterStats>,
    ISignalOnComponentAdded<WeaponInventory> {

        private const int WallLayerMask = 1 << 6; // Same as TrajectoryHelper

        public override void Update(Frame f, ref Filter filter) {
            CharacterSpec spec = f.FindAsset(filter.Stats->Spec);

            // Update general stats
            UpdateCharacterStats(f, filter, spec);

            // Handle shooting if player has input
            if (f.Unsafe.TryGetPointer<PlayerLink>(filter.Entity, out PlayerLink* playerLink)) {
                Input* input = f.GetPlayerInput(playerLink->Player);
                UpdateCharacterShooting(f, filter, input, spec);
            }
        }

        public struct Filter {
            public EntityRef Entity;
            public CharacterStats* Stats;
            public Transform2D* Transform;
        }

        public void OnAdded(Frame f, EntityRef entity, CharacterStats* component) {
            CharacterSpec spec = f.FindAsset(component->Spec);
            component->CurrentHealth = spec.MaxHealth;
            // Initialize multipliers from spec
            component->MaxAmmoMultiplier = spec.MaxAmmoMultiplier;
            component->ReloadTimeMultiplier = spec.ReloadTimeMultiplier;
            component->AttackCooldownMultiplier = spec.AttackCooldownMultiplier;
        }

        public void OnAdded(Frame f, EntityRef entity, WeaponInventory* component) {
            // Initialize the lists
            component->OwnedWeapons = f.AllocateList<WeaponInstance>();
            component->IsMainHandActive = true;

            // Initialize with empty weapon instances
            component->MainHandWeapon = default;
            component->OffHandWeapon = default;

            // Initialize starting loadout
            if (f.Unsafe.TryGetPointer<CharacterStats>(entity, out CharacterStats* stats)) {
                CharacterSpec characterSpec = f.FindAsset(stats->Spec);
                InitializeWeaponInstances(f, entity, component, characterSpec);
            }
        }

        private void InitializeWeaponInstances(Frame f, EntityRef entity, WeaponInventory* weaponInventory, CharacterSpec characterSpec) {
            // Add starting weapons
            if (characterSpec.StartingWeapons != null) {
                foreach (AssetRef<WeaponSpec> weaponRef in characterSpec.StartingWeapons) {
                    if (!weaponRef.Id.Equals(default)) {
                        InventoryHelper.AddWeaponToInventory(f, entity, weaponRef);
                    }
                }
            }

            // Equip default weapons if specified
            if (!characterSpec.DefaultMainHandWeapon.Id.Equals(default)) {
                InventoryHelper.EquipWeaponToSlot(f, entity, characterSpec.DefaultMainHandWeapon, true);
            }

            if (!characterSpec.DefaultOffHandWeapon.Id.Equals(default)) {
                InventoryHelper.EquipWeaponToSlot(f, entity, characterSpec.DefaultOffHandWeapon, false);
            }
        }

        private void UpdateCharacterStats(Frame f, Filter filter, CharacterSpec spec) {
            // Your existing stat updates (health regen, etc.)
            if (filter.Stats->IsRegenerating) {
                filter.Stats->CurrentHealth = FPMath.Min(
                    filter.Stats->CurrentHealth + spec.HealthRegenRate * f.DeltaTime,
                    spec.MaxHealth
                );
            }
        }

        private void UpdateCharacterShooting(Frame f, Filter filter, Input* input, CharacterSpec spec) {
            // Handle shooting logic
            if (input->Attack && WeaponHelper.CanShoot(f, filter.Entity)) {
                // Check if projectile spawn position would be inside a wall
                if (CanSpawnProjectile(f, filter.Entity)) {
                    // Consume ammo from the weapon instance
                    if (WeaponHelper.ConsumeAmmo(f, filter.Entity)) {
                        WeaponHelper.SetAttackCooldown(f, filter.Entity, spec);

                        // Trigger shooting signal/event
                        f.Signals.CharacterShoot(filter.Entity);
                    }
                }
                // If spawn position is blocked, still consume cooldown and ammo to prevent spam
                else {
                    if (WeaponHelper.ConsumeAmmo(f, filter.Entity)) {
                        WeaponHelper.SetAttackCooldown(f, filter.Entity, spec);
                    }
                }
            }

            // Handle weapon switching
            bool currentSwitch = input->SwitchWeapon;
            bool lastSwitch = filter.Stats->SwitchWeaponPressedLastFrame;
            if (currentSwitch && !lastSwitch) {
                f.Signals.SwitchWeapon(filter.Entity, -1);
            }

            filter.Stats->SwitchWeaponPressedLastFrame = currentSwitch;
        }

        /// <summary>
        /// Checks if a projectile can be spawned at the calculated position without being inside a wall.
        /// Uses the same position calculation logic as ProjectileSystem.CharacterShoot.
        /// </summary>
        private bool CanSpawnProjectile(Frame f, EntityRef owner) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(owner, out WeaponInventory* weaponInventory)) {
                return false;
            }

            if (!f.Unsafe.TryGetPointer<Transform2D>(owner, out Transform2D* ownerTransform)) {
                return false;
            }

            WeaponInstance activeWeapon = weaponInventory->IsMainHandActive ?
                weaponInventory->MainHandWeapon :
                weaponInventory->OffHandWeapon;

            if (activeWeapon.WeaponSpec.Id.Equals(default)) {
                return false;
            }

            WeaponSpec weaponSpec = f.FindAsset(activeWeapon.WeaponSpec);
            ProjectileSpec projectileSpec = f.FindAsset(weaponSpec.ProjectileSpec);

            // Calculate spawn position using same logic as ProjectileSystem.CharacterShoot
            FP adjustedRotation = ownerTransform->Rotation + FP.PiOver2;
            FPVector2 forwardDirection = new FPVector2(
                FPMath.Cos(adjustedRotation),
                FPMath.Sin(adjustedRotation)
            );

            // Use same offset calculation as ProjectileSystem
            FPVector2 baseOffset = weaponInventory->IsMainHandActive ?
                new FPVector2(FP._0_50, -FP._0_50) :
                new FPVector2(-FP._0_50, -FP._0_50);

            FP characterRotation = ownerTransform->Rotation;
            FPVector2 rotatedOffset = new FPVector2(
                baseOffset.X * FPMath.Cos(characterRotation) - baseOffset.Y * FPMath.Sin(characterRotation),
                baseOffset.X * FPMath.Sin(characterRotation) + baseOffset.Y * FPMath.Cos(characterRotation)
            );

            FPVector2 spawnPosition = ownerTransform->Position + forwardDirection * projectileSpec.ShotOffset + rotatedOffset;

            // Raycast FROM character position TO spawn position to check for walls in between
            FPVector2 directionToSpawn = spawnPosition - ownerTransform->Position;
            FP distanceToSpawn = directionToSpawn.Magnitude;

            if (distanceToSpawn <= FP._0) {
                return true; // Spawn position is same as character position
            }

            FPVector2 normalizedDirection = directionToSpawn.Normalized;

            var hit = f.Physics2D.Raycast(
                ownerTransform->Position,
                normalizedDirection,
                distanceToSpawn,
                WallLayerMask
            );

            // If we hit a wall between character and spawn position, prevent spawning
            if (hit.HasValue && f.Has<Wall>(hit.Value.Entity)) {
                return false;
            }

            return true;
        }
    }
}