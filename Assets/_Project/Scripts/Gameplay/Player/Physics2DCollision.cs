using UnityEngine;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>
    /// Physics2D.BoxCast-backed implementation of the motor's collision probe (§14: no Rigidbody2D
    /// on players). Allocation-free after construction so reconciliation can re-run it cheaply.
    /// </summary>
    public sealed class Physics2DCollision : IMotorCollision
    {
        readonly ContactFilter2D m_Filter;
        readonly RaycastHit2D[] m_Hits = new RaycastHit2D[8];

        public Physics2DCollision(LayerMask levelMask)
        {
            m_Filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = levelMask,
                useTriggers = false,
            };
        }

        public bool Cast(Vector2 origin, Vector2 size, Vector2 direction, float distance, out float hitDistance)
        {
            int count = Physics2D.BoxCast(origin, size, 0f, direction, m_Filter, m_Hits, distance);

            hitDistance = 0f;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                // A cast that starts already overlapping reports distance 0; keeping the nearest
                // hit means the motor stops rather than tunnelling through.
                if (found && m_Hits[i].distance >= hitDistance) continue;
                hitDistance = m_Hits[i].distance;
                found = true;
            }
            return found;
        }
    }
}
