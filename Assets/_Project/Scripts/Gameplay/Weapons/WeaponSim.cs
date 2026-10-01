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

    /// <summary>
    /// What a player is carrying, and what is left in it. A struct so the whole inventory rolls
    /// back during reconciliation along with everything else.
    ///
    /// The rifle is not stored because it is never absent: it has no magazine and cannot be lost,
    /// so "do I hold the rifle" is always yes and only the other two need counting.
    /// </summary>
    public struct WeaponState
    {
        public float Cooldown;
        public WeaponKind Kind;

        public int ShotgunAmmo;
        public int PistolAmmo;

        /// <summary>
        /// Whether the switch button was down last tick. Carried in the state rather than kept in
        /// the component, because switching is edge-triggered: a replay that could not see the
        /// previous tick's button would switch again on every tick the button was held.
        /// </summary>
        public bool SwitchHeld;

        public static WeaponState Starting => new WeaponState { Kind = WeaponKind.Rifle };

        public WeaponConfig Config => WeaponConfig.For(Kind);

        /// <summary>Rounds left. Negative means the weapon never runs out.</summary>
        public int AmmoFor(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Shotgun: return ShotgunAmmo;
                case WeaponKind.Pistol: return PistolAmmo;
                default: return -1;
            }
        }

        public void SetAmmo(WeaponKind kind, int rounds)
        {
            if (kind == WeaponKind.Shotgun) ShotgunAmmo = rounds;
            else if (kind == WeaponKind.Pistol) PistolAmmo = rounds;
        }

        /// <summary>Whether this weapon can be switched to.</summary>
        public bool Holds(WeaponKind kind) => kind == WeaponKind.Rifle || AmmoFor(kind) > 0;
    }

    /// <summary>
    /// Fire-rate logic, deterministic and free of Unity state so it can be re-simulated. The shot
    /// itself is resolved by the caller against an IHitscanWorld.
    /// </summary>
    public static class WeaponSim
    {
        /// <summary>The order the switch button walks through.</summary>
        static readonly WeaponKind[] k_Order =
            { WeaponKind.Rifle, WeaponKind.Shotgun, WeaponKind.Pistol };

        /// <summary>
        /// Advances the weapon by one tick: handles a switch, ticks the cooldown, and reports
        /// whether a shot goes out.
        /// </summary>
        public static bool Step(ref WeaponState state, bool wantsToFire, bool wantsSwitch, float dt)
        {
            // Edge-triggered, so holding the button does not cycle the whole inventory in a third
            // of a second. The previous state lives in the struct so a replay sees the same edge.
            if (wantsSwitch && !state.SwitchHeld) Cycle(ref state);
            state.SwitchHeld = wantsSwitch;

            if (state.Cooldown > 0f) state.Cooldown = Mathf.Max(0f, state.Cooldown - dt);

            if (!wantsToFire || state.Cooldown > 0f) return false;

            state.Cooldown = state.Config.FireInterval;

            int ammo = state.AmmoFor(state.Kind);
            if (ammo > 0)
            {
                ammo--;
                state.SetAmmo(state.Kind, ammo);

                /* Out of rounds: the weapon is gone, not merely empty, and you fall back to the
                 * rifle. Done here rather than at the call site so a replay reaches the same
                 * conclusion as the server did. */
                if (ammo == 0) state.Kind = WeaponKind.Rifle;
            }

            return true;
        }

        /// <summary>Moves to the next weapon that is actually held, wrapping around.</summary>
        public static void Cycle(ref WeaponState state)
        {
            int from = System.Array.IndexOf(k_Order, state.Kind);

            for (int step = 1; step <= k_Order.Length; step++)
            {
                WeaponKind candidate = k_Order[(from + step) % k_Order.Length];
                if (!state.Holds(candidate)) continue;

                state.Kind = candidate;
                return;
            }
        }

        /// <summary>
        /// Picks a weapon up. It joins the inventory rather than replacing what is held, so a
        /// player who finds a shotgun still has the rifle to switch back to.
        /// </summary>
        public static void Equip(ref WeaponState state, WeaponKind kind)
        {
            state.SetAmmo(kind, WeaponConfig.For(kind).Ammo);
            state.Kind = kind;
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
