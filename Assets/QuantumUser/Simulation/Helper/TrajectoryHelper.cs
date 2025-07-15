using Photon.Deterministic;
using Quantum;

namespace Quantum {
    public unsafe struct TrajectoryHitResult {
        public bool HasHit;
        public FPVector2 HitPoint;
        public FPVector2 HitNormal;
        public EntityRef HitEntity;
        public bool IsWall;
        public bool IsCharacter;
    }

    public unsafe struct TrajectoryStep {
        public FPVector2 Position;
        public FPVector2 Direction;
        public FP RemainingDistance;
        public int BounceCount;
        public bool ShouldContinue;
    }

    public static unsafe class TrajectoryHelper {
        private const int WallLayerMask = 1 << 6;
        private static FP WallOffsetDistance = FP._0_01;

        /// <summary>
        /// Performs a single raycast step with unified collision detection
        /// </summary>
        public static TrajectoryHitResult PerformRaycastStep(Frame frame, FPVector2 startPos, FPVector2 direction, FP distance, EntityRef owner = default) {
            TrajectoryHitResult result = new TrajectoryHitResult();

            var quantumHit = frame.Physics2D.Raycast(
                startPos,
                direction,
                distance,
                WallLayerMask,
                QueryOptions.ComputeDetailedInfo | QueryOptions.HitAll
            );

            if (!quantumHit.HasValue) {
                return result;
            }

            Quantum.Physics2D.Hit hit = quantumHit.Value;
            result.HasHit = true;
            result.HitPoint = hit.Point;
            result.HitNormal = hit.Normal;
            result.HitEntity = hit.Entity;

            // Determine hit type
            result.IsWall = frame.Has<Wall>(hit.Entity);
            result.IsCharacter = frame.Unsafe.TryGetPointer<CharacterStats>(hit.Entity, out CharacterStats* _);

            // For character hits, check if it's the owner
            if (result.IsCharacter && owner != default && hit.Entity == owner) {
                result.HasHit = false; // Ignore owner collision
            }

            return result;
        }

        /// <summary>
        /// Calculates bounce direction using deterministic reflection
        /// </summary>
        public static FPVector2 CalculateBounceDirection(FPVector2 incomingDirection, FPVector2 normal) {
            return incomingDirection - 2 * FPVector2.Dot(incomingDirection, normal) * normal;
        }

        /// <summary>
        /// Applies wall bounce to trajectory step
        /// </summary>
        public static TrajectoryStep ApplyWallBounce(TrajectoryStep step, TrajectoryHitResult hit, int maxBounces) {
            TrajectoryStep newStep = step;

            if (step.BounceCount >= maxBounces) {
                newStep.ShouldContinue = false;
                return newStep;
            }

            newStep.Direction = CalculateBounceDirection(step.Direction, hit.HitNormal);
            newStep.Position = hit.HitPoint + hit.HitNormal * WallOffsetDistance;
            newStep.BounceCount++;

            return newStep;
        }

        /// <summary>
        /// Traces complete trajectory for preview (LaserSight)
        /// </summary>
        public static int TraceCompleteTrajectory(Frame frame, FPVector2 startPos, FPVector2 direction,
            FPVector2[] trajectoryPoints, FP maxDistance, FP stepSize, int maxBounces, int maxPoints) {

            int pointIndex = 0;
            TrajectoryStep step = new TrajectoryStep {
                Position = startPos,
                Direction = direction.Normalized,
                RemainingDistance = maxDistance,
                BounceCount = 0,
                ShouldContinue = true
            };

            // Add starting point
            trajectoryPoints[pointIndex++] = step.Position;

            while (pointIndex < maxPoints - 1 && step.ShouldContinue && step.RemainingDistance > FP._0) {
                FP rayDistance = FPMath.Min(stepSize, step.RemainingDistance);

                TrajectoryHitResult hit = PerformRaycastStep(frame, step.Position, step.Direction, rayDistance);

                if (hit.HasHit && hit.IsWall) {
                    // Add hit point
                    trajectoryPoints[pointIndex++] = hit.HitPoint;

                    // Apply bounce
                    step = ApplyWallBounce(step, hit, maxBounces);
                }
                else {
                    // No collision, continue straight
                    step.Position += step.Direction * rayDistance;
                    step.RemainingDistance -= rayDistance;

                    // Add point every few steps to reduce vertex count
                    if (pointIndex % 2 == 0 || step.RemainingDistance <= rayDistance) {
                        trajectoryPoints[pointIndex++] = step.Position;
                    }
                }
            }

            return pointIndex;
        }

        /// <summary>
        /// Performs single frame projectile step (ProjectileSystem)
        /// </summary>
        public static bool PerformProjectileStep(Frame frame, EntityRef projectileEntity, EntityRef owner,
            FP speed, FP deltaTime, int maxBounces, out bool hitCharacter, out EntityRef hitEntity) {

            hitCharacter = false;
            hitEntity = EntityRef.None;

            Transform2D* transform = frame.Unsafe.GetPointer<Transform2D>(projectileEntity);
            Projectile* projectile = frame.Unsafe.GetPointer<Projectile>(projectileEntity);

            FPVector2 direction = new FPVector2(
                FPMath.Cos(transform->Rotation),
                FPMath.Sin(transform->Rotation)
            );

            FP frameDistance = speed * deltaTime;

            TrajectoryHitResult hit = PerformRaycastStep(frame, transform->Position, direction, frameDistance, owner);

            if (!hit.HasHit) {
                // No collision, move normally
                transform->Position += direction * frameDistance;
                return true;
            }

            if (hit.IsWall) {
                // Handle wall bounce
                if (projectile->BounceCount >= maxBounces) {
                    return false; // Destroy projectile
                }

                TrajectoryStep step = new TrajectoryStep {
                    Position = transform->Position,
                    Direction = direction,
                    BounceCount = projectile->BounceCount
                };

                TrajectoryStep newStep = ApplyWallBounce(step, hit, maxBounces);

                transform->Position = newStep.Position;
                transform->Rotation = FPMath.Atan2(newStep.Direction.Y, newStep.Direction.X);
                projectile->BounceCount = newStep.BounceCount;

                // Scale damage linearly: +100% base damage per bounce
                // We need to get the base damage from the projectile spec
                if (!projectile->ProjectileType.Id.Equals(default)) {
                    ProjectileSpec projectileSpec = frame.FindAsset(projectile->ProjectileType);
                    if (projectileSpec != null) {
                        projectile->Damage = projectileSpec.ProjectileDamage * (FP._1 + projectile->BounceCount);
                    }
                }

                return true;
            }

            if (hit.IsCharacter) {
                // Hit character
                hitCharacter = true;
                hitEntity = hit.HitEntity;
                return false; // Projectile should be destroyed
            }

            return true;
        }
    }
}