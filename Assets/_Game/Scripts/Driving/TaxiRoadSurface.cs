using UnityEngine;

namespace OddsCab
{
    public enum TaxiRoadSurfaceKind { Asphalt, Cobblestone, Curb }

    /// <summary>Analytic contact with the streamed route. Visual street meshes deliberately have no physics colliders.</summary>
    public static class TaxiRoadSurface
    {
        public const float CurbHeight = 0.14f;
        public const float TrackWidth = 1.04f;
        public const float HalfWheelbase = 0.86f;

        public static TaxiRoadSurfaceKind Classify(float x, TourRoadType road)
        {
            if (Mathf.Abs(x) >= GameConfig.RoadHalfWidth) return TaxiRoadSurfaceKind.Curb;
            return road == TourRoadType.Hill ? TaxiRoadSurfaceKind.Cobblestone : TaxiRoadSurfaceKind.Asphalt;
        }

        public static float Height(float x, float z, TourRoadType road)
        {
            var kind = Classify(x, road);
            if (kind == TaxiRoadSurfaceKind.Curb)
                return CurbHeight + StoneRelief(x, z) * 0.42f;
            if (kind == TaxiRoadSurfaceKind.Cobblestone) return StoneRelief(x, z);
            // Long, very shallow irregularities plus a fine aggregate texture; keyed to metres, never time.
            return Mathf.Sin(z * 1.87f + x * .72f) * .0018f
                + Mathf.Sin(z * 17.6f + x * 4.1f) * .00065f;
        }

        static float StoneRelief(float x, float z)
        {
            float row = Mathf.Floor(z / .23f);
            float stagger = ((int)row & 1) == 0 ? 0f : .105f;
            float u = Mathf.Repeat((x + stagger) / .21f, 1f);
            float v = Mathf.Repeat(z / .23f, 1f);
            float crown = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI);
            return crown * .014f - .004f + Mathf.Sin(row * 2.31f + x * 3.2f) * .0015f;
        }
    }

    /// <summary>Four contact patches feed a bounded suspension response; this struct owns no managed storage.</summary>
    public struct TaxiRoadFeel
    {
        public float Heave { get; private set; }
        public float Pitch { get; private set; }
        public float Roll { get; private set; }
        public float CameraHeave { get; private set; }
        public float CameraRoll { get; private set; }
        public float Impact { get; private set; }
        public float CobbleBlend { get; private set; }
        public float FrontLeft { get; private set; }
        public float FrontRight { get; private set; }
        public float RearLeft { get; private set; }
        public float RearRight { get; private set; }
        float _heaveVelocity, _lastFL, _lastFR, _lastRL, _lastRR, _impactEnvelope;
        bool _ready;

        public void Reset() { this = default; }

        public void Step(float x, float z, float speed, float lateralSpeed, TourRoadType road, float dt)
        {
            if (dt <= 0f) return;
            float halfTrack = TaxiRoadSurface.TrackWidth * .5f;
            float fl = TaxiRoadSurface.Height(x - halfTrack, z + TaxiRoadSurface.HalfWheelbase, road);
            float fr = TaxiRoadSurface.Height(x + halfTrack, z + TaxiRoadSurface.HalfWheelbase, road);
            float rl = TaxiRoadSurface.Height(x - halfTrack, z - TaxiRoadSurface.HalfWheelbase, road);
            float rr = TaxiRoadSurface.Height(x + halfTrack, z - TaxiRoadSurface.HalfWheelbase, road);
            float travel = Mathf.Clamp01((Mathf.Abs(speed) + Mathf.Abs(lateralSpeed)) / 2f);
            float jump = _ready ? Mathf.Max(Mathf.Max(Mathf.Abs(fl - _lastFL), Mathf.Abs(fr - _lastFR)),
                Mathf.Max(Mathf.Abs(rl - _lastRL), Mathf.Abs(rr - _lastRR))) : 0f;
            Impact = jump > .055f ? Mathf.Clamp01(jump * 5.4f * travel) : 0f;
            _impactEnvelope = Mathf.Max(Impact, _impactEnvelope * Mathf.Exp(-dt * 13f));
            _lastFL = fl; _lastFR = fr; _lastRL = rl; _lastRR = rr; _ready = true;
            float average = (fl + fr + rl + rr) * .25f;
            float target = average * (average > .045f ? 1f : travel);
            Heave = Mathf.SmoothDamp(Heave, target, ref _heaveVelocity, road == TourRoadType.Hill ? .065f : .11f,
                .9f, Mathf.Min(dt, .1f));
            float response = 1f - Mathf.Exp(-dt * 17f);
            Pitch = Mathf.Lerp(Pitch, Mathf.Clamp(Mathf.Atan2((rl + rr - fl - fr) * .5f,
                TaxiRoadSurface.HalfWheelbase * 2f) * Mathf.Rad2Deg * travel, -2.3f, 2.3f), response);
            Roll = Mathf.Lerp(Roll, Mathf.Clamp(Mathf.Atan2((fr + rr - fl - rl) * .5f,
                TaxiRoadSurface.TrackWidth) * Mathf.Rad2Deg * travel, -4.5f, 4.5f), response);
            FrontLeft = Mathf.Clamp(fl - Heave, -.045f, .055f);
            FrontRight = Mathf.Clamp(fr - Heave, -.045f, .055f);
            RearLeft = Mathf.Clamp(rl - Heave, -.045f, .055f);
            RearRight = Mathf.Clamp(rr - Heave, -.045f, .055f);
            CobbleBlend = Mathf.MoveTowards(CobbleBlend, road == TourRoadType.Hill ? 1f : 0f, dt * 3f);
            CameraHeave = Mathf.Clamp(Heave * .18f + _impactEnvelope * Mathf.Sin(z * 9f) * .008f, -.005f, .016f);
            CameraRoll = Mathf.Clamp(Roll * .10f + _impactEnvelope * .22f, -.5f, .5f);
        }
    }
}
