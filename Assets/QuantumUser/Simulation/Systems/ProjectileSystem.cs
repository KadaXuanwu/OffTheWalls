namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    /// <summary>
    /// The <c>ProjectileSystem</c> class manages the lifecycle of projectiles,
    /// including updating their time-to-live (TTL), movement, and handling bouncing using raycast prediction.
    /// </summary>
    [Preserve]
    public unsafe class ProjectileSystem : SystemMainThreadFilter<ProjectileSystem.Filter>, ISignalCharacterShoot, ISignalOnCollision2D {
        /// <summary>
        /// The <c>Filter</c> struct represents the components required for the system's operations,
        /// including an entity reference and a pointer to its projectile component.
        /// </summary>
        public struct Filter {
            /// <summary>
            /// The reference to the entity being processed.
            /// </summary>
            public EntityRef Entity;

            /// <summary>
            /// Pointer to the entity's projectile component.
            /// </summary>
            public Projectile* Projectile;

            /// <summary>
            /// Pointer to the entity's transform component.
            /// </summary>
            public Transform2D* Transform;
        }

        /// <summary>
        /// Updates TTL of projectiles, handles raycast-based movement and bouncing, and destroys them if TTL reaches zero.
        /// </summary>
        /// <param name="f">The game frame.</param>
        /// <param name="filter">The filter containing the entity and its projectile component.</param>
        public override void Update(Frame f, ref Filter filter) {
            // Update TTL
            filter.Projectile->TTL -= f.DeltaTime;
            if (filter.Projectile->TTL <= 0) {
                f.Destroy(filter.Entity);
                return;
            }

            // Get max bounces from ProjectileSpec
            int maxBounces = 3; // Default fallback
            if (!filter.Projectile->ProjectileType.Id.Equals(default)) {
                ProjectileSpec projectileSpec = f.FindAsset(filter.Projectile->ProjectileType);
                if (projectileSpec != null) {
                    maxBounces = projectileSpec.MaxBounces;
                }
            }

            // Use unified trajectory helper for projectile movement
            bool shouldContinue = TrajectoryHelper.PerformProjectileStep(
                f,
                filter.Entity,
                filter.Projectile->Owner,
                filter.Projectile->Speed,
                f.DeltaTime,
                maxBounces,
                out bool hitCharacter,
                out EntityRef hitEntity
            );

            if (!shouldContinue) {
                if (hitCharacter && hitEntity != EntityRef.None) {
                    // Handle character hit with scaled damage
                    HandleCharacterHit(f, filter.Entity, hitEntity, filter.Projectile);
                }
                // Destroy projectile (either hit max bounces or hit character)
                f.Destroy(filter.Entity);
            }
        }

        /// <summary>
        /// Handles collision with characters using the same collision system for non-wall entities.
        /// </summary>
        /// <param name="f">The game frame.</param>
        /// <param name="info">Collision information.</param>
        public void OnCollision2D(Frame f, CollisionInfo2D info) {
            // Only handle character collisions here, walls are handled by raycast
            EntityRef projectileEntity = EntityRef.None;
            EntityRef otherEntity = EntityRef.None;

            if (f.Has<Projectile>(info.Entity)) {
                projectileEntity = info.Entity;
                otherEntity = info.Other;
            }
            else if (f.Has<Projectile>(info.Other)) {
                projectileEntity = info.Other;
                otherEntity = info.Entity;
            }

            if (projectileEntity == EntityRef.None || f.Has<Wall>(otherEntity)) {
                return; // Skip walls (handled by raycast) and non-projectile collisions
            }

            Projectile* projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);

            // Handle collision with characters only
            HandleCharacterHit(f, projectileEntity, otherEntity, projectile);
        }

        /// <summary>
        /// Handles bouncing off walls using deterministic raycast collision data.
        /// </summary>
        private void HandleWallBounce(Frame f, Filter filter, Quantum.Physics2D.Hit hit, FPVector2 projectileDirection) {
            // Check bounce count limit (fallback to 3 if no spec available)
            int maxBounces = 3;

            // Try to get bounce limit from ProjectileSpec if available
            if (!filter.Projectile->ProjectileType.Id.Equals(default)) {
                ProjectileSpec projectileSpec = f.FindAsset(filter.Projectile->ProjectileType);
                if (projectileSpec != null) {
                    maxBounces = projectileSpec.MaxBounces;
                }
            }

            if (filter.Projectile->BounceCount >= maxBounces) {
                f.Destroy(filter.Entity);
                return;
            }

            // Apply deterministic reflection using hit normal
            FPVector2 reflectedDirection = projectileDirection - 2 * FPVector2.Dot(projectileDirection, hit.Normal) * hit.Normal;

            // Update projectile position to hit point with small offset
            FP wallOffsetDistance = FP._0_01;
            filter.Transform->Position = hit.Point + hit.Normal * wallOffsetDistance;

            // Update rotation to match reflected direction
            filter.Transform->Rotation = FPMath.Atan2(reflectedDirection.Y, reflectedDirection.X);

            filter.Projectile->BounceCount++;

            // Scale damage linearly: +100% base damage per bounce
            // We need to store the base damage to calculate correctly
            if (!filter.Projectile->ProjectileType.Id.Equals(default)) {
                ProjectileSpec projectileSpec = f.FindAsset(filter.Projectile->ProjectileType);
                if (projectileSpec != null) {
                    filter.Projectile->Damage = projectileSpec.ProjectileDamage * (FP._1 + filter.Projectile->BounceCount);
                }
            }
        }

        /// <summary>
        /// Handles collision with characters.
        /// </summary>
        private void HandleCharacterHit(Frame f, EntityRef projectileEntity, EntityRef characterEntity, Projectile* projectile) {
            // Don't collide with the owner
            if (characterEntity == projectile->Owner) {
                return;
            }

            // Apply scaled damage based on bounce count
            if (f.Unsafe.TryGetPointer<CharacterStats>(characterEntity, out CharacterStats* stats)) {
                stats->CurrentHealth -= projectile->Damage;

                if (stats->CurrentHealth <= 0) {
                    // Handle death
                    f.Destroy(characterEntity);
                }
            }

            // Destroy projectile after hitting a character
            f.Destroy(projectileEntity);
        }

        /// <summary>
        /// Handles the shooting of a projectile by a character using their currently equipped weapon.
        /// This method creates a new projectile based on the weapon's projectile spec.
        /// </summary>
        /// <param name="f">The game frame.</param>
        /// <param name="owner">The reference to the entity (character) that is shooting the projectile.</param>
        public void CharacterShoot(Frame f, EntityRef owner) {
            if (!f.Unsafe.TryGetPointer<WeaponInventory>(owner, out WeaponInventory* weaponInventory)) {
                return;
            }

            WeaponInstance activeWeapon = weaponInventory->IsMainHandActive ?
                weaponInventory->MainHandWeapon :
                weaponInventory->OffHandWeapon;

            if (activeWeapon.WeaponSpec.Id.Equals(default)) {
                return; // No weapon equipped
            }

            WeaponSpec weaponSpec = f.FindAsset(activeWeapon.WeaponSpec);
            ProjectileSpec projectileSpec = f.FindAsset(weaponSpec.ProjectileSpec);

            EntityRef projectileEntity = f.Create(projectileSpec.ProjectilePrototype);
            Transform2D* projectileTransform = f.Unsafe.GetPointer<Transform2D>(projectileEntity);
            Transform2D* ownerTransform = f.Unsafe.GetPointer<Transform2D>(owner);

            FP adjustedRotation = ownerTransform->Rotation + FP.PiOver2;
            FPVector2 forwardDirection = new FPVector2(
                FPMath.Cos(adjustedRotation),
                FPMath.Sin(adjustedRotation)
            );

            projectileTransform->Rotation = adjustedRotation;

            // Use different offset based on which hand is active (same as LaserSight)
            FPVector2 baseOffset = weaponInventory->IsMainHandActive ?
                new FPVector2(FP._0_50, -FP._0_50) :
                new FPVector2(-FP._0_50, -FP._0_50);

            FP characterRotation = ownerTransform->Rotation;
            FPVector2 rotatedOffset = new FPVector2(
                baseOffset.X * FPMath.Cos(characterRotation) - baseOffset.Y * FPMath.Sin(characterRotation),
                baseOffset.X * FPMath.Sin(characterRotation) + baseOffset.Y * FPMath.Cos(characterRotation)
            );

            projectileTransform->Position = ownerTransform->Position + forwardDirection * projectileSpec.ShotOffset + rotatedOffset;

            Projectile* projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            projectile->TTL = projectileSpec.ProjectileTTL;
            projectile->Owner = owner;
            projectile->Damage = projectileSpec.ProjectileDamage; // Base damage
            projectile->ProjectileType = weaponSpec.ProjectileSpec;
            projectile->WeaponType = activeWeapon.WeaponSpec;
            projectile->Speed = projectileSpec.ProjectileSpeed;
            projectile->BounceCount = 0;
        }
    }
}