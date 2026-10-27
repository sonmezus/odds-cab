using UnityEngine;

namespace OddsCab
{
    /// <summary>Route-space arcade motion. The gameplay root stays aligned with the stop planner.</summary>
    public struct TaxiMotion
    {
        public float Speed { get; private set; }
        public float LateralVelocity { get; private set; }
        public float Acceleration { get; private set; }
        public float LateralAcceleration { get; private set; }
        float _lateralSpringVelocity;

        public void Reset(float speed = 0f)
        {
            Speed = Mathf.Max(0f, speed);
            LateralVelocity = Acceleration = LateralAcceleration = _lateralSpringVelocity = 0f;
        }

        public void Step(ref Vector3 position, float targetSpeed, float targetX, float laneSmoothTime,
            float maxLateralSpeed, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            // Bounded substeps keep braking distances and the lane spring stable across display refresh rates.
            int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / (1f / 60f)));
            float dt = deltaTime / steps;
            float previousSpeed = Speed;
            float previousLateral = LateralVelocity;
            float startX = position.x;
            targetSpeed = Mathf.Max(0f, targetSpeed);
            for (int i = 0; i < steps; i++)
            {
                float speedBefore = Speed;
                Speed = Mathf.MoveTowards(Speed, targetSpeed,
                    (targetSpeed > Speed ? GameConfig.Acceleration : GameConfig.Braking) * dt);
                position.z += (speedBefore + Speed) * 0.5f * dt;
                position.x = Mathf.SmoothDamp(position.x, targetX, ref _lateralSpringVelocity,
                    Mathf.Max(0.05f, laneSmoothTime), Mathf.Max(0.1f, maxLateralSpeed), dt);
            }
            LateralVelocity = (position.x - startX) / deltaTime;
            Acceleration = (Speed - previousSpeed) / deltaTime;
            LateralAcceleration = (LateralVelocity - previousLateral) / deltaTime;
        }
    }
}
