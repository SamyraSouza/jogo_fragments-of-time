using System;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Vida do jogador (barra vertical do HUD).</summary>
    public class PlayerHealth : MonoBehaviour
    {
        public float maxHealth = 100f;

        public float Current { get; private set; }
        public bool Invulnerable { get; set; }
        public bool IsDead { get; private set; }

        public event Action<float, float> Changed;
        public event Action<float> Damaged;
        public event Action Died;

        /// <summary>Regeneração lenta: começa alguns segundos depois do último dano e devolve pouca vida por segundo.</summary>
        public float regenDelay = 12f;
        public float regenPerSecond = 0.8f;
        float lastDamage = -999f;

        void Awake() { Current = maxHealth; }

        void Update()
        {
            if (IsDead || Current >= maxHealth || Time.time - lastDamage < regenDelay) return;
            Heal(regenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || Invulnerable || amount <= 0f) return;
            // com a vida baixa os golpes pesam menos: demora mais para ele ser derrotado
            float frac = Current / maxHealth;
            if (frac < 0.4f) amount *= Mathf.Lerp(0.3f, 0.7f, frac / 0.4f);
            lastDamage = Time.time;
            Current = Mathf.Max(0f, Current - amount);
            if (Damaged != null) Damaged(amount);
            if (Changed != null) Changed(Current, maxHealth);
            if (Current <= 0f)
            {
                IsDead = true;
                if (Died != null) Died();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            if (Changed != null) Changed(Current, maxHealth);
        }
    }
}
