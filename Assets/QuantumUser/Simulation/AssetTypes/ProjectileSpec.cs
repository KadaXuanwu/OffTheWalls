using UnityEngine;
using Photon.Deterministic;

namespace Quantum {
    public partial class ProjectileSpec : AssetObject {
        public EntityPrototype ProjectilePrototype;
        public FP ProjectileSpeed = 25;
        public FP ProjectileTTL = 10;
        public FP ProjectileDamage = 10;
        public FP ShotOffset = 1;
        public int MaxBounces = 3;

        [Header("Visual Settings")]
        public Color BaseColor = new(0.5f, 0.5f, 0.5f, 1f);
        public float RedIncreasePerBounce = 0.1f;
    }
}