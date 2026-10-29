using UnityEngine;

namespace GameJam.Presentation
{
    // Presentation time only: this class never requests movement or spends resources.
    public sealed class IdleLookClock
    {
        public bool Looking { get; private set; }
        public bool Consumed { get; private set; }
        public float Elapsed { get; private set; }
        public void Reset() { Looking = false; Consumed = false; Elapsed = 0f; }
        public void Step(float seconds, bool eligible, float delay, float lookLength)
        {
            if (!eligible) { Reset(); return; }
            Elapsed += Mathf.Max(0f, seconds);
            if (Looking && Elapsed >= lookLength)
            { Looking = false; Consumed = true; Elapsed = 0f; }
            else if (!Looking && !Consumed && Elapsed >= delay)
            { Looking = true; Elapsed = 0f; }
        }
    }
}
