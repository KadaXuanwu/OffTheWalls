using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;

namespace Quantum {
    public unsafe class WeaponHelper {
        public static bool CanShoot(Frame f, EntityRef entity) {
            if (f.Has<RespawnTimer>(entity)) {
                return false;
            }

            WeaponInstance* activeWeapon = GetActiveWeaponPointer(f, entity);

            if (activeWeapon == null || activeWeapon->WeaponSpec.Id.Equals(default)) {
                return false;
            }

            return activeWeapon->CurrentAmmo > 0 &&
                   activeWeapon->AttackCooldownRemaining <= 0 &&
                   activeWeapon->ReloadTimeRemaining <= 0;
        }

        public static bool ConsumeAmmo(Frame f, EntityRef entity) {
            WeaponInstance* activeWeapon = GetActiveWeaponPointer(f, entity);

            if (activeWeapon == null ||
                activeWeapon->CurrentAmmo <= 0 ||
                activeWeapon->WeaponSpec.Id.Equals(default) ||
                activeWeapon->ReloadTimeRemaining > 0) {
                return false;
            }

            activeWeapon->CurrentAmmo--;

            // Update in owned weapons list
            UpdateWeaponInOwnedList(f, entity, *activeWeapon);

            return true;
        }

        public static void SetAttackCooldown(Frame f, EntityRef entity, CharacterStats* characterStats) {
            WeaponInstance* activeWeapon = GetActiveWeaponPointer(f, entity);

            if (activeWeapon == null || activeWeapon->WeaponSpec.Id.Equals(default)) {
                return;
            }

            WeaponSpec weaponSpec = f.FindAsset(activeWeapon->WeaponSpec);
            FP cooldown = weaponSpec.AttackCooldown * characterStats->AttackCooldownMultiplier;
            activeWeapon->AttackCooldownRemaining = cooldown;

            // Update in owned weapons list
            UpdateWeaponInOwnedList(f, entity, *activeWeapon);
        }

        public static WeaponInstance* GetActiveWeaponPointer(Frame f, EntityRef entity) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return null;
            }

            return weaponInventory->IsMainHandActive ?
                &weaponInventory->MainHandWeapon :
                &weaponInventory->OffHandWeapon;
        }

        public static void UpdateWeaponInOwnedList(Frame f, EntityRef entity, WeaponInstance updatedWeapon) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return;
            }

            QList<WeaponInstance> ownedWeapons = f.ResolveList(weaponInventory->OwnedWeapons);
            for (int i = 0; i < ownedWeapons.Count; i++) {
                if (ownedWeapons[i].WeaponSpec.Id == updatedWeapon.WeaponSpec.Id) {
                    ownedWeapons[i] = updatedWeapon;
                    break;
                }
            }
        }

        public static FP GetEffectiveAttackCooldown(Frame f, EntityRef entity, CharacterStats* characterStats) {
            WeaponInstance activeWeapon = GetActiveWeapon(f, entity);

            if (activeWeapon.WeaponSpec.Id.Equals(default)) {
                return -1;
            }

            WeaponSpec weaponSpec = f.FindAsset(activeWeapon.WeaponSpec);
            return weaponSpec.AttackCooldown * characterStats->AttackCooldownMultiplier;
        }

        public static int GetCurrentAmmo(Frame f, EntityRef entity) {
            WeaponInstance activeWeapon = GetActiveWeapon(f, entity);
            return activeWeapon.CurrentAmmo;
        }

        public static int GetMaxAmmo(Frame f, EntityRef entity, CharacterStats* characterStats) {
            WeaponInstance activeWeapon = GetActiveWeapon(f, entity);

            if (activeWeapon.WeaponSpec.Id.Equals(default)) {
                return 0;
            }

            WeaponSpec weaponSpec = f.FindAsset(activeWeapon.WeaponSpec);
            return FPMath.RoundToInt(weaponSpec.MaxAmmo * characterStats->MaxAmmoMultiplier);
        }

        public static FP GetEffectiveReloadTime(Frame f, EntityRef entity, CharacterStats characterStats) {
            WeaponInstance activeWeapon = GetActiveWeapon(f, entity);

            if (activeWeapon.WeaponSpec.Id.Equals(default)) {
                return -1;
            }

            WeaponSpec weaponSpec = f.FindAsset(activeWeapon.WeaponSpec);
            return weaponSpec.ReloadTime * characterStats.ReloadTimeMultiplier;
        }

        public static WeaponSpec GetCurrentWeaponSpec(Frame f, EntityRef entity) {
            WeaponInstance activeWeapon = GetActiveWeapon(f, entity);

            if (activeWeapon.WeaponSpec.Id.Equals(default)) {
                return default;
            }

            return f.FindAsset(activeWeapon.WeaponSpec);
        }

        public static WeaponInstance GetActiveWeapon(Frame f, EntityRef entity) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return default;
            }

            return weaponInventory->IsMainHandActive ?
                weaponInventory->MainHandWeapon :
                weaponInventory->OffHandWeapon;
        }

        public static bool IsReloading(Frame f, EntityRef entity) {
            WeaponInstance* activeWeapon = GetActiveWeaponPointer(f, entity);
            return activeWeapon != null && activeWeapon->ReloadTimeRemaining > 0;
        }

        public static FP GetReloadProgress(Frame f, EntityRef entity, CharacterStats* characterStats) {
            WeaponInstance* activeWeapon = GetActiveWeaponPointer(f, entity);

            if (activeWeapon == null || activeWeapon->ReloadTimeRemaining <= 0) {
                return 0;
            }

            WeaponSpec weaponSpec = f.FindAsset(activeWeapon->WeaponSpec);
            FP totalReloadTime = weaponSpec.ReloadTime * characterStats->ReloadTimeMultiplier;

            return FP._1 - (activeWeapon->ReloadTimeRemaining / totalReloadTime);
        }
    }
}