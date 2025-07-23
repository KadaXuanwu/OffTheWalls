namespace Quantum {
    using Photon.Deterministic;
    using Quantum;
    using TMPro;
    using UnityEngine;

    public class LobbyUIController : MonoBehaviour {
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private UnityEngine.UI.Button startButton;
        [SerializeField] private TextMeshProUGUI playerListText;
        [SerializeField] private TextMeshProUGUI countdownText;

        private bool _isLobbyCreator;
        private GamePhase _lastGamePhase = GamePhase.Lobby;
        private bool _startGameTriggered;

        private void OnEnable() {
            QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
        }

        private void Start() {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        private void Update() {
            if (QuantumRunner.Default?.Game?.Frames?.Predicted == null)
                return;

            Frame frame = QuantumRunner.Default.Game.Frames.Predicted;

            if (frame.TryGetSingleton(out GameState gameState))
                UpdateUI(frame, gameState);
        }

        private void UpdateUI(Frame frame, GameState gameState) {
            // Handle phase transitions
            if (_lastGamePhase != gameState.CurrentPhase) {
                OnPhaseChanged(gameState.CurrentPhase);
                _lastGamePhase = gameState.CurrentPhase;
            }

            switch (gameState.CurrentPhase) {
                case GamePhase.Lobby:
                    UpdateLobbyUI(frame, gameState);
                    break;
                case GamePhase.Countdown:
                    UpdateCountdownUI(gameState);
                    break;
                case GamePhase.Playing:
                    HideAllUI();
                    break;
            }
        }

        private void OnPhaseChanged(GamePhase newPhase) {
            lobbyPanel.SetActive(newPhase == GamePhase.Lobby);
            countdownPanel.SetActive(newPhase == GamePhase.Countdown);
        }

        private void UpdateLobbyUI(Frame frame, GameState gameState) {
            // Update player list
            string playerList = "Players:\n";
            for (int i = 0; i < frame.MaxPlayerCount; i++) {
                PlayerRef player = i;
                DeterministicInputFlags flags = frame.GetPlayerInputFlags(i);
                bool isConnected = (flags & DeterministicInputFlags.PlayerNotPresent) == 0;

                if (isConnected) {
                    string prefix = (player == gameState.LobbyCreator) ? "[HOST] " : "";
                    playerList += $"{prefix}Player {player}\n";
                }
            }
            playerListText.text = playerList;

            // Update start button - check if local player is lobby creator
            _isLobbyCreator = QuantumRunner.Default.Game.PlayerIsLocal(gameState.LobbyCreator);
            startButton.interactable = _isLobbyCreator;
        }

        private void UpdateCountdownUI(GameState gameState) {
            int countdown = FPMath.CeilToInt(gameState.CountdownTimeRemaining);
            countdownText.text = countdown.ToString();
        }

        private void HideAllUI() {
            lobbyPanel.SetActive(false);
            countdownPanel.SetActive(false);
        }

        public void PollInput(CallbackPollInput callback) {
            Quantum.Input i = new();

            i.StartGame = _startGameTriggered;
            _startGameTriggered = false;

            callback.SetInput(i, DeterministicInputFlags.Repeatable);
        }
        private void OnStartButtonClicked() {
            if (!_isLobbyCreator) return;

            _startGameTriggered = true;
        }
    }
}
