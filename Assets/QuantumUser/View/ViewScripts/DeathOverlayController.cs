using Photon.Deterministic;
using Quantum;
using UnityEngine;

public unsafe class DeathOverlayController : QuantumEntityViewComponent<CustomViewContext> {
    [SerializeField] private GameObject deathOverlayPrefab;
    [SerializeField] private string deathTextFormat = "You were eliminated by {0}";
    [SerializeField] private string respawnTextFormat = "Respawning in {0:F1}";

    private DeathOverlayContext _deathOverlayContext;
    private bool _isLocal;
    private bool _isDead;
    private bool _upgradesOffered;
    private string _killerName = "Unknown";
    private AssetRef<UpgradeSpec>[] _currentUpgradeOffers;

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

            // Setup upgrade button callbacks
            SetupUpgradeButtons();

            // Initially hide the overlay
            overlay.SetActive(false);
        }
    }

    private void SetupUpgradeButtons() {
        if (_deathOverlayContext?.upgradeButtons == null) return;

        for (int i = 0; i < _deathOverlayContext.upgradeButtons.Length; i++) {
            int buttonIndex = i; // Capture for closure
            _deathOverlayContext.upgradeButtons[i].button?.onClick.AddListener(() => OnUpgradeButtonClicked(buttonIndex));
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

        // Check for offered upgrades (stored in a temporary component or signal)
        CheckForUpgradeOffers();
    }

    private void CheckForUpgradeOffers() {
        // For now, we'll trigger upgrades after death manually
        // You can expand this to check for a component that stores offered upgrades
        if (_isDead && !_upgradesOffered) {
            // Trigger upgrade offers after 1 second
            Invoke(nameof(ShowUpgradeOptions), 1.0f);
        }
    }

    private void ShowUpgradeOptions() {
        if (!_isDead) return;

        Debug.Log("Hi");
        // Get upgrades from the database manually for now
        // In a full implementation, this would come from the simulation
        Frame frame = QuantumRunner.Default.Game.Frames.Verified;
        UpgradeDatabase upgradeDB = frame.FindAsset(frame.RuntimeConfig.UpgradeDatabase);
        if (upgradeDB == null) return;

        // Manually create upgrade offers (in real implementation, this comes from simulation)
        _currentUpgradeOffers = new AssetRef<UpgradeSpec>[3];
        int availableCount = Mathf.Min(3, upgradeDB.AllUpgrades.Length);
        
        for (int i = 0; i < availableCount; i++) {
            _currentUpgradeOffers[i] = upgradeDB.AllUpgrades[i];
        }

        _upgradesOffered = true;

        // Show upgrade panel
        if (_deathOverlayContext.upgradePanel != null) {
            Debug.Log("activating");
            _deathOverlayContext.upgradePanel.SetActive(true);
        }

        // Setup upgrade buttons with the offered upgrades
        SetupUpgradeOptionsUI();
    }

    private void SetupUpgradeOptionsUI() {
        if (_deathOverlayContext?.upgradeButtons == null || _currentUpgradeOffers == null) return;

        Frame frame = QuantumRunner.Default.Game.Frames.Verified;

        for (int i = 0; i < _deathOverlayContext.upgradeButtons.Length && i < _currentUpgradeOffers.Length; i++) {
            if (_currentUpgradeOffers[i].Id.Equals(default)) continue;

            UpgradeSpec upgrade = frame.FindAsset(_currentUpgradeOffers[i]);
            if (upgrade == null) continue;

            UpgradeButtonContext buttonContext = _deathOverlayContext.upgradeButtons[i];

            // Set upgrade icon
            if (buttonContext.upgradeIcon != null && upgrade.UpgradeIcon != null) {
                buttonContext.upgradeIcon.sprite = upgrade.UpgradeIcon;
            }

            // Set upgrade name
            if (buttonContext.upgradeNameText != null) {
                buttonContext.upgradeNameText.text = upgrade.UpgradeName;
            }

            // Set upgrade description
            if (buttonContext.upgradeDescriptionText != null) {
                buttonContext.upgradeDescriptionText.text = upgrade.Description;
            }

            // Enable button
            if (buttonContext.button != null) {
                buttonContext.button.gameObject.SetActive(true);
                buttonContext.button.interactable = true;
            }
        }

        // Hide unused buttons
        for (int i = _currentUpgradeOffers.Length; i < _deathOverlayContext.upgradeButtons.Length; i++) {
            if (_deathOverlayContext.upgradeButtons[i].button != null) {
                _deathOverlayContext.upgradeButtons[i].button.gameObject.SetActive(false);
            }
        }
    }

    private void OnUpgradeButtonClicked(int buttonIndex) {
        if (!_upgradesOffered || _currentUpgradeOffers == null || buttonIndex >= _currentUpgradeOffers.Length) {
            return;
        }

        // Apply upgrade directly to the simulation
        Frame frame = QuantumRunner.Default.Game.Frames.Verified;
        if (frame.Unsafe.TryGetPointer<CharacterStats>(EntityRef, out CharacterStats* stats)) {
            UpgradeSpec upgrade = frame.FindAsset(_currentUpgradeOffers[buttonIndex]);
            if (upgrade != null) {
                ApplyUpgradeToStats(stats, upgrade, frame);
            }
        }

        // Hide upgrade panel
        if (_deathOverlayContext.upgradePanel != null) {
            _deathOverlayContext.upgradePanel.SetActive(false);
        }

        _upgradesOffered = false;
        _currentUpgradeOffers = null;
    }

    private unsafe void ApplyUpgradeToStats(CharacterStats* stats, UpgradeSpec upgrade, Frame frame) {
        switch (upgrade.Type) {
            case UpgradeType.MaxAmmoMultiplier:
                stats->MaxAmmoMultiplier *= upgrade.MultiplierValue;
                break;
            case UpgradeType.ReloadTimeMultiplier:
                stats->ReloadTimeMultiplier *= upgrade.MultiplierValue;
                break;
            case UpgradeType.AttackCooldownMultiplier:
                stats->AttackCooldownMultiplier *= upgrade.MultiplierValue;
                break;
            case UpgradeType.MoveSpeedMultiplier:
                stats->MoveSpeedMultiplier *= upgrade.MultiplierValue;
                break;
            case UpgradeType.DamageMultiplier:
                stats->DamageMultiplier *= upgrade.MultiplierValue;
                break;
            case UpgradeType.MaxHealthMultiplier:
                CharacterSpec spec = frame.FindAsset(stats->Spec);
                FP oldMaxHealth = spec.MaxHealth * stats->MaxHealthMultiplier;
                stats->MaxHealthMultiplier *= upgrade.MultiplierValue;
                FP newMaxHealth = spec.MaxHealth * stats->MaxHealthMultiplier;
                // Scale current health proportionally
                stats->CurrentHealth = (stats->CurrentHealth / oldMaxHealth) * newMaxHealth;
                break;
            case UpgradeType.BulletSpeedMultiplier:
                stats->BulletSpeedMultiplier *= upgrade.MultiplierValue;
                break;
            case UpgradeType.BounceDamageIncreaseMultiplier:
                stats->BounceDamageIncreaseMultiplier *= upgrade.MultiplierValue;
                break;
            case UpgradeType.AdditionalBulletBounces:
                stats->AdditionalBulletBounces += upgrade.IntegerValue;
                break;
        }
    }

    private void OnDeath() {
        _isDead = true;
        _upgradesOffered = false;

        if (_deathOverlayContext.deathOverlay != null) {
            _deathOverlayContext.deathOverlay.SetActive(true);
        }

        // Hide upgrade panel initially
        if (_deathOverlayContext.upgradePanel != null) {
            _deathOverlayContext.upgradePanel.SetActive(false);
        }

        UpdateDeathMessage();
    }

    private void OnRespawn() {
        _isDead = false;
        _upgradesOffered = false;
        _currentUpgradeOffers = null;

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