using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : QuantumEntityViewComponent<CustomViewContext> {
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 1, 0);
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private float fadeSpeed = 2f;

    private HealthBarContext _healthBarContext;
    private CharacterStats _lastStats;
    private float _targetAlpha = 0f;

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        _healthBarContext = Instantiate(healthBarPrefab).GetComponent<HealthBarContext>();
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        if (_healthBarContext.healthBar == null) return;

        // Hide health bar if player is dead (has respawn timer)
        if (PredictedFrame.Has<RespawnTimer>(EntityRef)) {
            SetVisibility(false);
            return;
        }

        if (!PredictedFrame.TryGet<CharacterStats>(EntityRef, out CharacterStats currentStats)) {
            return;
        }

        // Check if health changed
        if (HasHealthChanged(currentStats)) {
            CharacterSpec spec = PredictedFrame.FindAsset(currentStats.Spec);
            UpdateHealthBar(currentStats.CurrentHealth, spec.MaxHealth);
            _lastStats = currentStats;
        }

        // Update position
        UpdateHealthBarPosition();

        // Handle fade animation
        //UpdateFadeAnimation();
    }

    private bool HasHealthChanged(CharacterStats current) {
        return current.CurrentHealth != _lastStats.CurrentHealth;
    }

    private void UpdateHealthBar(FP currentHealth, FP maxHealth) {
        if (_healthBarContext.healthBarFill == null) return;

        float healthPercentage = (currentHealth / maxHealth).AsFloat;
        _healthBarContext.healthBarFill.fillAmount = healthPercentage;

        // Color based on health percentage
        if (healthPercentage > 0.6f) {
            _healthBarContext.healthBarFill.color = Color.green;
        }
        else if (healthPercentage > 0.3f) {
            _healthBarContext.healthBarFill.color = Color.yellow;
        }
        else {
            _healthBarContext.healthBarFill.color = Color.red;
        }

        // Show/hide logic
        bool shouldShow = !hideWhenFull || healthPercentage < 1f;
        SetVisibility(shouldShow);
    }

    private void UpdateHealthBarPosition() {
        if (_healthBarContext.healthBar == null) return;

        Vector3 worldPos = transform.position + worldOffset;
        _healthBarContext.healthBar.transform.position = worldPos;

        // Make health bar face camera
        if (Camera.main != null) {
            _healthBarContext.healthBar.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }
    }

    private void SetVisibility(bool visible) {
        // Force hide if dead
        if (visible && PredictedFrame != null && PredictedFrame.Has<RespawnTimer>(EntityRef)) {
            visible = false;
        }

        if (_healthBarContext.healthBar != null) {
            _healthBarContext.healthBar.SetActive(visible);
        }
    }

    private void UpdateFadeAnimation() {
        if (_healthBarContext.canvasGroup == null) return;

        float currentAlpha = _healthBarContext.canvasGroup.alpha;
        float newAlpha = Mathf.MoveTowards(currentAlpha, _targetAlpha, fadeSpeed * Time.deltaTime);

        _healthBarContext.canvasGroup.alpha = newAlpha;

        // Completely hide when fully faded
        if (newAlpha <= 0f && _healthBarContext.healthBar.activeSelf) {
            _healthBarContext.healthBar.SetActive(false);
        }
        else if (newAlpha > 0f && !_healthBarContext.healthBar.activeSelf) {
            _healthBarContext.healthBar.SetActive(true);
        }
    }

    private void OnDestroy() {
        if (_healthBarContext.healthBar != null) {
            Destroy(_healthBarContext.healthBar);
        }
    }
}