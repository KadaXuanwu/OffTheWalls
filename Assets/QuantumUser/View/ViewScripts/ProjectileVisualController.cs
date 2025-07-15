using Photon.Deterministic;
using Quantum;
using UnityEngine;

/// <summary>
/// Handles visual updates for projectiles, including color changes based on bounce count
/// </summary>
public class ProjectileVisualController : QuantumEntityViewComponent<CustomViewContext> {
    [SerializeField] private SpriteRenderer spriteRenderer;

    private int _lastBounceCount = 0;
    private Color _currentColor;
    private Color _baseColor;
    private float _redIncreasePerBounce;
    private bool _colorsInitialized = false;

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        // Initialize colors from ProjectileSpec
        InitializeColorsFromSpec(frame);
    }

    private void InitializeColorsFromSpec(Frame frame) {
        if (!frame.TryGet<Projectile>(EntityRef, out Projectile projectile)) {
            // Fallback colors if we can't get the projectile spec
            _baseColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            _redIncreasePerBounce = 0.1f;
            return;
        }

        if (!projectile.ProjectileType.Id.Equals(default)) {
            ProjectileSpec projectileSpec = frame.FindAsset(projectile.ProjectileType);
            if (projectileSpec != null) {
                _baseColor = projectileSpec.BaseColor;
                _redIncreasePerBounce = projectileSpec.RedIncreasePerBounce;
                _colorsInitialized = true;
            }
        }

        if (!_colorsInitialized) {
            // Fallback colors
            _baseColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            _redIncreasePerBounce = 0.1f;
        }

        _currentColor = _baseColor;
        spriteRenderer.color = _currentColor;
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        if (spriteRenderer == null) return;

        if (!PredictedFrame.TryGet<Projectile>(EntityRef, out Projectile projectile)) {
            return;
        }

        // Initialize colors if not done yet
        if (!_colorsInitialized) {
            InitializeColorsFromSpec(PredictedFrame);
        }

        // Check if bounce count has changed
        if (projectile.BounceCount != _lastBounceCount) {
            UpdateProjectileColor(projectile.BounceCount);
            _lastBounceCount = projectile.BounceCount;
        }
    }

    private void UpdateProjectileColor(int bounceCount) {
        if (bounceCount == 0) {
            _currentColor = _baseColor;
        }
        else {
            // Calculate new color with increased red component
            float redIncrease = bounceCount * _redIncreasePerBounce;
            float newRed = Mathf.Clamp01(_baseColor.r + redIncrease);

            // Keep green and blue components the same or slightly reduced for better contrast
            float newGreen = Mathf.Clamp01(_baseColor.g - redIncrease * 0.5f);
            float newBlue = Mathf.Clamp01(_baseColor.b - redIncrease * 0.5f);

            _currentColor = new Color(newRed, newGreen, newBlue, _baseColor.a);
        }

        spriteRenderer.color = _currentColor;
    }
}