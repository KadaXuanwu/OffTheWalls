using System;
using System.Collections.Generic;
using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    /// <summary>
    /// Database of all available upgrades in the game
    /// </summary>
    public partial class UpgradeDatabase : AssetObject {
        public AssetRef<UpgradeSpec>[] AllUpgrades;

        public unsafe QList<AssetRef<UpgradeSpec>> GetRandomUpgrades(Frame f, int count) {
            QList<AssetRef<UpgradeSpec>> selectedUpgrades = f.AllocateList<AssetRef<UpgradeSpec>>();
            
            if (AllUpgrades == null || AllUpgrades.Length == 0) {
                return selectedUpgrades;
            }

            List<AssetRef<UpgradeSpec>> availableUpgrades = new List<AssetRef<UpgradeSpec>>(AllUpgrades);
            int actualCount = Math.Min(count, availableUpgrades.Count);

            for (int i = 0; i < actualCount; i++) {
                if (availableUpgrades.Count == 0) break;

                // Use deterministic random
                int randomIndex = f.RNG->Next(0, availableUpgrades.Count);
                selectedUpgrades.Add(availableUpgrades[randomIndex]);
                availableUpgrades.RemoveAt(randomIndex);
            }

            return selectedUpgrades;
        }

        public unsafe AssetRef<UpgradeSpec> GetUpgradeByName(Frame f, string upgradeName) {
            foreach (AssetRef<UpgradeSpec> upgradeRef in AllUpgrades) {
                UpgradeSpec spec = f.FindAsset(upgradeRef);
                if (spec != null && spec.UpgradeName == upgradeName) {
                    return upgradeRef;
                }
            }
            return default;
        }
    }
}