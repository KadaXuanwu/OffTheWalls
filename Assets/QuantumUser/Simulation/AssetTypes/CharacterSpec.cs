using System;
using Photon.Deterministic;

namespace Quantum {
    public partial class CharacterSpec : AssetObject {
        public FP MaxHealth = 100;
        public FP HealthRegenRate = 0;

        // Existing multipliers
        public FP MaxAmmoMultiplier = 1;
        public FP ReloadTimeMultiplier = FP._1;
        public FP AttackCooldownMultiplier = FP._1;
        
        // New multipliers
        public FP MoveSpeedMultiplier = FP._1;
        public FP DamageMultiplier = FP._1;
        public FP MaxHealthMultiplier = FP._1;
        public FP BulletSpeedMultiplier = FP._1;
        public FP BounceDamageIncreaseMultiplier = FP._1;
        public int AdditionalBulletBounces = 0;

        public AssetRef<WeaponSpec>[] StartingWeapons;
        public AssetRef<WeaponSpec> DefaultMainHandWeapon;
        public AssetRef<WeaponSpec> DefaultOffHandWeapon;
    }
}