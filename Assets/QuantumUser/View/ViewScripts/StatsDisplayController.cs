using Quantum;
using TMPro;
using UnityEngine;

public class StatsDisplayController : QuantumEntityViewComponent<CustomViewContext> {
    [SerializeField] private GameObject statsUIPrefab;
    [SerializeField] private Vector2 anchorPosition = new Vector2(10, 10);
    [SerializeField] private string statsFormat = "K: {0} / D: {1} / A: {2}";

    private StatsDisplayContext _statsContext;
    private PlayerStats _lastStats;
    private bool _isLocal;

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        // Check if local player
        if (frame.TryGet<PlayerLink>(EntityRef, out PlayerLink playerLink)) {
            _isLocal = Game.PlayerIsLocal(playerLink.Player);
        }

        if (!_isLocal) return;

        // Create stats UI
        if (statsUIPrefab != null) {
            GameObject statsUI = Instantiate(statsUIPrefab);
            _statsContext = statsUI.GetComponent<StatsDisplayContext>();

            // Position the UI
            if (_statsContext.statsPanel != null) {
                RectTransform rect = _statsContext.statsPanel.GetComponent<RectTransform>();
                if (rect != null) {
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.anchoredPosition = anchorPosition;
                }
            }
        }
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        if (!_isLocal || _statsContext == null) return;

        if (PredictedFrame.TryGet<PlayerStats>(EntityRef, out PlayerStats currentStats)) {
            // Only update if stats changed
            if (HasStatsChanged(currentStats)) {
                UpdateStatsDisplay(currentStats);
                _lastStats = currentStats;
            }
        }
    }

    private bool HasStatsChanged(PlayerStats current) {
        return current.Kills != _lastStats.Kills ||
               current.Deaths != _lastStats.Deaths ||
               current.Assists != _lastStats.Assists;
    }

    private void UpdateStatsDisplay(PlayerStats stats) {
        // Optional: Update individual stat displays if you have them
        if (_statsContext.killsText != null) {
            _statsContext.killsText.text = stats.Kills.ToString();
        }

        if (_statsContext.deathsText != null) {
            _statsContext.deathsText.text = stats.Deaths.ToString();
        }

        if (_statsContext.assistsText != null) {
            _statsContext.assistsText.text = stats.Assists.ToString();
        }

        // Calculate and display K/D ratio if you have a field for it
        if (_statsContext.kdRatioText != null) {
            float kdRatio = stats.Deaths > 0 ? (float)stats.Kills / stats.Deaths : stats.Kills;
            _statsContext.kdRatioText.text = $"K/D: {kdRatio:F2}";
        }
    }

    private void OnDestroy() {
        if (_statsContext != null && _statsContext.statsPanel != null) {
            Destroy(_statsContext.statsPanel);
        }
    }
}