namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class UpgradeSelectionSystem : SystemMainThreadFilter<UpgradeSelectionSystem.Filter> {
        
        public struct Filter {
            public EntityRef Entity;
            public PlayerLink* PlayerLink;
            public PlayerUpgrades* Upgrades;
            public CharacterStats* Stats;
        }

        public override void Update(Frame f, ref Filter filter) {
            Input* input = f.GetPlayerInput(filter.PlayerLink->Player);
            
            if (input->SelectedUpgradeIndex > 0 && filter.Upgrades->HasPendingOffers) {
                int selectedIndex = input->SelectedUpgradeIndex - 1;
                var offers = f.ResolveList(filter.Upgrades->CurrentOffers);
                
                if (selectedIndex >= 0 && selectedIndex < offers.Count) {
                    // Apply the upgrade through the upgrade system
                    f.Signals.OnUpgradeSelected(filter.Entity, offers[selectedIndex]);
                    
                    // Clear offers
                    filter.Upgrades->HasPendingOffers = false;
                    offers.Clear();
                }
            }
        }
    }
}