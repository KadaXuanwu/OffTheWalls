namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine.Scripting;
    using Quantum.Collections;

    [Preserve]
    public unsafe class UpgradeSystem : SystemSignalsOnly, 
        ISignalOnUpgradeSelected,
        ISignalOnComponentAdded<PlayerUpgrades> {

        public unsafe void OnUpgradeSelected(Frame f, EntityRef player, AssetRef<UpgradeSpec> upgradeSpec) {
            if (!f.Unsafe.TryGetPointer<CharacterStats>(player, out CharacterStats* stats)) {
                return;
            }

            if (!f.Unsafe.TryGetPointer<PlayerUpgrades>(player, out PlayerUpgrades* upgrades)) {
                return;
            }

            UpgradeSpec upgrade = f.FindAsset(upgradeSpec);
            if (upgrade == null) return;

            // Apply the upgrade
            ApplyUpgrade(f, player, stats, upgrade);

            // Track the upgrade
            TrackUpgrade(f, upgrades, upgradeSpec, upgrade);
        }

        public unsafe void OnAdded(Frame f, EntityRef entity, PlayerUpgrades* component) {
            component->OwnedUpgrades = f.AllocateList<UpgradeRecord>();
        }

        private unsafe void ApplyUpgrade(Frame f, EntityRef player, CharacterStats* stats, UpgradeSpec upgrade) {
            switch (upgrade.Type) {
                case UpgradeType.MaxAmmoMultiplier:
                    stats->MaxAmmoMultiplier *= upgrade.MultiplierValue;
                    break;
                case UpgradeType.ReloadTimeMultiplier:
                    stats->ReloadTimeMultiplier *= upgrade.MultiplierValue;
                    break;
                case UpgradeType.AttackCooldownMultiplier:
                    stats->AttackCooldownMultiplier *= upgrade.MultiplierValue;
                    break;
                case UpgradeType.MoveSpeedMultiplier:
                    stats->MoveSpeedMultiplier *= upgrade.MultiplierValue;
                    break;
                case UpgradeType.DamageMultiplier:
                    stats->DamageMultiplier *= upgrade.MultiplierValue;
                    break;
                case UpgradeType.MaxHealthMultiplier:
                    FP oldMaxHealth = GetMaxHealth(f, stats);
                    stats->MaxHealthMultiplier *= upgrade.MultiplierValue;
                    FP newMaxHealth = GetMaxHealth(f, stats);
                    // Scale current health proportionally
                    stats->CurrentHealth = (stats->CurrentHealth / oldMaxHealth) * newMaxHealth;
                    break;
                case UpgradeType.BulletSpeedMultiplier:
                    stats->BulletSpeedMultiplier *= upgrade.MultiplierValue;
                    break;
                case UpgradeType.BounceDamageIncreaseMultiplier:
                    stats->BounceDamageIncreaseMultiplier *= upgrade.MultiplierValue;
                    break;
                case UpgradeType.AdditionalBulletBounces:
                    stats->AdditionalBulletBounces += upgrade.IntegerValue;
                    break;
            }
        }

        private unsafe FP GetMaxHealth(Frame f, CharacterStats* stats) {
            CharacterSpec spec = f.FindAsset(stats->Spec);
            return spec.MaxHealth * stats->MaxHealthMultiplier;
        }

        private unsafe void TrackUpgrade(Frame f, PlayerUpgrades* upgrades, AssetRef<UpgradeSpec> upgradeSpec, UpgradeSpec upgrade) {
            var upgradeList = f.ResolveList(upgrades->OwnedUpgrades);

            // Find existing upgrade record
            for (int i = 0; i < upgradeList.Count; i++) {
                if (upgradeList[i].UpgradeSpec.Id == upgradeSpec.Id) {
                    if (upgrade.CanStack && upgradeList[i].StackCount < upgrade.MaxStacks) {
                        UpgradeRecord updated = upgradeList[i];
                        updated.StackCount++;
                        upgradeList[i] = updated;
                    }
                    return;
                }
            }

            // Add new upgrade record
            upgradeList.Add(new UpgradeRecord {
                UpgradeSpec = upgradeSpec,
                StackCount = 1
            });
        }

        public static unsafe void OfferUpgrades(Frame f, EntityRef player) {
            UpgradeDatabase upgradeDB = f.FindAsset(f.RuntimeConfig.UpgradeDatabase);
            if (upgradeDB == null) return;

            QList<AssetRef<UpgradeSpec>> offeredUpgrades = upgradeDB.GetRandomUpgrades(f, 3);
            f.Signals.OnUpgradeOffered(player, offeredUpgrades);
        }
    }
}