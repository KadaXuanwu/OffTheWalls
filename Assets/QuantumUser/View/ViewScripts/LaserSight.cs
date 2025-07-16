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

    // Dynamic color settings from ProjectileSpec
    private Color _baseColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private float _redIncreasePerBounce = 0.1f;

    // Cached arrays - reused every frame to avoid GC
    private FPVector2[] _trajectoryPoints;
    private Vector3[] _cachedPositions;
    private int[] _bounceSegments; // Track which segment corresponds to which bounce

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
        _bounceSegments = new int[maxTrajectoryPoints];
    }

    private void SetupLaserRenderer() {
        if (laserLineRenderer.material == null) {
            laserLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
        laserLineRenderer.useWorldSpace = true;
        laserLineRenderer.positionCount = 0;
        laserLineRenderer.enabled = false;
        laserLineRenderer.sortingOrder = 100;

        // Setup gradient for color changes
        laserLineRenderer.colorGradient = CreateColorGradient();
    }

    private Gradient CreateColorGradient() {
        Gradient gradient = new Gradient();

        // Create color keys for different bounce levels
        GradientColorKey[] colorKeys = new GradientColorKey[4];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];

        // Base color (no bounces)
        colorKeys[0] = new GradientColorKey(_baseColor, 0f);

        // Colors for each bounce level
        for (int i = 1; i < 4; i++) {
            float redIncrease = i * _redIncreasePerBounce;
            float newRed = Mathf.Clamp01(_baseColor.r + redIncrease);
            float newGreen = Mathf.Clamp01(_baseColor.g - redIncrease * 0.5f);
            float newBlue = Mathf.Clamp01(_baseColor.b - redIncrease * 0.5f);

            Color bounceColor = new Color(newRed, newGreen, newBlue, _baseColor.a);
            colorKeys[i] = new GradientColorKey(bounceColor, i / 3f);
        }

        // Alpha keys
        alphaKeys[0] = new GradientAlphaKey(_baseColor.a, 0f);
        alphaKeys[1] = new GradientAlphaKey(_baseColor.a, 1f);

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
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

        // Hide laser sight if player is dead
        if (PredictedFrame.Has<RespawnTimer>(EntityRef)) {
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

        // Update color settings from current weapon's projectile spec
        UpdateColorSettingsFromWeapon(activeWeapon);

        int pointCount = CalculateTrajectoryWithBounces(activeWeapon, weaponInventory.IsMainHandActive);

        if (pointCount > 0 && pointCount <= maxTrajectoryPoints) {
            UpdateLineRendererWithColors(pointCount);
        }
        else {
            DisableLaser();
        }
    }

    private void UpdateColorSettingsFromWeapon(WeaponInstance weaponInstance) {
        WeaponSpec weaponSpec = QuantumRunner.Default.Game.Frames.Verified.FindAsset(weaponInstance.WeaponSpec);
        if (weaponSpec == null) return;

        ProjectileSpec projectileSpec = QuantumRunner.Default.Game.Frames.Verified.FindAsset(weaponSpec.ProjectileSpec);
        if (projectileSpec != null) {
            _baseColor = projectileSpec.BaseColor;
            _redIncreasePerBounce = projectileSpec.RedIncreasePerBounce;
        }
    }

    private void UpdateLineRendererWithColors(int pointCount) {
        laserLineRenderer.positionCount = pointCount;

        // Reuse cached array instead of allocating
        for (int i = 0; i < pointCount; i++) {
            _cachedPositions[i].x = _trajectoryPoints[i].X.AsFloat;
            _cachedPositions[i].y = _trajectoryPoints[i].Y.AsFloat;
            _cachedPositions[i].z = 0f;
        }

        laserLineRenderer.SetPositions(_cachedPositions);

        // Create discrete color segments based on bounce count
        CreateDiscreteColorSegments(pointCount);

        if (!laserLineRenderer.enabled) {
            laserLineRenderer.enabled = true;
        }
    }

    private void CreateDiscreteColorSegments(int pointCount) {
        // Find bounce transition points
        List<int> bounceTransitions = new List<int>();
        bounceTransitions.Add(0); // Always start at 0

        for (int i = 1; i < pointCount; i++) {
            if (_bounceSegments[i] != _bounceSegments[i - 1]) {
                bounceTransitions.Add(i);
            }
        }

        // Create gradient with discrete color segments (max 8 keys)
        Gradient gradient = new Gradient();
        List<GradientColorKey> colorKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> alphaKeys = new List<GradientAlphaKey>();

        // Add color keys only at transition points (not duplicate end points)
        for (int i = 0; i < bounceTransitions.Count && colorKeys.Count < 8; i++) {
            int pointIndex = bounceTransitions[i];
            float normalizedPosition = (float)pointIndex / (pointCount - 1);
            int bounceCount = _bounceSegments[pointIndex];

            Color segmentColor = CalculateBounceColor(bounceCount);

            colorKeys.Add(new GradientColorKey(segmentColor, normalizedPosition));
            alphaKeys.Add(new GradientAlphaKey(segmentColor.a, normalizedPosition));
        }

        // Add final point with last segment color if we have room and it's not already there
        if (colorKeys.Count < 8 && bounceTransitions.Count > 0) {
            int lastTransitionIndex = bounceTransitions[bounceTransitions.Count - 1];
            if (lastTransitionIndex < pointCount - 1) {
                int lastBounceCount = _bounceSegments[pointCount - 1];
                Color lastColor = CalculateBounceColor(lastBounceCount);

                colorKeys.Add(new GradientColorKey(lastColor, 1f));
                alphaKeys.Add(new GradientAlphaKey(lastColor.a, 1f));
            }
        }

        // Ensure we have at least 2 keys for a valid gradient
        if (colorKeys.Count == 0) {
            colorKeys.Add(new GradientColorKey(_baseColor, 0f));
            colorKeys.Add(new GradientColorKey(_baseColor, 1f));
            alphaKeys.Add(new GradientAlphaKey(_baseColor.a, 0f));
            alphaKeys.Add(new GradientAlphaKey(_baseColor.a, 1f));
        }
        else if (colorKeys.Count == 1) {
            colorKeys.Add(new GradientColorKey(colorKeys[0].color, 1f));
            alphaKeys.Add(new GradientAlphaKey(alphaKeys[0].alpha, 1f));
        }

        gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
        laserLineRenderer.colorGradient = gradient;
    }

    private Color CalculateBounceColor(int bounceCount) {
        if (bounceCount == 0) {
            return _baseColor;
        }

        float redIncrease = bounceCount * _redIncreasePerBounce;
        float newRed = Mathf.Clamp01(_baseColor.r + redIncrease);
        float newGreen = Mathf.Clamp01(_baseColor.g - redIncrease * 0.5f);
        float newBlue = Mathf.Clamp01(_baseColor.b - redIncrease * 0.5f);

        return new Color(newRed, newGreen, newBlue, _baseColor.a);
    }

    private int CalculateTrajectoryWithBounces(WeaponInstance weaponInstance, bool isMainHand) {
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

        // Use the unified trajectory helper with bounce tracking
        return TraceTrajectoryWithBounceTracking(
            PredictedFrame,
            startPos,
            forwardDirection,
            _trajectoryPoints,
            _bounceSegments,
            _maxDistance,
            _adaptiveStepSize,
            _maxBounces,
            maxTrajectoryPoints
        );
    }

    private int TraceTrajectoryWithBounceTracking(Frame frame, FPVector2 startPos, FPVector2 direction,
        FPVector2[] trajectoryPoints, int[] bounceSegments, FP maxDistance, FP stepSize, int maxBounces, int maxPoints) {

        int pointIndex = 0;
        int currentBounceCount = 0;

        TrajectoryStep step = new TrajectoryStep {
            Position = startPos,
            Direction = direction.Normalized,
            RemainingDistance = maxDistance,
            BounceCount = 0,
            ShouldContinue = true
        };

        // Add starting point
        trajectoryPoints[pointIndex] = step.Position;
        bounceSegments[pointIndex] = currentBounceCount;
        pointIndex++;

        while (pointIndex < maxPoints - 1 && step.ShouldContinue && step.RemainingDistance > FP._0) {
            FP rayDistance = FPMath.Min(stepSize, step.RemainingDistance);

            TrajectoryHitResult hit = TrajectoryHelper.PerformRaycastStep(frame, step.Position, step.Direction, rayDistance);

            if (hit.HasHit && hit.IsWall) {
                // Add hit point with current bounce count (before the bounce)
                trajectoryPoints[pointIndex] = hit.HitPoint;
                bounceSegments[pointIndex] = currentBounceCount;
                pointIndex++;

                // Apply bounce and increment bounce count
                step = TrajectoryHelper.ApplyWallBounce(step, hit, maxBounces);
                currentBounceCount++; // Increment here instead of using step.BounceCount

                if (!step.ShouldContinue) {
                    break;
                }
            }
            else {
                // No collision, continue straight
                step.Position += step.Direction * rayDistance;
                step.RemainingDistance -= rayDistance;

                // Add point with current bounce count
                trajectoryPoints[pointIndex] = step.Position;
                bounceSegments[pointIndex] = currentBounceCount;
                pointIndex++;
            }
        }

        return pointIndex;
    }

    private void OnDestroy() {
        // Clear references
        _trajectoryPoints = null;
        _cachedPositions = null;
        _bounceSegments = null;
    }
}