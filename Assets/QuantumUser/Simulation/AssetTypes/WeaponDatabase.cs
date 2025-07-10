using System;
using Photon.Deterministic;

namespace Quantum {
    /// <summary>
    /// Database of all available weapons in the game
    /// </summary>
    public partial class WeaponDatabase : AssetObject {
        public AssetRef<WeaponSpec>[] AllWeapons;

        public AssetRef<WeaponSpec> GetWeaponByName(Frame f, string weaponName) {
            foreach (AssetRef<WeaponSpec> weaponRef in AllWeapons) {
                WeaponSpec spec = f.FindAsset(weaponRef);
                if (spec != null && spec.WeaponName == weaponName) {
                    return weaponRef;
                }
            }
            return default;
        }

        public AssetRef<WeaponSpec> GetWeaponByIndex(int index) {
            if (index >= 0 && index < AllWeapons.Length) {
                return AllWeapons[index];
            }
            return default;
        }
    }
}