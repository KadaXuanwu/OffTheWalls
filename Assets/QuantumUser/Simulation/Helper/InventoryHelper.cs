using System;
using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe class InventoryHelper {
        public static void AddWeaponToInventory(Frame f, EntityRef entity, AssetRef<WeaponSpec> weaponSpecRef) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return;
            }

            var ownedWeapons = f.ResolveList(weaponInventory->OwnedWeapons);
            WeaponSpec weaponSpec = f.FindAsset(weaponSpecRef);

            // Check if weapon already owned
            for (int i = 0; i < ownedWeapons.Count; i++) {
                if (ownedWeapons[i].WeaponSpec.Id == weaponSpecRef.Id) {
                    return;
                }
            }

            // Create new weapon instance
            WeaponInstance newWeapon = new WeaponInstance {
                WeaponSpec = weaponSpecRef,
                CurrentAmmo = weaponSpec.MaxAmmo,
                AttackCooldownRemaining = 0,
                ReloadTimeRemaining = 0,
            };

            ownedWeapons.Add(newWeapon);

            // Auto-equip if slots are empty
            if (weaponInventory->MainHandWeapon.WeaponSpec.Id.Equals(default)) {
                weaponInventory->MainHandWeapon = newWeapon;
                weaponInventory->IsMainHandActive = true;
            }
            else if (weaponInventory->OffHandWeapon.WeaponSpec.Id.Equals(default)) {
                weaponInventory->OffHandWeapon = newWeapon;
            }
        }

        public static void EquipWeaponToSlot(Frame f, EntityRef entity, AssetRef<WeaponSpec> weaponSpecRef, bool toMainHand) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return;
            }

            var ownedWeapons = f.ResolveList(weaponInventory->OwnedWeapons);

            // Find weapon in owned inventory
            for (int i = 0; i < ownedWeapons.Count; i++) {
                if (ownedWeapons[i].WeaponSpec.Id == weaponSpecRef.Id) {
                    if (toMainHand) {
                        weaponInventory->MainHandWeapon = ownedWeapons[i];
                    }
                    else {
                        weaponInventory->OffHandWeapon = ownedWeapons[i];
                    }
                    break;
                }
            }
        }

        public static void SwapHands(Frame f, EntityRef entity) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return;
            }

            weaponInventory->IsMainHandActive = !weaponInventory->IsMainHandActive;
        }

        public static WeaponInstance GetActiveWeapon(Frame f, EntityRef entity) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return default;
            }

            return weaponInventory->IsMainHandActive ?
                weaponInventory->MainHandWeapon :
                weaponInventory->OffHandWeapon;
        }

        public static void RemoveWeaponFromInventory(Frame f, EntityRef entity, AssetRef<WeaponSpec> weaponSpecRef) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(entity, out WeaponInventory* weaponInventory)) {
                return;
            }

            var ownedWeapons = f.ResolveList(weaponInventory->OwnedWeapons);

            for (int i = 0; i < ownedWeapons.Count; i++) {
                if (ownedWeapons[i].WeaponSpec.Id == weaponSpecRef.Id) {
                    ownedWeapons.RemoveAt(i);

                    // Clear from equipped slots if necessary
                    if (weaponInventory->MainHandWeapon.WeaponSpec.Id == weaponSpecRef.Id) {
                        weaponInventory->MainHandWeapon = default;
                    }
                    if (weaponInventory->OffHandWeapon.WeaponSpec.Id == weaponSpecRef.Id) {
                        weaponInventory->OffHandWeapon = default;
                    }

                    break;
                }
            }
        }
    }
}