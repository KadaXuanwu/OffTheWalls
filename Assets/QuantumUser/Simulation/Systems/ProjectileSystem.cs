namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.Scripting;

    /// <summary>
    /// The <c>ProjectileSystem</c> class manages the lifecycle of projectiles,
    /// including updating their time-to-live (TTL) and movement using unified trajectory logic.
    /// </summary>
    [Preserve]
    public unsafe class ProjectileSystem : SystemMainThreadFilter<ProjectileSystem.Filter>, ISignalCharacterShoot {
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
        /// Updates TTL of projectiles and handles movement using unified trajectory system.
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

            // Use unified trajectory helper for all projectile movement
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
                    // Handle character hit
                    HandleCharacterHit(f, filter.Entity, hitEntity, filter.Projectile);
                }
                // Destroy projectile (either hit max bounces or hit character)
                f.Destroy(filter.Entity);
            }
        }

        /// <summary>
        /// Handles collision with characters by applying damage.
        /// </summary>
        /// <param name="f">The game frame.</param>
        /// <param name="projectileEntity">The projectile entity.</param>
        /// <param name="characterEntity">The character entity that was hit.</param>
        /// <param name="projectile">Pointer to the projectile component.</param>
        private void HandleCharacterHit(Frame f, EntityRef projectileEntity, EntityRef characterEntity, Projectile* projectile) {
            // Don't collide with the owner
            if (characterEntity == projectile->Owner) {
                return;
            }

            // Apply damage if the entity can take damage
            if (f.Unsafe.TryGetPointer<CharacterStats>(characterEntity, out CharacterStats* stats)) {
                stats->CurrentHealth -= projectile->Damage;

                if (stats->CurrentHealth <= 0) {
                    // Handle death
                    f.Destroy(characterEntity);
                }
            }
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
            projectile->Damage = projectileSpec.ProjectileDamage;
            projectile->ProjectileType = weaponSpec.ProjectileSpec;
            projectile->WeaponType = activeWeapon.WeaponSpec;
            projectile->Speed = projectileSpec.ProjectileSpeed;
            projectile->BounceCount = 0;
        }
    }
}