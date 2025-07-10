namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class PlayerSpawnSystem : SystemMainThread, ISignalOnPlayerAdded {
        public override void Update(Frame f) {
            
        }

        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
            RuntimePlayer runtimePlayer = f.GetPlayerData(player);
            EntityRef entity = f.Create(runtimePlayer.PlayerAvatar);

            f.Add(entity, new PlayerLink {
                Player = player
            });

            if (f.Unsafe.TryGetPointer<Transform2D>(entity, out Transform2D* transform)) {
                transform->Position = new FPVector2(player * 2, 2);
            }
        }
    }
}
