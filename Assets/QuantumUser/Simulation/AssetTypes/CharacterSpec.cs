using System;
using Photon.Deterministic;

namespace Quantum {
    public partial class CharacterSpec : AssetObject {
        public FP MaxHealth = 100;
        public FP HealthRegenRate = 0;

        public FP MaxAmmoMultiplier = 1;
        public FP ReloadTimeMultiplier = FP._1;
        public FP AttackCooldownMultiplier = FP._1;
        public int AdditionalBounces = 0;

        public AssetRef<WeaponSpec>[] StartingWeapons;
        public AssetRef<WeaponSpec> DefaultMainHandWeapon;
        public AssetRef<WeaponSpec> DefaultOffHandWeapon;
    }
}