using UnityEngine;
using UnityEngine.InputSystem;

namespace RiftOfTime
{
    /// <summary>Rolamento para o lado (clique direito do mouse), só ligado durante a luta contra o chefe; invulnerável enquanto rola.</summary>
    public class PlayerDodge : MonoBehaviour
    {
        public float rollTime = 0.45f, rollSpeed = 10f, cooldown = 1.1f, tilt = 22f;

        SimpleFPSController fps;
        PlayerHealth health;
        float rollEnd, nextRoll, side;

        void Awake()
        {
            fps = GetComponent<SimpleFPSController>();
            health = GetComponent<PlayerHealth>();
        }

        void OnDisable()
        {
            if (health != null) health.Invulnerable = false;
            if (fps != null) fps.rollTilt = 0f;
        }

        void Update()
        {
            if (fps == null || Mouse.current == null || Time.timeScale <= 0f) return;
            bool rolling = Time.time < rollEnd;

            if (!rolling && fps.rollTilt != 0f)
            {
                fps.rollTilt = 0f;
                if (health != null) health.Invulnerable = false;
            }

            if (!rolling && Time.time >= nextRoll && Mouse.current.rightButton.wasPressedThisFrame && (health == null || !health.IsDead))
            {
                side = 1f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.aKey.isPressed && !Keyboard.current.dKey.isPressed) side = -1f;
                }
                rollEnd = Time.time + rollTime;
                nextRoll = Time.time + cooldown;
                if (health != null) health.Invulnerable = true;
                fps.rollTilt = -side * tilt;
            }

            if (Time.time < rollEnd)
            {
                float k = 1f - (rollEnd - Time.time) / rollTime;
                float speed = rollSpeed * Mathf.Lerp(1.2f, 0.4f, k);
                fps.Push(transform.right * side * speed, Time.deltaTime * 1.5f);
            }
        }
    }
}
