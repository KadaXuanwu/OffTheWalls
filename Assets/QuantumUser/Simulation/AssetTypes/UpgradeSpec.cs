using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public enum UpgradeType {
        MaxAmmoMultiplier,
        ReloadTimeMultiplier,
        AttackCooldownMultiplier,
        MoveSpeedMultiplier,
        DamageMultiplier,
        MaxHealthMultiplier,
        BulletSpeedMultiplier,
        BounceDamageIncreaseMultiplier,
        AdditionalBulletBounces
    }

    /// <summary>
    /// Defines an upgrade that can be applied to a character
    /// </summary>
    public partial class UpgradeSpec : AssetObject {
        [Header("Basic Info")]
        public string UpgradeName;
        public string Description;
        public Sprite UpgradeIcon;

        [Header("Upgrade Settings")]
        public UpgradeType Type;
        
        [Header("Values")]
        public FP MultiplierValue = FP._1; // For multiplier upgrades
        public int IntegerValue = 0; // For integer upgrades like AdditionalBulletBounces
        
        [Header("Stacking")]
        public bool CanStack = true;
        public int MaxStacks = 5;
    }
}