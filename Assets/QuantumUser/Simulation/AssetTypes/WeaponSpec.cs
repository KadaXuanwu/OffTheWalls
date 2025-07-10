using System;
using System.Collections.Generic;
using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    /// <summary>
    /// WeaponSpec asset defines a weapon and its properties
    /// </summary>
    public partial class WeaponSpec : AssetObject {
        public string WeaponName;
        public AssetRef<ProjectileSpec> ProjectileSpec;
        public int MaxAmmo = 1;
        public FP ReloadTime = 1;
        public FP AttackCooldown = 1;

        public Sprite WeaponSprite;
        public Vector2 HandOffset = Vector2.zero;
        public float RotationOffset = 0f;
        public Vector2 Scale = Vector2.one;
    }
}