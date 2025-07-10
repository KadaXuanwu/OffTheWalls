// Example: Auto-equip weapons on game start
namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class GameInitializationSystem : SystemSignalsOnly, ISignalOnPlayerAdded {

        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
            // This runs after PlayerSpawnSystem creates the entity
            // Find the player's entity
            var playerEntities = f.Filter<PlayerLink>();
            while (playerEntities.NextUnsafe(out EntityRef entity, out PlayerLink* playerLink)) {
                if (playerLink->Player == player) {
                    // Get weapon database
                    WeaponDatabase weaponDB = f.FindAsset(f.RuntimeConfig.WeaponDatabase);

                    if (weaponDB != null && weaponDB.AllWeapons.Length >= 2) {
                        // Add first two weapons from database to inventory
                        InventoryHelper.AddWeaponToInventory(f, entity, weaponDB.AllWeapons[0]);
                        InventoryHelper.AddWeaponToInventory(f, entity, weaponDB.AllWeapons[1]);

                        // Or add specific weapons by name
                        // AssetRef<WeaponSpec> pistol = weaponDB.GetWeaponByName(f, "Pistol");
                        // AssetRef<WeaponSpec> shotgun = weaponDB.GetWeaponByName(f, "Shotgun");
                        // if (!pistol.Id.Equals(default)) {
                        //     InventoryHelper.AddWeaponToInventory(f, entity, pistol);
                        // }
                    }

                    break;
                }
            }
        }
    }
}