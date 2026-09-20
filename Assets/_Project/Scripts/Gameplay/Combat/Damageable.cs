using System;
using UnityEngine;

namespace MiniBrawl.Gameplay.Combat
{
    /// <summary>
    /// Prototype health holder: flashes white when hit, disappears on death, comes back after a
    /// delay (§14 item 4). Phase 2 moves the authority for this to the host.
    /// </summary>
    public sealed class Damageable : MonoBehaviour
    {
        public int MaxHealth = 100;
        public float RespawnDelay = 3f;

        const float k_FlashDuration = 0.08f;

        SpriteRenderer m_Renderer;
        Collider2D m_Collider;
        Color m_BaseColor;
        float m_FlashRemaining;
        float m_RespawnRemaining;
        int m_Health;

        public int Health => m_Health;
        public bool IsDead => m_Health <= 0;

        /// <summary>Fired on every hit, with the damage applied.</summary>
        public event Action<Damageable, int> Damaged;

        void Awake()
        {
            m_Renderer = GetComponent<SpriteRenderer>();
            m_Collider = GetComponent<Collider2D>();
            if (m_Renderer != null) m_BaseColor = m_Renderer.color;
            m_Health = MaxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            m_Health = Mathf.Max(0, m_Health - amount);
            m_FlashRemaining = k_FlashDuration;
            Damaged?.Invoke(this, amount);

            if (m_Health == 0)
            {
                m_RespawnRemaining = RespawnDelay;
                SetVisible(false);
            }
        }

        public void Respawn()
        {
            m_Health = MaxHealth;
            m_RespawnRemaining = 0f;
            SetVisible(true);
        }

        void Update()
        {
            if (IsDead)
            {
                m_RespawnRemaining -= Time.deltaTime;
                if (m_RespawnRemaining <= 0f) Respawn();
                return;
            }

            if (m_FlashRemaining <= 0f || m_Renderer == null) return;

            m_FlashRemaining -= Time.deltaTime;
            m_Renderer.color = m_FlashRemaining > 0f ? Color.white : m_BaseColor;
        }

        void SetVisible(bool visible)
        {
            if (m_Renderer != null)
            {
                m_Renderer.enabled = visible;
                m_Renderer.color = m_BaseColor;
            }
            if (m_Collider != null) m_Collider.enabled = visible;
        }
    }
}
