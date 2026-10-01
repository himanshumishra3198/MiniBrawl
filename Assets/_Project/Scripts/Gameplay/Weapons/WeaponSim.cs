using System;
using UnityEngine;

namespace MiniBrawl.Gameplay.Weapons
{
    /// <summary>What a player is carrying. A byte because it travels in the reconcile.</summary>
    public enum WeaponKind : byte
    {
        Rifle = 0,
        Shotgun = 1,
        Pistol = 2,
    }

    /// <summary>Balance values for one weapon.</summary>
    [Serializable]
    public struct WeaponConfig
    {
        public float FireInterval;   // seconds between shots
        public float Range;
        public int Damage;           // per pellet

        [Tooltip("Shots fired at once. Above one the damage is per pellet, so the total depends " +
                 "on how many of them land.")]
        public int Pellets;

        [Tooltip("Total cone, in degrees. Pellets are spread evenly across it.")]
        public float Spread;

        [Tooltip("Rounds before dropping back to the rifle. Negative is unlimited.")]
        public int Ammo;

        public static WeaponConfig Default => For(WeaponKind.Rifle);

        /// <summary>
        /// The weapon table.
        ///
        /// Each one is meant to be the best answer to a different question, rather than a strictly
        /// better gun: the rifle has no weakness and no peak, the shotgun wins a room and loses a
        /// field, and the pistol out-damages both for as long as its magazine lasts.
        /// </summary>
        public static WeaponConfig For(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Shotgun:
                    return new WeaponConfig
                    {
                        FireInterval = 0.8f,
                        Range = 8f,          // a third of the rifle: this is a closing weapon
                        Damage = 11,         // 66 across six pellets, if every one lands
                        Pellets = 6,
                        Spread = 24f,
                        Ammo = 12,
                    };

                case WeaponKind.Pistol:
                    return new WeaponConfig
                    {
                        FireInterval = 0.09f,
                        Range = 16f,
                        Damage = 9,          // ~100 a second, against the rifle's 60
                        Pellets = 1,
                        Spread = 0f,
                        Ammo = 40,           // about four seconds of holding the trigger
                    };

                default:
                    return new WeaponConfig
                    {
                        FireInterval = 0.2f,
                        Range = 22f,
                        Damage = 12,
                        Pellets = 1,
                        Spread = 0f,
                        Ammo = -1,           // the weapon you always have
                    };
            }
        }
    }

    /// <summary>Per-weapon state. A struct so it can be rolled back during reconciliation.</summary>
    public struct WeaponState
    {
        public float Cooldown;
        public WeaponKind Kind;

        /// <summary>Negative means unlimited.</summary>
        public int Ammo;

        public static WeaponState Starting => new WeaponState
        {
            Cooldown = 0f,
            Kind = WeaponKind.Rifle,
            Ammo = -1,
        };

        public WeaponConfig Config => WeaponConfig.For(Kind);
    }

    /// <summary>
    /// Fire-rate logic, deterministic and free of Unity state so it can be re-simulated. The shot
    /// itself is resolved by the caller against an IHitscanWorld.
    /// </summary>
    public static class WeaponSim
    {
        /// <summary>Advances the cooldown by one tick and reports whether a shot goes out.</summary>
        public static bool Step(ref WeaponState state, bool wantsToFire, float dt)
        {
            WeaponConfig config = state.Config;

            if (state.Cooldown > 0f) state.Cooldown = Mathf.Max(0f, state.Cooldown - dt);

            if (!wantsToFire || state.Cooldown > 0f) return false;

            state.Cooldown = config.FireInterval;

            if (state.Ammo > 0)
            {
                state.Ammo--;

                // Out of rounds: back to the rifle, which is the one weapon that never runs dry.
                // Done here rather than at the call site so a replay reaches the same conclusion.
                if (state.Ammo == 0) state = WeaponState.Starting;
            }

            return true;
        }

        /// <summary>Picks a weapon up, replacing whatever was held.</summary>
        public static void Equip(ref WeaponState state, WeaponKind kind)
        {
            WeaponConfig config = WeaponConfig.For(kind);
            state.Kind = kind;
            state.Ammo = config.Ammo;
            // Cooldown is deliberately kept: swapping weapons mid-burst should not refund the
            // shot you were waiting on.
        }

        /// <summary>
        /// Which way one pellet goes.
        ///
        /// The scatter has to be identical on the firing client and on the server, or a predicted
        /// shotgun blast would disagree with the one that decided the damage. UnityEngine.Random
        /// cannot do that — it carries state that replays do not rewind — so the angle is hashed
        /// out of the tick and the pellet index instead. Same tick, same pellet, same direction,
        /// on every machine and on every replay of it.
        /// </summary>
        public static Vector2 Pellet(Vector2 aim, int index, uint tick, in WeaponConfig config)
        {
            if (config.Pellets <= 1 || config.Spread <= 0f) return aim;

            uint h = tick * 2654435761u + (uint)index * 40503u;
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;

            float offset = (h & 0xFFFFu) / 65535f - 0.5f;          // -0.5 .. 0.5
            float angle = offset * config.Spread * Mathf.Deg2Rad;

            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            return new Vector2(aim.x * c - aim.y * s, aim.x * s + aim.y * c);
        }
    }
}
