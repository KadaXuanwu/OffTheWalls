namespace Quantum {
    using Photon.Deterministic;
    using Quantum;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class LobbySystem : SystemMainThread, ISignalOnPlayerAdded, ISignalOnPlayerRemoved {

        public override void Update(Frame f) {
            if (!f.TryGetSingleton(out GameState gameState)) {
                // Initialize game state
                gameState = new GameState {
                    CurrentPhase = GamePhase.Lobby,
                    LobbyCreator = GetFirstPlayer(f),
                    CountdownActive = false
                };
                f.SetSingleton(gameState);
                return;
            }

            UpdateGamePhase(f, ref gameState);
            f.SetSingleton(gameState);
        }

        private void UpdateGamePhase(Frame f, ref GameState gameState) {
            switch (gameState.CurrentPhase) {
                case GamePhase.Lobby:
                    HandleLobbyPhase(f, ref gameState);
                    break;
                case GamePhase.Countdown:
                    HandleCountdownPhase(f, ref gameState);
                    break;
                case GamePhase.Playing:
                    // Game is running
                    break;
            }
        }

        private void HandleLobbyPhase(Frame f, ref GameState gameState) {
            // Check if lobby creator pressed start
            if (IsValidLobbyCreator(f, gameState.LobbyCreator)) {
                Input* creatorInput = f.GetPlayerInput(gameState.LobbyCreator);
                if (creatorInput->StartGame.WasPressed) {
                    StartCountdown(ref gameState);
                }
            }
        }

        private void HandleCountdownPhase(Frame f, ref GameState gameState) {
            if (!gameState.CountdownActive) return;

            gameState.CountdownTimeRemaining -= f.DeltaTime;

            if (gameState.CountdownTimeRemaining <= 0) {
                StartGame(f, ref gameState);
            }
        }

        private void StartCountdown(ref GameState gameState) {
            gameState.CurrentPhase = GamePhase.Countdown;
            gameState.CountdownTimeRemaining = FP._3; // 3 seconds
            gameState.CountdownActive = true;
        }

        private void StartGame(Frame f, ref GameState gameState) {
            gameState.CurrentPhase = GamePhase.Playing;
            gameState.CountdownActive = false;

            // Trigger player spawning
            //SpawnAllPlayers(f);
        }

        private void SpawnAllPlayers(Frame f) {
            // Use the correct Quantum 3 API for iterating players
            for (int i = 0; i < f.MaxPlayerCount; i++) {
                PlayerRef player = i;

                // Check if player is connected using input flags
                DeterministicInputFlags flags = f.GetPlayerInputFlags(i);
                bool isConnected = (flags & DeterministicInputFlags.PlayerNotPresent) == 0;

                if (isConnected) {
                    RuntimePlayer runtimePlayer = f.GetPlayerData(player);
                    EntityRef entity = f.Create(runtimePlayer.PlayerAvatar);

                    f.Add(entity, new PlayerLink { Player = player });

                    if (f.Unsafe.TryGetPointer<Transform2D>(entity, out Transform2D* transform)) {
                        transform->Position = GetSpawnPosition(player);
                    }
                }
            }
        }

        private FPVector2 GetSpawnPosition(PlayerRef player) {
            // Spread players around spawn area
            FP angle = (FP._2 * FP.Pi * player) / 4; // Assuming max 4 players
            FP radius = FP._3;
            return new FPVector2(
                FPMath.Cos(angle) * radius,
                FPMath.Sin(angle) * radius
            );
        }

        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
            // Handle late joiners during gameplay
            if (f.TryGetSingleton<GameState>(out GameState gameState) &&
                gameState.CurrentPhase == GamePhase.Playing) {

                RuntimePlayer runtimePlayer = f.GetPlayerData(player);
                EntityRef entity = f.Create(runtimePlayer.PlayerAvatar);

                f.Add(entity, new PlayerLink { Player = player });

                if (f.Unsafe.TryGetPointer<Transform2D>(entity, out Transform2D* transform)) {
                    transform->Position = GetSpawnPosition(player);
                }
            }

            // Update lobby creator if this is the first player
            if (f.TryGetSingleton<GameState>(out gameState) && gameState.LobbyCreator == PlayerRef.None) {
                gameState.LobbyCreator = player;
                f.SetSingleton(gameState);
            }
        }

        public void OnPlayerRemoved(Frame f, PlayerRef player) {
            if (!f.TryGetSingleton<GameState>(out GameState gameState)) return;

            // Handle lobby creator disconnect
            if (gameState.LobbyCreator == player) {
                gameState.LobbyCreator = GetFirstPlayer(f);
            }

            f.SetSingleton(gameState);
        }

        private PlayerRef GetFirstPlayer(Frame f) {
            for (int i = 0; i < f.MaxPlayerCount; i++) {
                PlayerRef player = i;
                DeterministicInputFlags flags = f.GetPlayerInputFlags(i);
                bool isConnected = (flags & DeterministicInputFlags.PlayerNotPresent) == 0;

                if (isConnected) {
                    return player;
                }
            }
            return PlayerRef.None;
        }

        private bool IsValidLobbyCreator(Frame f, PlayerRef creator) {
            if (creator == PlayerRef.None) return false;

            DeterministicInputFlags flags = f.GetPlayerInputFlags(creator);
            return (flags & DeterministicInputFlags.PlayerNotPresent) == 0;
        }
    }
}