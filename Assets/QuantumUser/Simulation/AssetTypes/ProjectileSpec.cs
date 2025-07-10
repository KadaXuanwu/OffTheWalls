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
    }
}