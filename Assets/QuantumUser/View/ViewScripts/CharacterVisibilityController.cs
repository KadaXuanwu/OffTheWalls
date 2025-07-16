using Quantum;
using UnityEngine;

public class CharacterVisibilityController : QuantumEntityViewComponent<CustomViewContext> {
    [SerializeField] private SpriteRenderer[] spritesToHide;
    [SerializeField] private GameObject[] gameObjectsToHide;
    [SerializeField] private float fadeSpeed = 5f;

    private bool _isDead = false;
    private float _currentAlpha = 1f;

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        // Auto-find sprite renderers if not assigned
        if (spritesToHide == null || spritesToHide.Length == 0) {
            spritesToHide = GetComponentsInChildren<SpriteRenderer>();
        }
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        // Check if player is dead (has respawn timer)
        bool hasRespawnTimer = PredictedFrame.Has<RespawnTimer>(EntityRef);

        if (hasRespawnTimer != _isDead) {
            _isDead = hasRespawnTimer;
            OnDeathStateChanged();
        }

        // Update fade animation
        UpdateFade();
    }

    private void OnDeathStateChanged() {
        // Immediately hide/show game objects
        foreach (GameObject obj in gameObjectsToHide) {
            if (obj != null) {
                obj.SetActive(!_isDead);
            }
        }
    }

    private void UpdateFade() {
        float targetAlpha = _isDead ? 0f : 1f;

        if (Mathf.Abs(_currentAlpha - targetAlpha) > 0.01f) {
            _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

            // Update sprite alphas
            foreach (SpriteRenderer sprite in spritesToHide) {
                if (sprite != null) {
                    Color color = sprite.color;
                    color.a = _currentAlpha;
                    sprite.color = color;
                }
            }
        }
    }
}