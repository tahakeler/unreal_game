using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Stamina (player-character section 3 rule 2): max 100, sprint drains 15/s, carrying drains 8 * load/s,
    /// regenerates 25/s after a 1 s delay once nothing drains it; at 0 sprint locks until 30.
    /// Plain C# so it is unit-testable.
    /// </summary>
    public sealed class StaminaModel
    {
        readonly PlayerTuning _t;
        float _sinceDrain;

        /// <summary>Current stamina.</summary>
        public float Value { get; private set; }

        /// <summary>True after hitting 0 until stamina recovers to the resume value.</summary>
        public bool Exhausted { get; private set; }

        /// <summary>Sprinting is allowed.</summary>
        public bool CanSprint => !Exhausted && Value > 0f;

        /// <summary>Creates a full stamina bar.</summary>
        public StaminaModel(PlayerTuning tuning)
        {
            _t = tuning;
            Value = tuning.StaminaMax;
            _sinceDrain = tuning.StaminaRegenDelay;
        }

        /// <summary>Spends a lump (e.g. dive); returns false if not enough.</summary>
        public bool TrySpend(float amount)
        {
            if (Value < amount) return false;
            Value -= amount;
            _sinceDrain = 0f;
            return true;
        }

        /// <summary>Advances the model by <paramref name="dt"/>.</summary>
        public void Tick(float dt, bool sprinting, float loadRatio)
        {
            float drain = (sprinting && CanSprint ? _t.SprintDrain : 0f) + _t.CarryDrainPerLoad * Mathf.Clamp01(loadRatio);
            if (drain > 0f)
            {
                Value = Mathf.Max(0f, Value - drain * dt);
                _sinceDrain = 0f;
            }
            else
            {
                _sinceDrain += dt;
                if (_sinceDrain >= _t.StaminaRegenDelay) Value = Mathf.Min(_t.StaminaMax, Value + _t.StaminaRegen * dt);
            }

            if (Value <= 0f) Exhausted = true;
            else if (Exhausted && Value >= _t.SprintResumeStamina) Exhausted = false;
        }
    }
}
