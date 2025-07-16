using Quantum;
using UnityEngine;

public class DeathOverlayController : QuantumEntityViewComponent<CustomViewContext> {
    [SerializeField] private GameObject deathOverlayPrefab;
    [SerializeField] private string deathTextFormat = "You were eliminated by {0}";
    [SerializeField] private string respawnTextFormat = "Respawning in {0:F1}";

    private DeathOverlayContext _deathOverlayContext;
    private bool _isLocal;
    private bool _isDead;
    private string _killerName = "Unknown";

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        // Check if local player
        if (frame.TryGet<PlayerLink>(EntityRef, out PlayerLink playerLink)) {
            _isLocal = Game.PlayerIsLocal(playerLink.Player);
        }

        if (!_isLocal) {
            return;
        }

        // Create death overlay UI
        if (deathOverlayPrefab != null) {
            GameObject overlay = Instantiate(deathOverlayPrefab);
            _deathOverlayContext = overlay.GetComponent<DeathOverlayContext>();

            // Initially hide the overlay
            overlay.SetActive(false);
        }
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        if (!_isLocal || _deathOverlayContext == null) return;

        // Check if player has respawn timer (is dead)
        bool hasRespawnTimer = PredictedFrame.Has<RespawnTimer>(EntityRef);

        if (hasRespawnTimer && !_isDead) {
            // Just died
            OnDeath();
        }
        else if (!hasRespawnTimer && _isDead) {
            // Just respawned
            OnRespawn();
        }

        // Update timer display if dead
        if (_isDead && PredictedFrame.TryGet<RespawnTimer>(EntityRef, out RespawnTimer respawnTimer)) {
            UpdateRespawnTimer(respawnTimer);
        }
    }

    private void OnDeath() {
        _isDead = true;

        if (_deathOverlayContext.deathOverlay != null) {
            _deathOverlayContext.deathOverlay.SetActive(true);
        }

        // Get killer info from the last damage dealer
        // In a real implementation, you'd pass this through the death event
        UpdateDeathMessage();
    }

    private void OnRespawn() {
        _isDead = false;

        if (_deathOverlayContext.deathOverlay != null) {
            _deathOverlayContext.deathOverlay.SetActive(false);
        }
    }

    private void UpdateDeathMessage() {
        if (_deathOverlayContext.deathMessageText != null) {
            _deathOverlayContext.deathMessageText.text = string.Format(deathTextFormat, _killerName);
        }
    }

    private void UpdateRespawnTimer(RespawnTimer respawnTimer) {
        if (_deathOverlayContext.respawnTimerText != null) {
            float timeRemaining = respawnTimer.TimeRemaining.AsFloat;
            _deathOverlayContext.respawnTimerText.text = string.Format(respawnTextFormat, timeRemaining);
        }
    }

    private void OnDestroy() {
        if (_deathOverlayContext != null && _deathOverlayContext.deathOverlay != null) {
            Destroy(_deathOverlayContext.deathOverlay);
        }
    }
}