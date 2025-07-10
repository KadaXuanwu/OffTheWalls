using Quantum;
using UnityEngine;
using System.Collections.Generic;
using Photon.Deterministic;

public unsafe class LaserSight : QuantumEntityViewComponent<CustomViewContext> {
    [SerializeField] private LineRenderer laserLineRenderer;
    [SerializeField] private int maxTrajectoryPoints = 200;
    [SerializeField] private int trajectoryUpdateInterval = 3; // Update every N frames
    [SerializeField] private int maxRaycastsPerFrame = 50; // Limit raycasts per frame

    private bool _isLocal;

    // Cached arrays - reused every frame to avoid GC
    private FPVector2[] _trajectoryPoints;
    private Vector3[] _cachedPositions;

    // Performance tracking
    private int _trajectoryUpdateCounter = 0;
    private bool _lastTrajectoryState = false;
    private WeaponInstance _lastWeaponCache;
    private bool _weaponChanged = true;

    // Deterministic physics constants
    private readonly FP _maxDistance = 1000;
    private readonly FP _bounceExtension = 2;
    private readonly FP _adaptiveStepSize = FP._0_50; // Larger step for performance
    private readonly FP _wallOffsetDistance = FP._0_01;
    private readonly int _maxBounces = 3;

    // Cached physics filter
    private ContactFilter2D _wallContactFilter = new();

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        if (frame.TryGet<PlayerLink>(EntityRef, out PlayerLink playerLink)) {
            _isLocal = Game.PlayerIsLocal(playerLink.Player);
        }

        SetupLaserRenderer();
        SetupPhysicsFilter();
        InitializeCachedArrays();

        if (laserLineRenderer == null) {
            laserLineRenderer = gameObject.AddComponent<LineRenderer>();
        }
    }

    private void InitializeCachedArrays() {
        _trajectoryPoints = new FPVector2[maxTrajectoryPoints];
        _cachedPositions = new Vector3[maxTrajectoryPoints];
    }

    private void SetupLaserRenderer() {
        if (laserLineRenderer.material == null) {
            laserLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
        laserLineRenderer.useWorldSpace = true;
        laserLineRenderer.positionCount = 0;
        laserLineRenderer.enabled = false;
        laserLineRenderer.sortingOrder = 100;
    }

    private void SetupPhysicsFilter() {
        _wallContactFilter.useLayerMask = true;
        _wallContactFilter.layerMask = UnityEngine.LayerMask.GetMask("Environment");
        _wallContactFilter.useTriggers = false;
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        if (!_isLocal) {
            DisableLaser();
            return;
        }

        PlayerLink playerLink = PredictedFrame.Get<PlayerLink>(EntityRef);
        Quantum.Input* input = PredictedFrame.GetPlayerInput(playerLink.Player);

        bool shouldShowTrajectory = input->ShowTrajectory;

        // Early exit if state hasn't changed and we're not showing trajectory
        if (!shouldShowTrajectory && !_lastTrajectoryState) {
            return;
        }

        if (shouldShowTrajectory) {
            // Check if weapon changed first
            CheckWeaponChange();

            // Only update trajectory at intervals or when weapon changes
            if (_trajectoryUpdateCounter % trajectoryUpdateInterval == 0 || _weaponChanged) {
                UpdateTrajectoryPreview();
                _weaponChanged = false;
            }
            _trajectoryUpdateCounter++;
        }
        else {
            DisableLaser();
            _trajectoryUpdateCounter = 0;
        }

        _lastTrajectoryState = shouldShowTrajectory;
    }

    private void CheckWeaponChange() {
        if (!PredictedFrame.TryGet<WeaponInventory>(EntityRef, out WeaponInventory weaponInventory)) {
            return;
        }

        WeaponInstance activeWeapon = weaponInventory.IsMainHandActive ?
            weaponInventory.MainHandWeapon :
            weaponInventory.OffHandWeapon;

        // Check if weapon actually changed
        if (!activeWeapon.WeaponSpec.Id.Equals(_lastWeaponCache.WeaponSpec.Id) ||
            activeWeapon.CurrentAmmo != _lastWeaponCache.CurrentAmmo) {
            _weaponChanged = true;
            _lastWeaponCache = activeWeapon;
        }
    }

    private void DisableLaser() {
        if (laserLineRenderer.enabled) {
            laserLineRenderer.enabled = false;
        }
    }

    private void UpdateTrajectoryPreview() {
        if (!PredictedFrame.TryGet<WeaponInventory>(EntityRef, out WeaponInventory weaponInventory)) {
            DisableLaser();
            return;
        }

        WeaponInstance activeWeapon = weaponInventory.IsMainHandActive ?
            weaponInventory.MainHandWeapon :
            weaponInventory.OffHandWeapon;

        if (activeWeapon.WeaponSpec.Id.Equals(default)) {
            DisableLaser();
            return;
        }

        int pointCount = CalculateTrajectory(activeWeapon, weaponInventory.IsMainHandActive);

        if (pointCount > 0 && pointCount <= maxTrajectoryPoints) {
            UpdateLineRenderer(pointCount);
        }
        else {
            DisableLaser();
        }
    }

    private void UpdateLineRenderer(int pointCount) {
        laserLineRenderer.positionCount = pointCount;

        // Reuse cached array instead of allocating
        for (int i = 0; i < pointCount; i++) {
            _cachedPositions[i].x = _trajectoryPoints[i].X.AsFloat;
            _cachedPositions[i].y = _trajectoryPoints[i].Y.AsFloat;
            _cachedPositions[i].z = 0f;
        }

        laserLineRenderer.SetPositions(_cachedPositions);

        if (!laserLineRenderer.enabled) {
            laserLineRenderer.enabled = true;
        }
    }

    private int CalculateTrajectory(WeaponInstance weaponInstance, bool isMainHand) {
        // Early validation
        if (_trajectoryPoints == null || _trajectoryPoints.Length == 0) {
            return 0;
        }

        if (!PredictedFrame.TryGet<Transform2D>(EntityRef, out Transform2D transform2D)) {
            return 0;
        }

        WeaponSpec weaponSpec = QuantumRunner.Default.Game.Frames.Verified.FindAsset(weaponInstance.WeaponSpec);
        if (weaponSpec == null) return 0;

        ProjectileSpec projectileSpec = QuantumRunner.Default.Game.Frames.Verified.FindAsset(weaponSpec.ProjectileSpec);
        if (projectileSpec == null) return 0;

        // Calculate starting position and direction
        FPVector2 characterPos = transform2D.Position;
        FP characterRotation = transform2D.Rotation;

        FP adjustedRotation = characterRotation + FP.PiOver2;
        FPVector2 forwardDirection = new FPVector2(
            FPMath.Cos(adjustedRotation),
            FPMath.Sin(adjustedRotation)
        );

        // Use deterministic offsets matching ProjectileSystem
        FPVector2 baseOffset = isMainHand ?
            new FPVector2(FP._0_50, -FP._0_50) :
            new FPVector2(-FP._0_50, -FP._0_50);

        FPVector2 rotatedOffset = new FPVector2(
            baseOffset.X * FPMath.Cos(characterRotation) - baseOffset.Y * FPMath.Sin(characterRotation),
            baseOffset.X * FPMath.Sin(characterRotation) + baseOffset.Y * FPMath.Cos(characterRotation)
        );

        FPVector2 startPos = characterPos + forwardDirection * projectileSpec.ShotOffset + rotatedOffset;

        // Use the unified trajectory helper
        return TrajectoryHelper.TraceCompleteTrajectory(
            PredictedFrame,
            startPos,
            forwardDirection,
            _trajectoryPoints,
            _maxDistance,
            _adaptiveStepSize,
            _maxBounces,
            maxTrajectoryPoints
        );
    }

    private void OnDestroy() {
        // Clear references
        _trajectoryPoints = null;
        _cachedPositions = null;
    }
}