using MiniBrawl.Gameplay.Player;
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

        PlayerDriver m_Driver;
        float m_SmoothedFps;

        void Awake() => m_Driver = FindFirstObjectByType<PlayerDriver>();

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

            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) m_SmoothedFps = Mathf.Lerp(m_SmoothedFps, 1f / dt, 0.1f);

            Readout.text =
                $"fps {m_SmoothedFps:0}  |  tick {m_Driver.Tick}  ({m_Driver.TicksThisFrame}/frame)\n" +
                $"fuel {fuel * 100f:0}%  |  {(state.Grounded ? "grounded" : "airborne")}\n" +
                $"pos {state.Position.x:0.0}, {state.Position.y:0.0}  |  vel {state.Velocity.x:0.0}, {state.Velocity.y:0.0}";
        }
    }
}
