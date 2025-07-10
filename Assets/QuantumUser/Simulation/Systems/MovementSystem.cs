namespace Quantum {
    using Photon.Deterministic;
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class MovementSystem : SystemMainThreadFilter<MovementSystem.Filter> {

        public override void Update(Frame f, ref Filter filter) {
            var input = f.GetPlayerInput(filter.Link->Player);
            var entity = filter.Entity;

            // Handle dashing
            if (f.TryGet(entity, out Dashing dash)) {
                if (dash.RemainingFrames > 0) {
                    FP dashSpeed = 30;
                    filter.PhysicsBody->Velocity = dash.Direction * dashSpeed;
                    dash.RemainingFrames--;
                    f.Set(entity, dash);
                    return;
                }
                else {
                    f.Remove<Dashing>(entity);
                }
            }

            var direction = input->Direction;
            if (direction.Magnitude > 1) {
                direction = direction.Normalized;
            }

            // Handle dash input
            if (input->Dash.WasPressed && direction.Magnitude > FP._0) {
                Dashing newDash = new Dashing {
                    Direction = direction,
                    RemainingFrames = 5
                };
                f.Add(entity, newDash);
                return;
            }

            // Movement
            FP moveSpeed = FP._5;
            filter.PhysicsBody->Velocity = direction * moveSpeed;

            if (input->MousePosition != FPVector2.Zero) {
                FPVector2 characterPos = filter.Transform->Position.XY;
                FPVector2 mouseDirection = input->MousePosition - characterPos;
                if (mouseDirection.Magnitude > FP._0_01) {
                    FP angle = FPMath.Atan2(mouseDirection.Y, mouseDirection.X);
                    filter.Transform->Rotation = angle - FP.PiOver2;
                }
            }
        }

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PhysicsBody2D* PhysicsBody;
            public PlayerLink* Link;
        }
    }
}