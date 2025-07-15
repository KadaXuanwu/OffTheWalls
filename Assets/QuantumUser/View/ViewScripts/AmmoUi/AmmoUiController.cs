using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.UI;

public class AmmoUIController : QuantumEntityViewComponent<CustomViewContext> {
    [SerializeField] private GameObject ammoUIPrefab;
    [SerializeField] private Vector3 worldOffset = new Vector3(0, -1.5f, 0);
    [SerializeField] private bool hideWhenFull = false;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private Color ammoTextColor = Color.white;
    [SerializeField] private Color reloadProgressColor = new Color(1f, 0.5f, 0f);

    private AmmoUIContext _ammoUIContext;
    private WeaponInstance _lastWeaponInstance;
    private CharacterStats _lastStats;
    private float _targetAlpha = 1f;
    private bool _isLocal;
    private bool _uiInitialized = false;

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        // Check if local player FIRST before doing anything
        if (frame.TryGet<PlayerLink>(EntityRef, out PlayerLink playerLink)) {
            _isLocal = Game.PlayerIsLocal(playerLink.Player);
        }

        // Only initialize UI for local player
        if (_isLocal) {
            InitializeUI();
        }
    }

    private void InitializeUI() {
        if (_uiInitialized || !_isLocal) return;

        _ammoUIContext = Instantiate(ammoUIPrefab).GetComponent<AmmoUIContext>();
        SetupUI();
        _uiInitialized = true;
    }

    private void SetupUI() {
        if (_ammoUIContext == null) return;

        if (_ammoUIContext.ammoText != null) {
            _ammoUIContext.ammoText.color = ammoTextColor;
        }

        if (_ammoUIContext.reloadProgressFill != null) {
            _ammoUIContext.reloadProgressFill.color = reloadProgressColor;
            _ammoUIContext.reloadProgressFill.fillAmount = 0f;
        }
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        // Early exit if not local player or UI not initialized
        if (!_isLocal || !_uiInitialized || _ammoUIContext == null || _ammoUIContext.ammoUI == null) {
            return;
        }

        if (!PredictedFrame.TryGet<WeaponInventory>(EntityRef, out WeaponInventory weaponInventory)) {
            SetVisibility(false);
            return;
        }

        if (!PredictedFrame.TryGet<CharacterStats>(EntityRef, out CharacterStats stats)) {
            return;
        }

        WeaponInstance activeWeapon = weaponInventory.IsMainHandActive ?
            weaponInventory.MainHandWeapon :
            weaponInventory.OffHandWeapon;

        // Check if weapon changed or ammo changed
        if (HasWeaponChanged(activeWeapon, stats)) {
            UpdateAmmoDisplay(activeWeapon, stats);
            _lastWeaponInstance = activeWeapon;
            _lastStats = stats;
        }

        // Update reload progress
        UpdateReloadProgress(activeWeapon, stats);

        // Update position
        UpdateUIPosition();

        // Handle fade animation
        UpdateFadeAnimation();
    }

    private bool HasWeaponChanged(WeaponInstance current, CharacterStats currentStats) {
        return !current.WeaponSpec.Id.Equals(_lastWeaponInstance.WeaponSpec.Id) ||
               current.CurrentAmmo != _lastWeaponInstance.CurrentAmmo ||
               current.ReloadTimeRemaining != _lastWeaponInstance.ReloadTimeRemaining ||
               currentStats.MaxAmmoMultiplier != _lastStats.MaxAmmoMultiplier;
    }

    private void UpdateAmmoDisplay(WeaponInstance weaponInstance, CharacterStats stats) {
        if (weaponInstance.WeaponSpec.Id.Equals(default)) {
            SetVisibility(false);
            return;
        }

        WeaponSpec weaponSpec = PredictedFrame.FindAsset(weaponInstance.WeaponSpec);
        if (weaponSpec == null) {
            SetVisibility(false);
            return;
        }

        CharacterSpec characterSpec = PredictedFrame.FindAsset(stats.Spec);
        int maxAmmo = FPMath.RoundToInt(weaponSpec.MaxAmmo * characterSpec.MaxAmmoMultiplier);

        // Update ammo text
        if (_ammoUIContext.ammoText != null) {
            _ammoUIContext.ammoText.text = $"{weaponInstance.CurrentAmmo}/{maxAmmo}";

            // Color based on ammo level
            float ammoPercentage = weaponInstance.CurrentAmmo / (float)maxAmmo;
            if (ammoPercentage > 0.5f) {
                _ammoUIContext.ammoText.color = ammoTextColor;
            }
            else if (ammoPercentage > 0.2f) {
                _ammoUIContext.ammoText.color = Color.yellow;
            }
            else {
                _ammoUIContext.ammoText.color = Color.red;
            }
        }

        // Update weapon icon if available
        if (_ammoUIContext.weaponIcon != null && weaponSpec.WeaponSprite != null) {
            _ammoUIContext.weaponIcon.sprite = weaponSpec.WeaponSprite;
            _ammoUIContext.weaponIcon.enabled = true;
        }

        // Show/hide logic
        bool shouldShow = !hideWhenFull || weaponInstance.CurrentAmmo < maxAmmo || weaponInstance.ReloadTimeRemaining > 0;
        SetVisibility(shouldShow);
    }

    private void UpdateReloadProgress(WeaponInstance weaponInstance, CharacterStats stats) {
        if (_ammoUIContext.reloadProgressFill == null) return;

        if (weaponInstance.ReloadTimeRemaining > 0 && !weaponInstance.WeaponSpec.Id.Equals(default)) {
            WeaponSpec weaponSpec = PredictedFrame.FindAsset(weaponInstance.WeaponSpec);
            CharacterSpec characterSpec = PredictedFrame.FindAsset(stats.Spec);

            FP totalReloadTime = weaponSpec.ReloadTime * characterSpec.ReloadTimeMultiplier;
            float reloadProgress = 1f - (weaponInstance.ReloadTimeRemaining / totalReloadTime).AsFloat;

            _ammoUIContext.reloadProgressFill.fillAmount = reloadProgress;

            // Show reload circle
            if (_ammoUIContext.reloadCircle != null) {
                _ammoUIContext.reloadCircle.SetActive(true);
            }
        }
        else {
            _ammoUIContext.reloadProgressFill.fillAmount = 0f;

            // Hide reload circle
            if (_ammoUIContext.reloadCircle != null) {
                _ammoUIContext.reloadCircle.SetActive(false);
            }
        }
    }

    private void UpdateUIPosition() {
        if (_ammoUIContext.ammoUI == null) return;

        Vector3 worldPos = transform.position + worldOffset;
        _ammoUIContext.ammoUI.transform.position = worldPos;

        // Make UI face camera
        if (Camera.main != null) {
            _ammoUIContext.ammoUI.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }
    }

    private void SetVisibility(bool visible) {
        _targetAlpha = visible ? 1f : 0f;
    }

    private void UpdateFadeAnimation() {
        if (_ammoUIContext.canvasGroup == null) return;

        float currentAlpha = _ammoUIContext.canvasGroup.alpha;
        float newAlpha = Mathf.MoveTowards(currentAlpha, _targetAlpha, fadeSpeed * Time.deltaTime);

        _ammoUIContext.canvasGroup.alpha = newAlpha;

        // Completely hide when fully faded
        if (newAlpha <= 0f && _ammoUIContext.ammoUI.activeSelf) {
            _ammoUIContext.ammoUI.SetActive(false);
        }
        else if (newAlpha > 0f && !_ammoUIContext.ammoUI.activeSelf) {
            _ammoUIContext.ammoUI.SetActive(true);
        }
    }

    private void OnDestroy() {
        if (_ammoUIContext != null && _ammoUIContext.ammoUI != null) {
            Destroy(_ammoUIContext.ammoUI);
        }
    }
}