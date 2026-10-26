using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Colapso da pirâmide (Fase 3): contagem regressiva, tremor contínuo e pedras com aviso no chão.</summary>
    public class CollapseController : MonoBehaviour
    {
        public float duration = 180f;
        public bool decorOnly;
        [Tooltip("Só pedrinhas inofensivas caindo (sem pedras grandes) enquanto a contagem corre.")]
        public bool pebbles;
        public float rockRadius = 1.7f;
        public float rockDamage = 25f;
        public float spawnRadius = 7f;
        public GameObject rockPrefab;

        // caminho contínuo (Fase 3): as pedras caem uma depois da outra seguindo-o, devagar, e matam quem estiver embaixo
        public List<Vector3> path;
        public float pathStep = 4f, pathInterval = 1.5f, warnDelay = 1.7f;
        float pathDist;

        public event Action TimeUp;
        public bool Running { get; private set; }
        public float TimeLeft { get; private set; }

        Transform player;
        float spawnTimer;

        public void Begin(Transform playerTransform)
        {
            player = playerTransform;
            TimeLeft = duration;
            Running = true;
            spawnTimer = 1f;
            pathDist = 0f;
            CameraShake.Hold(decorOnly ? 0.03f : 0.02f);
            AudioDirector.Sfx("rumble", 0.8f);
            if (!decorOnly && HUD.Instance != null) HUD.Instance.ShowTimer(true);
        }

        public void Stop()
        {
            Running = false;
            CameraShake.Hold(0f);
            if (HUD.Instance != null) HUD.Instance.ShowTimer(false);
        }

        void Update()
        {
            if (!Running || player == null) return;

            float elapsed01 = decorOnly ? 0.5f : 1f - Mathf.Clamp01(TimeLeft / duration);
            if (!decorOnly)
            {
                TimeLeft -= Time.deltaTime;
                if (HUD.Instance != null) HUD.Instance.SetTimer(TimeLeft);
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    Running = false;
                    CameraShake.Hold(0f);
                    if (TimeUp != null) TimeUp();
                    return;
                }
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                if (pebbles) { spawnTimer = UnityEngine.Random.Range(0.8f, 1.8f); SpawnPebble(); }
                else if (!decorOnly && path != null && path.Count > 1) { spawnTimer = pathInterval; SpawnPathRock(); }
                else { spawnTimer = Mathf.Lerp(2.0f, 0.7f, elapsed01); SpawnRock(elapsed01); }
            }
        }

        void SpawnPathRock()
        {
            pathDist += pathStep;
            float left = pathDist;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                float len = Vector3.Distance(a, b);
                if (left > len) { left -= len; continue; }
                Vector3 dir = (b - a).normalized;
                Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
                Vector3 p = a + dir * left + perp * UnityEngine.Random.Range(-0.8f, 0.8f);
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return;
                var go = new GameObject("FallingRock");
                go.AddComponent<FallingRock>().Run(hit.point, warnDelay, rockRadius, 9999f, player, rockPrefab);
                return;
            }
            path = null;     // chegou na máquina: acabaram as pedras
        }

        void SpawnPebble()
        {
            Vector2 c = UnityEngine.Random.insideUnitCircle * 6f;
            Vector3 origin = player.position + new Vector3(c.x, 2.5f, c.y);
            RaycastHit hit;
            if (!Physics.Raycast(origin, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.normal.y < 0.7f || hit.collider.transform.IsChildOf(player)) return;
            var go = new GameObject("FallingPebble");
            FallingRock r = go.AddComponent<FallingRock>();
            r.size = 0.25f;
            r.Run(hit.point, 1.0f, 0.45f, 0f, player, rockPrefab);
        }

        void SpawnRock(float elapsed01)
        {
            Vector2 c = UnityEngine.Random.insideUnitCircle * spawnRadius;
            Vector3 origin = player.position + player.forward * UnityEngine.Random.Range(0f, 5f) + new Vector3(c.x, 2.5f, c.y);
            RaycastHit hit;
            if (!Physics.Raycast(origin, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.normal.y < 0.7f || hit.collider.transform.IsChildOf(player)) return;

            var go = new GameObject("FallingRock");
            float delay = Mathf.Lerp(1.7f, 1.05f, elapsed01);
            go.AddComponent<FallingRock>().Run(hit.point, delay, rockRadius, decorOnly ? 0f : rockDamage, player, rockPrefab);
        }
    }
}
