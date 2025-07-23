namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class PlayerSpawnSystem : SystemMainThread, ISignalOnPlayerAdded {
        private bool _hasSpawnedInitialPlayers = false;

        public override void Update(Frame f) {
            // Check if we need to spawn initial players when game starts
            if (!_hasSpawnedInitialPlayers &&
                f.TryGetSingleton<GameState>(out GameState gameState) &&
                gameState.CurrentPhase == GamePhase.Playing) {

                SpawnAllConnectedPlayers(f);
                _hasSpawnedInitialPlayers = true;
            }
        }

        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
            // Only handle late joiners during gameplay
            if (!f.TryGetSingleton<GameState>(out GameState gameState) ||
                gameState.CurrentPhase != GamePhase.Playing) {
                return;
            }

            // Spawn the late joiner
            SpawnPlayer(f, player);
        }

        private void SpawnAllConnectedPlayers(Frame f) {
            for (int i = 0; i < f.MaxPlayerCount; i++) {
                PlayerRef player = i;
                DeterministicInputFlags flags = f.GetPlayerInputFlags(i);
                bool isConnected = (flags & DeterministicInputFlags.PlayerNotPresent) == 0;

                if (isConnected) {
                    SpawnPlayer(f, player);
                }
            }
        }

        private void SpawnPlayer(Frame f, PlayerRef player) {
            RuntimePlayer runtimePlayer = f.GetPlayerData(player);
            EntityRef entity = f.Create(runtimePlayer.PlayerAvatar);

            f.Add(entity, new PlayerLink {
                Player = player
            });

            // Add player stats component
            f.Add(entity, new PlayerStats {
                Kills = 0,
                Deaths = 0,
                Assists = 0
            });

            // Add damage tracker component
            f.Add(entity, new DamageTracker());

            if (f.Unsafe.TryGetPointer<Transform2D>(entity, out Transform2D* transform)) {
                transform->Position = GetSpawnPosition(player);
            }
        }

        private FPVector2 GetSpawnPosition(PlayerRef player) {
            // Spread players around spawn area instead of linear
            FP angle = (FP._2 * FP.Pi * player) / 4; // Assuming max 4 players
            FP radius = FP._3;
            return new FPVector2(
                FPMath.Cos(angle) * radius,
                FPMath.Sin(angle) * radius
            );
        }
    }
}