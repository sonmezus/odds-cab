using UnityEngine;

namespace OddsCab
{
    public enum CabAudioBus { Interface, Feedback, Mechanical, Voice, Ambience, Music }
    public enum CabAudioCue
    {
        UiPress, UiHover, Click, Confirm, PanelOpen, PanelClose, Correct, Wrong, Pickup, Rewind,
        BrakeWarning, DoorOpen, DoorClose, BrakeSqueal, Horn, Wiper, GearShift, Hint, Coin, Timer,
        Bell1, Bell2, Bell3, Bell4, PassengerVoice, Gull, Bird, FerryHorn, Count
    }

    public readonly struct CabCueSettings
    {
        public readonly CabAudioBus Bus;
        public readonly float Gain, PitchCents, Cooldown, Duck;
        public readonly int Priority;
        public CabCueSettings(CabAudioBus bus, float gain, float pitchCents, int priority,
            float cooldown = 0f, float duck = 1f)
        {
            Bus = bus; Gain = gain; PitchCents = pitchCents;
            Priority = priority; Cooldown = cooldown; Duck = duck;
        }
    }

    /// <summary>Pure allocation-free cue policy. Lower priority numbers protect important feedback.</summary>
    public static class AudioCuePolicy
    {
        static readonly CabCueSettings[] Settings =
        {
            new CabCueSettings(CabAudioBus.Interface, .42f, 36f, 150, .035f),
            new CabCueSettings(CabAudioBus.Interface, .22f, 28f, 180, .055f),
            new CabCueSettings(CabAudioBus.Interface, .56f, 24f, 110, .045f),
            new CabCueSettings(CabAudioBus.Interface, .62f, 14f, 80, .06f),
            new CabCueSettings(CabAudioBus.Interface, .46f, 18f, 110, .10f),
            new CabCueSettings(CabAudioBus.Interface, .36f, 18f, 120, .10f),
            new CabCueSettings(CabAudioBus.Feedback, .82f, 0f, 10, .20f, .52f),
            new CabCueSettings(CabAudioBus.Feedback, .66f, 0f, 10, .20f, .60f),
            new CabCueSettings(CabAudioBus.Feedback, .70f, 0f, 25, .20f, .72f),
            new CabCueSettings(CabAudioBus.Feedback, .62f, 0f, 20, .20f, .64f),
            new CabCueSettings(CabAudioBus.Mechanical, .44f, 10f, 45, .45f, .82f),
            new CabCueSettings(CabAudioBus.Mechanical, .67f, 38f, 60, .12f),
            new CabCueSettings(CabAudioBus.Mechanical, .72f, 33f, 60, .12f),
            new CabCueSettings(CabAudioBus.Mechanical, .31f, 44f, 90, .60f),
            new CabCueSettings(CabAudioBus.Mechanical, .50f, 22f, 55, .45f),
            new CabCueSettings(CabAudioBus.Mechanical, .21f, 20f, 170, .30f),
            new CabCueSettings(CabAudioBus.Mechanical, .21f, 28f, 150, .35f),
            new CabCueSettings(CabAudioBus.Feedback, .58f, 0f, 30, .20f, .74f),
            new CabCueSettings(CabAudioBus.Interface, .43f, 12f, 120, .025f),
            new CabCueSettings(CabAudioBus.Feedback, .42f, 0f, 35, .35f, .88f),
            new CabCueSettings(CabAudioBus.Feedback, .72f, 0f, 25),
            new CabCueSettings(CabAudioBus.Feedback, .72f, 0f, 25),
            new CabCueSettings(CabAudioBus.Feedback, .72f, 0f, 25),
            new CabCueSettings(CabAudioBus.Feedback, .72f, 0f, 25),
            new CabCueSettings(CabAudioBus.Voice, .62f, 68f, 35, .75f, .48f),
            new CabCueSettings(CabAudioBus.Ambience, .15f, 95f, 225, 2f),
            new CabCueSettings(CabAudioBus.Ambience, .18f, 75f, 230, 1f),
            new CabCueSettings(CabAudioBus.Ambience, .19f, 28f, 220, 20f)
        };
        public static CabCueSettings Get(CabAudioCue cue) => Settings[Mathf.Clamp((int)cue, 0, Settings.Length - 1)];
        public static uint Next(ref uint state)
        {
            if (state == 0u) state = 0x9E3779B9u;
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return state;
        }
        public static float Unit(ref uint state) => (Next(ref state) >> 8) * (1f / 16777216f);
        public static float SamplePitch(float cents, ref uint state) =>
            Mathf.Pow(2f, (Unit(ref state) * 2f - 1f) * Mathf.Abs(cents) / 1200f);

        /// <summary>Use an idle voice first; otherwise replace only an equal or less important cue.</summary>
        public static int SelectVoice(double now, int requestedPriority, double[] endTimes, int[] priorities)
        {
            if (endTimes == null || priorities == null || endTimes.Length != priorities.Length) return -1;
            int candidate = -1;
            for (int i = 0; i < endTimes.Length; i++)
            {
                if (endTimes[i] <= now) return i;
                if (priorities[i] < requestedPriority) continue;
                if (candidate < 0 || priorities[i] > priorities[candidate] ||
                    (priorities[i] == priorities[candidate] && endTimes[i] < endTimes[candidate])) candidate = i;
            }
            return candidate;
        }
    }
}
