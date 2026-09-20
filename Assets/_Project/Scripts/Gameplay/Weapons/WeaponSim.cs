using System;
using UnityEngine;

namespace MiniBrawl.Gameplay.Weapons
{
    /// <summary>Balance values for the prototype's single weapon. Phase 4 moves these to a ScriptableObject.</summary>
    [Serializable]
    public struct WeaponConfig
    {
        public float FireInterval;   // seconds between shots
        public float Range;
        public int Damage;

        public static WeaponConfig Default => new WeaponConfig
        {
            FireInterval = 0.2f,     // §14: auto-fire every 0.2 s
            Range = 22f,
            Damage = 12,
        };
    }

    /// <summary>Per-weapon timing state. A struct so it can be rolled back during reconciliation.</summary>
    public struct WeaponState
    {
        public float Cooldown;
    }

    /// <summary>
    /// Fire-rate logic, deterministic and free of Unity state so it can be re-simulated. The shot
    /// itself is resolved by the caller against an IHitscanWorld.
    /// </summary>
    public static class WeaponSim
    {
        /// <summary>Advances the cooldown by one tick and reports whether a shot goes out.</summary>
        public static bool Step(ref WeaponState state, in WeaponConfig config, bool wantsToFire, float dt)
        {
            if (state.Cooldown > 0f) state.Cooldown = Mathf.Max(0f, state.Cooldown - dt);

            if (!wantsToFire || state.Cooldown > 0f) return false;

            state.Cooldown = config.FireInterval;
            return true;
        }
    }
}
