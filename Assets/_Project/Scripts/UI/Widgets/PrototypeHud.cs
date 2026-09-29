using MiniBrawl.Gameplay.Combat;
using MiniBrawl.Gameplay.Player;
using MiniBrawl.Gameplay.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace MiniBrawl.UI.Widgets
{
    /// <summary>
    /// Fuel gauge plus the debug readout from §14 item 6. Network counters get added in Phase 2;
    /// what matters now is seeing fuel and frame rate while tuning the jetpack by feel.
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        public Image FuelFill;
        public Text Readout;

        const float k_RedrawInterval = 0.1f;

        readonly System.Text.StringBuilder m_Builder = new();
        PlayerDriver m_Driver;
        PlayerWeapon m_Weapon;
        Damageable[] m_Targets;
        float m_SmoothedFps;
        float m_NextRedraw;

        void Awake()
        {
            m_Driver = FindFirstObjectByType<PlayerDriver>();
            m_Weapon = FindFirstObjectByType<PlayerWeapon>();
            m_Targets = FindObjectsByType<Damageable>(FindObjectsSortMode.None);
        }

        void Update()
        {
            if (m_Driver == null) return;

            var state = m_Driver.State;
            float fuel = m_Driver.Config.FuelMax > 0f ? state.Fuel / m_Driver.Config.FuelMax : 0f;

            if (FuelFill != null)
            {
                FuelFill.fillAmount = fuel;
                FuelFill.color = fuel > 0.25f ? new Color(0.35f, 0.8f, 1f) : new Color(1f, 0.4f, 0.3f);
            }

            if (Readout == null) return;

            // Sampled every frame, drawn ten times a second: the text was rebuilt at 60 fps, and
            // the LINQ join below allocated a closure, an enumerator and a string each time.
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) m_SmoothedFps = Mathf.Lerp(m_SmoothedFps, 1f / dt, 0.1f);

            if (Time.unscaledTime < m_NextRedraw) return;
            m_NextRedraw = Time.unscaledTime + k_RedrawInterval;

            m_Builder.Clear();
            if (m_Targets == null || m_Targets.Length == 0) m_Builder.Append("none");
            foreach (Damageable target in m_Targets)
            {
                m_Builder.Append(target.IsDead ? "dead" : target.Health.ToString());
                m_Builder.Append(' ');
            }
            string targets = m_Builder.ToString();

            Readout.text =
                $"fps {m_SmoothedFps:0}  |  tick {m_Driver.Tick}  ({m_Driver.TicksThisFrame}/frame)\n" +
                $"fuel {fuel * 100f:0}%  |  {(state.Grounded ? "grounded" : "airborne")}\n" +
                $"pos {state.Position.x:0.0}, {state.Position.y:0.0}  |  vel {state.Velocity.x:0.0}, {state.Velocity.y:0.0}\n" +
                $"hits {(m_Weapon != null ? m_Weapon.Hits : 0)}  |  targets {targets}";
        }
    }
}
