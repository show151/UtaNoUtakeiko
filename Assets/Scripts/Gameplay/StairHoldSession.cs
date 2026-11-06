using System;
using System.Collections.Generic;

namespace UtaNoUtakeiko.Gameplay
{
    // Tracks the red-button captures in order while the yellow button is held.
    public sealed class StairHoldSession
    {
        private readonly float[] positions;

        public int CapturedCount { get; private set; }
        public int Count => positions.Length;
        public bool IsComplete => CapturedCount == Count;
        public bool IsActive { get; private set; }

        public StairHoldSession(int noteCount)
        {
            if (noteCount < 2)
                throw new ArgumentOutOfRangeException(nameof(noteCount));
            positions = new float[noteCount];
        }

        public void Begin()
        {
            if (IsActive || CapturedCount != 0)
                throw new InvalidOperationException("Stair input has already started.");
            IsActive = true;
        }

        public bool TryCapture(float position)
        {
            if (!IsActive || IsComplete || float.IsNaN(position) ||
                float.IsInfinity(position) || position < 0f || position > 1f)
                return false;
            positions[CapturedCount++] = position;
            return true;
        }

        public void End() => IsActive = false;

        public float GetPosition(int index)
        {
            if (index < 0 || index >= CapturedCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return positions[index];
        }
    }
}
