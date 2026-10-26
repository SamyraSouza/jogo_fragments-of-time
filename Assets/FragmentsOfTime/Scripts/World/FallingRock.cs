using System.Collections;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Pedra do colapso.</summary>
    public class FallingRock : MonoBehaviour
    {
        /// <summary>1 = pedra grande; menor = pedrinha.</summary>
        public float size = 1f;

        public void Run(Vector3 ground, float delay, float radius, float damage, Transform player, GameObject rockPrefab)
        {
            StartCoroutine(Routine(ground, delay, radius, damage, player, rockPrefab));
        }

        IEnumerator Routine(Vector3 ground, float delay, float radius, float damage, Transform player, GameObject rockPrefab)
        {
            float diameter = radius * 2.15f;

            // aviso: uma sombra clara no chão que vai escurecendo até a pedra cair
            Color wine = new Color(0.5f, 0.05f, 0.14f, 0f);
            Transform warn = Fx.FloorMark(ground, diameter, Fx.Soft(), wine, 0.03f);
            // bola vinho no chão mostrando onde a pedra vai cair
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.name = "AvisoDaPedra";
            ball.transform.SetParent(transform, false);
            ball.transform.position = ground + Vector3.up * 0.03f;
            ball.transform.localScale = new Vector3(0.6f, 0.02f, 0.6f);
            ball.GetComponent<Renderer>().sharedMaterial = RotMaterials.Unlit(new Color(0.38f, 0.04f, 0.1f));
            ball.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AudioDirector.Sfx("rumble", 0.22f);

            float ceiling = 4.7f;

            GameObject rock;
            if (rockPrefab != null)
            {
                rock = Instantiate(rockPrefab);
                rock.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f) * size;
                foreach (Collider c in rock.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            else
            {
                rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(rock.GetComponent<Collider>());
                rock.GetComponent<Renderer>().sharedMaterial = RotMaterials.Lit(new Color(0.55f, 0.47f, 0.36f));
                rock.transform.localScale = new Vector3(1.1f, 0.8f, 0.95f) * size;
            }
            rock.transform.rotation = Random.rotation;
            rock.transform.position = ground + Vector3.up * ceiling;
            Renderer[] rockRenderers = rock.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rockRenderers) r.enabled = false;

            float t = 0f;
            while (t < delay)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / delay);
                Fx.SetAlpha(warn, Mathf.Lerp(0.03f, 0.2f, k));
                float pr = 0.6f + 0.06f * Mathf.Sin(t * 9f);
                ball.transform.localScale = new Vector3(pr, 0.02f, pr);
                yield return null;
            }

            foreach (Renderer r in rockRenderers) r.enabled = true;
            Vector3 spin = Random.onUnitSphere * Random.Range(120f, 320f);
            float v = 3f;
            float startY = rock.transform.position.y;
            float rockHalf = 0.35f * size;
            while (rock != null && rock.transform.position.y > ground.y + rockHalf)
            {
                v += 38f * Time.deltaTime;
                rock.transform.position += Vector3.down * v * Time.deltaTime;
                rock.transform.Rotate(spin * Time.deltaTime, Space.World);
                float near = 1f - Mathf.Clamp01((rock.transform.position.y - ground.y) / Mathf.Max(0.01f, startY - ground.y));
                Fx.SetAlpha(warn, Mathf.Lerp(0.2f, 0.5f, near));
                yield return null;
            }

            if (rock != null)
            {
                Vector3 p = rock.transform.position;
                p.y = ground.y + rockHalf;
                rock.transform.position = p;
            }

            float dist = 99f;
            if (player != null)
            {
                Vector3 d = player.position - ground;
                d.y = 0f;
                dist = d.magnitude;
                if (dist < radius)
                {
                    PlayerHealth ph = player.GetComponent<PlayerHealth>();
                    if (ph != null) ph.TakeDamage(damage);
                }
            }
            float closeness = 1f - Mathf.Clamp01(dist / 14f);
            CameraShake.Kick(Mathf.Lerp(0.04f, 0.22f, closeness), Mathf.Lerp(0.3f, 0.65f, closeness));
            AudioDirector.Rec("Sfx/boulder_thud", Mathf.Lerp(0.45f, 1f, closeness));
            AudioDirector.Sfx("rumble", Mathf.Lerp(0.2f, 0.5f, closeness));

            Fx.Flash(this, ground + Vector3.up * 0.8f, new Color(1f, 0.6f, 0.25f), 7f, radius * 4f, 0.28f);
            StartCoroutine(Fx.Shockwave(ground, radius * 0.6f, radius * 3.2f, new Color(1f, 0.7f, 0.4f, 0.9f), 0.55f));

            Destroy(warn.gameObject);
            Destroy(ball);
            Transform crater = Fx.FloorMark(ground, radius * 1.7f, Fx.Soft(), new Color(0.02f, 0.015f, 0.01f, 0.7f), 0.022f);

            yield return new WaitForSeconds(1.5f);

            float sink = 0f;
            Vector3 rp = rock != null ? rock.transform.position : Vector3.zero;
            while (sink < 1f)
            {
                sink += Time.deltaTime / 1.2f;
                if (rock != null) rock.transform.position = rp + Vector3.down * (sink * 0.9f);
                if (crater != null) Fx.SetAlpha(crater, 0.7f * (1f - Mathf.Clamp01((sink - 0.3f) / 0.7f)));
                yield return null;
            }
            if (rock != null) Destroy(rock);
            if (crater != null) Destroy(crater.gameObject);
            Destroy(gameObject);
        }
    }
}
