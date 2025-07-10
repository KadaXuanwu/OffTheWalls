using Quantum;
using UnityEngine;

public class WeaponViewController : QuantumEntityViewComponent<CustomViewContext> {
    public Transform mainHandTransform;
    public Transform offHandTransform;

    public SpriteRenderer mainHandWeaponRenderer;
    public SpriteRenderer offHandWeaponRenderer;

    private WeaponInventory _lastWeaponInventory;
    private bool _isLocal;

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        // Check if this is the local player
        if (frame.TryGet<PlayerLink>(EntityRef, out PlayerLink playerLink)) {
            _isLocal = Game.PlayerIsLocal(playerLink.Player);
        }
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        if (!PredictedFrame.TryGet<WeaponInventory>(EntityRef, out WeaponInventory weaponInventory)) {
            return;
        }

        // Check if weapon inventory has changed
        if (HasWeaponInventoryChanged(weaponInventory)) {
            UpdateWeaponVisuals(weaponInventory);
            _lastWeaponInventory = weaponInventory;
        }

        // Update active weapon highlighting
        UpdateActiveWeaponHighlight(weaponInventory);
    }

    private bool HasWeaponInventoryChanged(WeaponInventory current) {
        return !current.MainHandWeapon.WeaponSpec.Id.Equals(_lastWeaponInventory.MainHandWeapon.WeaponSpec.Id) ||
               !current.OffHandWeapon.WeaponSpec.Id.Equals(_lastWeaponInventory.OffHandWeapon.WeaponSpec.Id) ||
               current.IsMainHandActive != _lastWeaponInventory.IsMainHandActive;
    }

    private void UpdateWeaponVisuals(WeaponInventory weaponInventory) {
        // Update main hand weapon
        UpdateHandWeapon(mainHandWeaponRenderer, mainHandTransform, weaponInventory.MainHandWeapon);

        // Update off hand weapon
        UpdateHandWeapon(offHandWeaponRenderer, offHandTransform, weaponInventory.OffHandWeapon);
    }

    private void UpdateHandWeapon(SpriteRenderer weaponRenderer, Transform handTransform, WeaponInstance weaponInstance) {
        if (weaponRenderer == null || handTransform == null) return;

        if (weaponInstance.WeaponSpec.Id.Equals(default)) {
            // No weapon equipped
            weaponRenderer.sprite = null;
            weaponRenderer.gameObject.SetActive(false);
            return;
        }

        WeaponSpec weaponSpec = QuantumRunner.Default.Game.Frames.Verified.FindAsset(weaponInstance.WeaponSpec);
        if (weaponSpec == null) return;

        weaponRenderer.gameObject.SetActive(true);
        weaponRenderer.sprite = weaponSpec.WeaponSprite;

        // Apply weapon visual settings as offsets to hand transform
        Vector3 offsetPosition = handTransform.localPosition + new Vector3(weaponSpec.HandOffset.x, weaponSpec.HandOffset.y, 0f);
        weaponRenderer.transform.localPosition = offsetPosition;

        Quaternion offsetRotation = handTransform.localRotation * Quaternion.Euler(0f, 0f, weaponSpec.RotationOffset);
        weaponRenderer.transform.localRotation = offsetRotation;

        // fix
        Vector3 offsetScale = new Vector3(weaponSpec.Scale.x, weaponSpec.Scale.y, 1f);
        weaponRenderer.transform.localScale = offsetScale;
    }

    private void UpdateActiveWeaponHighlight(WeaponInventory weaponInventory) {
        if (offHandWeaponRenderer == null || mainHandWeaponRenderer == null) return;

        Color activeColor = Color.white;
        Color inactiveColor = new Color(1f, 1f, 1f, 0.25f);

        if (weaponInventory.IsMainHandActive) {
            mainHandWeaponRenderer.color = activeColor;
            offHandWeaponRenderer.color = inactiveColor;
        }
        else {
            offHandWeaponRenderer.color = activeColor;
            mainHandWeaponRenderer.color = inactiveColor;
        }
    }
}