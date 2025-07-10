namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class WeaponSystem : SystemMainThreadFilter<WeaponSystem.Filter>,
    ISignalSwitchWeapon, ISignalReloadWeapon, ISignalOnComponentRemoved<WeaponInventory> {

        public struct Filter {
            public EntityRef Entity;
            public WeaponInventory* WeaponInventory;
            public CharacterStats* Stats;
        }

        public override void Update(Frame f, ref Filter filter) {
            WeaponInstance* activeWeapon = WeaponHelper.GetActiveWeaponPointer(f, filter.Entity);

            if (activeWeapon == null) return;

            // Update weapon cooldowns
            if (activeWeapon->AttackCooldownRemaining > 0) {
                activeWeapon->AttackCooldownRemaining -= f.DeltaTime;
            }

            if (activeWeapon->ReloadTimeRemaining > 0) {
                activeWeapon->ReloadTimeRemaining -= f.DeltaTime;

                // Complete reload when timer reaches zero
                if (activeWeapon->ReloadTimeRemaining <= 0) {
                    WeaponSpec weaponSpec = f.FindAsset(activeWeapon->WeaponSpec);
                    CharacterSpec characterSpec = f.FindAsset(filter.Stats->Spec);

                    int maxAmmo = FPMath.RoundToInt(weaponSpec.MaxAmmo * characterSpec.MaxAmmoMultiplier);
                    activeWeapon->CurrentAmmo = maxAmmo;

                    // Update in owned weapons list
                    WeaponHelper.UpdateWeaponInOwnedList(f, filter.Entity, *activeWeapon);
                }
            }

            // Auto-reload when out of ammo and not currently reloading
            if (activeWeapon->CurrentAmmo <= 0 &&
                !activeWeapon->WeaponSpec.Id.Equals(default) &&
                activeWeapon->ReloadTimeRemaining <= 0) {
                ReloadWeapon(f, filter.Entity);
            }
        }

        public void SwitchWeapon(Frame f, EntityRef entity, int weaponIndex) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return;
            }

            // Cancel reload when switching weapons
            WeaponInstance* currentWeapon = WeaponHelper.GetActiveWeaponPointer(f, entity);
            if (currentWeapon != null && currentWeapon->ReloadTimeRemaining > 0) {
                currentWeapon->ReloadTimeRemaining = 0;
                WeaponHelper.UpdateWeaponInOwnedList(f, entity, *currentWeapon);
            }

            // Simple hand swap
            InventoryHelper.SwapHands(f, entity);
        }

        public void ReloadWeapon(Frame f, EntityRef entity) {
            WeaponInstance* activeWeapon = WeaponHelper.GetActiveWeaponPointer(f, entity);

            if (activeWeapon == null || activeWeapon->WeaponSpec.Id.Equals(default)) {
                return;
            }

            // Don't start reload if already reloading or at max ammo
            if (activeWeapon->ReloadTimeRemaining > 0) {
                return;
            }

            WeaponSpec weaponSpec = f.FindAsset(activeWeapon->WeaponSpec);
            CharacterSpec characterSpec = f.FindAsset(f.Unsafe.GetPointer<CharacterStats>(entity)->Spec);

            int maxAmmo = FPMath.RoundToInt(weaponSpec.MaxAmmo * characterSpec.MaxAmmoMultiplier);
            if (activeWeapon->CurrentAmmo >= maxAmmo) {
                return;
            }

            // Start reload timer
            FP reloadTime = weaponSpec.ReloadTime * characterSpec.ReloadTimeMultiplier;
            activeWeapon->ReloadTimeRemaining = reloadTime;

            // Update in owned weapons list
            WeaponHelper.UpdateWeaponInOwnedList(f, entity, *activeWeapon);
        }

        public void OnRemoved(Frame f, EntityRef entity, WeaponInventory* component) {
            f.FreeList(component->OwnedWeapons);
            component->OwnedWeapons = default;
        }
    }
}