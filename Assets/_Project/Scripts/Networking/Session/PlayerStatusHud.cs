using MiniBrawl.Gameplay.Player;
using MiniBrawl.Gameplay.Weapons;
using MiniBrawl.Networking.Replication;
using UnityEngine;
using UnityEngine.UI;

namespace MiniBrawl.Networking.Session
{
    /// <summary>
    /// Your own health, jetpack fuel and weapon, where you can see them.
    ///
    /// None of this was on screen before. Health was readable only from the nameplate above your
    /// own head — which is where you are least likely to be looking — and fuel and ammo appeared
    /// only in the developer readout, which is now off by default. Fuel is the worst of the three
    /// to be missing: the jetpack is the whole movement system, and running dry mid-climb with no
    /// warning is the difference between a mistake and a surprise.
    ///
    /// Drawn from reconciled state, so it is correct without anything extra being sent.
    /// </summary>
    public sealed class PlayerStatusHud : MonoBehaviour
    {
        public Image HealthFill;
        public Image FuelFill;
        public Text WeaponLabel;
        public CanvasGroup Group;

        /// <summary>Below this, the bar pulses. Enough warning to get back to the ground.</summary>
        const float k_LowFuel = 0.3f;
        const float k_LowHealth = 0.35f;

        static readonly Color k_HealthFull = new Color(0.42f, 0.86f, 0.44f);
        static readonly Color k_HealthLow = new Color(0.94f, 0.33f, 0.28f);
        static readonly Color k_Fuel = new Color(0.40f, 0.74f, 1f);
        static readonly Color k_FuelLow = new Color(1f, 0.70f, 0.25f);

        NetworkPlayerMotor m_Local;

        void Update()
        {
            if (m_Local == null)
            {
                foreach (NetworkPlayerMotor motor in FindObjectsByType<NetworkPlayerMotor>(FindObjectsSortMode.None))
                {
                    if (!motor.IsOwner) continue;
                    m_Local = motor;
                    break;
                }
            }

            // Hidden until there is something to report, so the panel does not sit on the menu
            // showing a full bar for a player who has not spawned.
            if (Group != null) Group.alpha = m_Local == null ? 0f : 1f;
            if (m_Local == null) return;

            PlayerState state = m_Local.State;
            float health = Mathf.Clamp01(state.Health / 100f);
            float fuel = Mathf.Clamp01(state.Fuel);

            if (HealthFill != null)
            {
                HealthFill.fillAmount = health;
                HealthFill.color = Pulse(Color.Lerp(k_HealthLow, k_HealthFull, health),
                                         health <= k_LowHealth && health > 0f);
            }

            if (FuelFill != null)
            {
                FuelFill.fillAmount = fuel;
                FuelFill.color = Pulse(fuel <= k_LowFuel ? k_FuelLow : k_Fuel, fuel <= k_LowFuel);
            }

            if (WeaponLabel != null)
            {
                WeaponState weapon = m_Local.Weapon;
                int rounds = weapon.AmmoFor(weapon.Kind);
                string text = rounds < 0
                    ? weapon.Kind.ToString().ToUpperInvariant()
                    : $"{weapon.Kind.ToString().ToUpperInvariant()}  {rounds}";

                if (WeaponLabel.text != text) WeaponLabel.text = text;   // setting it rebuilds the mesh
            }
        }

        /// <summary>
        /// Brightens on a sine when something is nearly out.
        ///
        /// Colour alone is a weak warning on a phone held at arm's length in daylight, and the
        /// same colour is already carrying how full the bar is. Movement is what the eye catches
        /// without being pointed at it.
        /// </summary>
        static Color Pulse(Color colour, bool urgent)
        {
            if (!urgent) return colour;

            float t = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
            return new Color(colour.r * t + (1f - t), colour.g * t, colour.b * t, 1f);
        }
    }
}
