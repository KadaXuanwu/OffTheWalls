namespace Quantum {
    using System;
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class CharacterSystem : SystemMainThreadFilter<CharacterSystem.Filter>,
    ISignalOnComponentAdded<CharacterStats>,
    ISignalOnComponentAdded<WeaponInventory> {

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
            // Initialize multipliers to 1
            component->MaxAmmoMultiplier = FP._1;
            component->ReloadTimeMultiplier = FP._1;
            component->AttackCooldownMultiplier = FP._1;
            component->MoveSpeedMultiplier = FP._1;
            component->DamageMultiplier = FP._1;
            component->MaxHealthMultiplier = FP._1;
            component->BulletSpeedMultiplier = FP._1;
            component->BounceDamageIncreaseMultiplier = FP._1;
            component->AdditionalBulletBounces = 0;

            CharacterSpec spec = f.FindAsset(component->Spec);
            component->CurrentHealth = spec.MaxHealth * component->MaxHealthMultiplier;
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
                    spec.MaxHealth * filter.Stats->MaxHealthMultiplier
                );
            }
        }

        private void UpdateCharacterShooting(Frame f, Filter filter, Input* input, CharacterSpec spec) {
            // Handle shooting logic
            if (input->Attack && WeaponHelper.CanShoot(f, filter.Entity)) {
                // Consume ammo from the weapon instance
                if (WeaponHelper.ConsumeAmmo(f, filter.Entity)) {
                    // Use CharacterStats multipliers instead of spec
                    WeaponHelper.SetAttackCooldown(f, filter.Entity, filter.Stats);

                    // Trigger shooting signal/event
                    f.Signals.CharacterShoot(filter.Entity);
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
    }
}