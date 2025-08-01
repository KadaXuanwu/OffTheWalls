using System;
using Photon.Deterministic;

namespace Quantum {
    public partial class CharacterSpec : AssetObject {
        public FP MaxHealth = 100;
        public FP HealthRegenRate = 0;
        public FP DashCooldown = 5;

        public AssetRef<WeaponSpec>[] StartingWeapons;
        public AssetRef<WeaponSpec> DefaultMainHandWeapon;
        public AssetRef<WeaponSpec> DefaultOffHandWeapon;
    }
}