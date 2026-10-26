using System;
using System.Collections;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>A queda do jogador entre a Fase 2 e a Fase 3.</summary>
    public class FallCinematic : MonoBehaviour
    {
        Transform player;
        Rigidbody rb;
        Camera cam;
        SimpleFPSController fps;
        PlayerCombat combat;
        PlayerInteractor interactor;
        float baseFov = 60f;

        public static FallCinematic Create(Transform playerTransform)
        {
            var go = new GameObject("QuedaDoJogador");
            var f = go.AddComponent<FallCinematic>();
            f.player = playerTransform;
            f.rb = playerTransform.GetComponent<Rigidbody>();
            f.cam = playerTransform.GetComponentInChildren<Camera>();
            f.fps = playerTransform.GetComponent<SimpleFPSController>();
            f.combat = playerTransform.GetComponent<PlayerCombat>();
            f.interactor = playerTransform.GetComponent<PlayerInteractor>();
            if (f.cam != null) f.baseFov = f.cam.fieldOfView;
            return f;
        }

        void Lock(bool locked)
        {
            if (fps != null) fps.enabled = !locked;
            if (combat != null) combat.enabled = !locked;
            if (interactor != null) interactor.enabled = !locked;
        }

        void SetKinematic(bool kinematic)
        {
            if (rb == null) return;
            if (kinematic) rb.linearVelocity = Vector3.zero;
            rb.isKinematic = kinematic;
            if (!kinematic) rb.linearVelocity = Vector3.zero;
        }

        void Move(Vector3 pos)
        {
            player.position = pos;
            if (rb != null) rb.position = pos;
        }

        void SetCamera(float pitch, float roll, float fov)
        {
            if (cam == null) return;
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
            cam.fieldOfView = fov;
        }

        /// <summary>Leva o jogador ao centro do alçapão, abre o chão e faz a queda.</summary>
        public IEnumerator FallThroughTrapdoor(TrapdoorFloor trapdoor, Action beginTransition)
        {
            Lock(true);
            SetKinematic(true);

            Vector3 center = player.position + player.forward * 2f;
            if (trapdoor != null)
            {
                Bounds b = default(Bounds);
                bool first = true;
                foreach (Renderer r in trapdoor.GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = r.bounds; first = false; }
                    else b.Encapsulate(r.bounds);
                }
                if (!first) center = new Vector3(b.center.x, player.position.y, b.center.z);
            }

            Vector3 from = player.position;
            Vector3 to = new Vector3(center.x, from.y, center.z);
            Quaternion camFrom = cam != null ? cam.transform.localRotation : Quaternion.identity;
            float pitchFrom = camFrom.eulerAngles.x > 180f ? camFrom.eulerAngles.x - 360f : camFrom.eulerAngles.x;
            Vector3 toward = to - from; toward.y = 0f;
            Quaternion bodyFrom = player.rotation;
            Quaternion bodyTo = toward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toward.normalized, Vector3.up) : bodyFrom;
            float t = 0f, dur = 1.25f, nextPuff = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                Move(Vector3.Lerp(from, to, k));
                player.rotation = Quaternion.Slerp(bodyFrom, bodyTo, Mathf.Clamp01(k * 2f));
                SetCamera(Mathf.Lerp(pitchFrom, 32f, k), Mathf.Sin(t * 9f) * 1.5f * k, baseFov);
                CameraShake.Hold(0.04f + 0.05f * k);
                nextPuff -= Time.deltaTime;
                if (nextPuff <= 0f)
                {
                    nextPuff = 0.3f;
                    Vector3 p = Vector3.Lerp(from, to, k) + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f, UnityEngine.Random.Range(-1.5f, 1.5f));
                    Fx.ImpactDust(p, 0.7f);
                }
                yield return null;
            }

            CameraShake.Kick(0.2f, 0.6f);
            AudioDirector.Sfx("rumble", 1f);
            if (trapdoor != null) { trapdoor.dropDistance = 60f; trapdoor.Open(1.8f); }
            SetKinematic(false);
            if (rb != null) rb.linearVelocity = new Vector3(0f, -1f, 0f);

            float ft = 0f, fall = 3.2f;
            bool began = false;
            while (ft < fall)
            {
                ft += Time.deltaTime;
                float k = Mathf.Clamp01(ft / 1.6f);
                float e = k * k * (3f - 2f * k);
                SetCamera(Mathf.Lerp(32f, 62f, e), Mathf.Sin(ft * 4.2f) * 9f * e, Mathf.Lerp(baseFov, baseFov + 38f, e));
                CameraShake.Hold(Mathf.Lerp(0.06f, 0.16f, e));
                if (!began && ft > 0.95f) { began = true; if (beginTransition != null) beginTransition(); }
                yield return null;
            }
            CameraShake.Hold(0f);
        }

        /// <summary>Abre um buraco no teto sobre o jogador e o faz cair por ele até o chão da câmara.</summary>
        public IEnumerator ArriveFromAbove(GameManager gm, GameObject rockPrefab)
        {
            Lock(true);

            float floorY = player.position.y;
            RaycastHit hit;
            if (Physics.Raycast(player.position + Vector3.up * 0.2f, Vector3.down, out hit, 10f, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
            Vector3 spot = new Vector3(player.position.x, floorY, player.position.z);
            HideCeilingTile(spot);

            const float height = 7f;
            SetKinematic(true);
            Move(spot + Vector3.up * height);
            SetCamera(40f, 0f, baseFov + 30f);

            float wait = 0f;
            while (gm != null && gm.Fader != null && gm.Fader.Covered && wait < 8f) { wait += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSeconds(0.15f);

            AudioDirector.Sfx("rumble", 0.9f);
            ParticleSystem dust = Fx.CeilingDust(spot, 1.8f, 5.1f, null);
            Light shaft = MakeShaft(spot, 5.6f);
            StartCoroutine(Rubble(spot, floorY, rockPrefab));

            SetKinematic(false);
            if (rb != null) rb.linearVelocity = new Vector3(0f, -2f, 0f);
            float t = 0f;
            bool landed = false;
            while (!landed && t < 5f)
            {
                t += Time.deltaTime;
                float h = Mathf.Clamp01((player.position.y - floorY) / height);
                SetCamera(Mathf.Lerp(8f, 40f, h * h), Mathf.Sin(t * 5f) * 7f * h, Mathf.Lerp(baseFov, baseFov + 30f, h));
                CameraShake.Hold(0.1f * h);
                landed = t > 0.3f && Physics.Raycast(player.position + Vector3.up * 0.3f, Vector3.down, 0.5f, ~0, QueryTriggerInteraction.Ignore);
                yield return null;
            }

            CameraShake.Hold(0f);
            CameraShake.Kick(0.28f, 0.8f);
            AudioDirector.Sfx("fall", 1f);
            AudioDirector.Sfx("rumble", 0.7f);
            Fx.ImpactDust(spot, 1.8f);
            StartCoroutine(Fx.Shockwave(spot, 0.6f, 5.5f, new Color(1f, 0.85f, 0.6f, 0.7f), 0.7f));

            float dip = 0f;
            while (dip < 0.55f)
            {
                dip += Time.deltaTime;
                float k = dip / 0.55f;
                SetCamera(Mathf.Sin(k * Mathf.PI) * 16f, Mathf.Sin(k * Mathf.PI * 2f) * 3f, baseFov);
                yield return null;
            }
            SetCamera(0f, 0f, baseFov);
            Lock(false);

            if (dust != null) dust.Stop();
            yield return StartCoroutine(FadeLight(shaft, 4f));
            if (dust != null) Destroy(dust.gameObject, 2f);
            Destroy(gameObject, 16f);
        }

        void HideCeilingTile(Vector3 spot)
        {
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                Bounds b = r.bounds;
                if (b.center.y < spot.y + 3.5f || b.center.y > spot.y + 7f) continue;
                if (spot.x < b.min.x || spot.x > b.max.x || spot.z < b.min.z || spot.z > b.max.z) continue;
                Transform tile = r.transform;
                if (!tile.name.StartsWith("Ceiling")) { bool found = false; for (Transform p = tile.parent; p != null; p = p.parent) if (p.name.StartsWith("Ceiling")) { tile = p; found = true; break; } if (!found) continue; }
                while (tile.parent != null && tile.parent.name.StartsWith("Ceiling")) tile = tile.parent;
                tile.gameObject.SetActive(false);
                BuildDarkShaft(b);
                return;
            }
        }

        /// <summary>O buraco dá para o escuro: um poço preto (4 paredes e uma tampa) sobre o buraco do teto, de onde o jogador cai.</summary>
        static void BuildDarkShaft(Bounds tile)
        {
            const float rise = 12f;
            float s = Mathf.Max(tile.size.x, 4.5f);
            float baseY = tile.max.y;
            var root = new GameObject("PocoEscuroDoBuraco").transform;
            Material black = RotMaterials.Unlit(Color.black);
            Vector3 c = new Vector3(tile.center.x, baseY, tile.center.z);
            Dark(root, black, c + new Vector3(0f, rise * 0.5f, s * 0.5f), new Vector3(s + 0.4f, rise, 0.2f));
            Dark(root, black, c + new Vector3(0f, rise * 0.5f, -s * 0.5f), new Vector3(s + 0.4f, rise, 0.2f));
            Dark(root, black, c + new Vector3(s * 0.5f, rise * 0.5f, 0f), new Vector3(0.2f, rise, s + 0.4f));
            Dark(root, black, c + new Vector3(-s * 0.5f, rise * 0.5f, 0f), new Vector3(0.2f, rise, s + 0.4f));
            Dark(root, black, c + new Vector3(0f, rise, 0f), new Vector3(s + 0.4f, 0.2f, s + 0.4f));
        }

        static void Dark(Transform parent, Material mat, Vector3 pos, Vector3 scale)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Escuro";
            Collider col = g.GetComponent<Collider>();
            if (col != null) Destroy(col);
            g.transform.SetParent(parent, true);
            g.transform.position = pos;
            g.transform.localScale = scale;
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Light MakeShaft(Vector3 spot, float topY)
        {
            var go = new GameObject("FachoDoBuraco");
            go.transform.position = new Vector3(spot.x, spot.y + topY, spot.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(0.78f, 0.88f, 1f);
            l.intensity = 40f;
            l.range = 12f;
            l.spotAngle = 48f;
            l.innerSpotAngle = 20f;
            l.shadows = LightShadows.None;
            return l;
        }

        static IEnumerator FadeLight(Light l, float seconds)
        {
            if (l == null) yield break;
            float i0 = l.intensity, t = 0f;
            while (t < seconds && l != null)
            {
                t += Time.deltaTime;
                l.intensity = Mathf.Lerp(i0, 0f, t / seconds);
                yield return null;
            }
            if (l != null) Destroy(l.gameObject);
        }

        /// <summary>Pedaços de laje e pedra que despencam pelo buraco junto com o jogador.</summary>
        IEnumerator Rubble(Vector3 spot, float floorY, GameObject rockPrefab)
        {
            for (int i = 0; i < 16; i++)
            {
                StartCoroutine(Piece(spot, floorY, rockPrefab, i < 3));
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.03f, 0.12f));
            }
        }

        IEnumerator Piece(Vector3 spot, float floorY, GameObject rockPrefab, bool big)
        {
            GameObject g;
            if (big && rockPrefab != null)
            {
                g = Instantiate(rockPrefab);
                g.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.35f, 0.6f);
                foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            else
            {
                g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(g.GetComponent<Collider>());
                float s = big ? UnityEngine.Random.Range(0.4f, 0.7f) : UnityEngine.Random.Range(0.08f, 0.3f);
                g.transform.localScale = new Vector3(s, s * UnityEngine.Random.Range(0.4f, 0.9f), s * UnityEngine.Random.Range(0.7f, 1.2f));
                g.GetComponent<Renderer>().sharedMaterial = RotMaterials.Lit(new Color(0.55f, 0.48f, 0.37f));
            }
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // pedras grandes caem longe do jogador (não pode pousar na cara dele)
            Vector2 o = big ? UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(3f, 4.5f) : UnityEngine.Random.insideUnitCircle * 1.6f;
            g.transform.position = new Vector3(spot.x + o.x, floorY + UnityEngine.Random.Range(5.2f, 9f), spot.z + o.y);
            g.transform.rotation = UnityEngine.Random.rotation;
            Vector3 spin = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(90f, 400f);
            float v = UnityEngine.Random.Range(1f, 4f);
            float rest = g.transform.localScale.y * 0.4f;
            while (g != null && g.transform.position.y > floorY + rest)
            {
                v += 22f * Time.deltaTime;
                g.transform.position += Vector3.down * v * Time.deltaTime;
                g.transform.Rotate(spin * Time.deltaTime, Space.World);
                yield return null;
            }
            if (g == null) yield break;
            Vector3 p = g.transform.position;
            p.y = floorY + rest;
            g.transform.position = p;
            if (big) { Fx.ImpactDust(new Vector3(p.x, floorY, p.z), 0.9f); CameraShake.Kick(0.06f, 0.3f); }
            yield return new WaitForSeconds(big ? 5f : 6f);
            if (g != null) Destroy(g);
        }
    }
}
