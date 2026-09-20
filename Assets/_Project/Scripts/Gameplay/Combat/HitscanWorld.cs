using UnityEngine;

namespace MiniBrawl.Gameplay.Combat
{
    /// <summary>What a hitscan shot found. Distance is used to draw the tracer.</summary>
    public struct HitscanHit
    {
        public bool Hit;
        public Vector2 Point;
        public float Distance;
        public Collider2D Collider;
    }

    /// <summary>
    /// Raycast abstraction, mirroring IMotorCollision. Phase 7's lag compensation rewinds hitboxes
    /// behind this interface, so weapon code never talks to Physics2D directly.
    /// </summary>
    public interface IHitscanWorld
    {
        HitscanHit Raycast(Vector2 origin, Vector2 direction, float distance);
    }

    /// <summary>Physics2D-backed implementation used by the offline prototype and the host.</summary>
    public sealed class Physics2DHitscan : IHitscanWorld
    {
        readonly ContactFilter2D m_Filter;
        readonly RaycastHit2D[] m_Hits = new RaycastHit2D[8];

        public Physics2DHitscan(LayerMask mask)
        {
            m_Filter = new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = false };
        }

        public HitscanHit Raycast(Vector2 origin, Vector2 direction, float distance)
        {
            int count = Physics2D.Raycast(origin, direction, m_Filter, m_Hits, distance);

            var best = new HitscanHit { Distance = distance };
            for (int i = 0; i < count; i++)
            {
                if (best.Hit && m_Hits[i].distance >= best.Distance) continue;
                best = new HitscanHit
                {
                    Hit = true,
                    Point = m_Hits[i].point,
                    Distance = m_Hits[i].distance,
                    Collider = m_Hits[i].collider,
                };
            }
            return best;
        }
    }
}
