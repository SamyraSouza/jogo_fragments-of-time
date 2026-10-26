using System;
using System.Collections;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Porta de pedra que se ergue sozinha (Fase 2, depois do fragmento). Quando o jogador atravessa o vão, chama o callback.</summary>
    public class ExitDoor : MonoBehaviour
    {
        float Width = 2.2f, Height = 3.1f;
        float rise = 3.3f, slabY0, zoneZ0 = -0.3f, zoneZ1 = 1.5f;

        Transform slab, player;
        Action onEnter;
        bool open, fired;

        /// <summary>Cria a porta com o centro da base em pos, virada para -Z (o jogador entra andando para +Z).</summary>
        public static ExitDoor Create(Vector3 pos, Action onEnter, float fitWidth = 0f)
        {
            var root = new GameObject("PortaDeSaida");
            root.transform.position = pos;
            var d = root.AddComponent<ExitDoor>();
            d.onEnter = onEnter;

            if (BuildPrefabDoor(root.transform, d, fitWidth)) return d;

            Material stone = RotMaterials.Lit(new Color(0.55f, 0.45f, 0.3f));
            stone.SetFloat("_Smoothness", 0.15f);
            Texture2D tex = Resources.Load<Texture2D>("RoT/Sandstone_A");
            if (tex != null)
            {
                stone.color = Color.white;
                stone.SetColor("_BaseColor", Color.white);
                stone.mainTexture = tex;
                stone.SetTexture("_BaseMap", tex);
                stone.mainTextureScale = new Vector2(1f, 2f);
            }
            float W = d.Width, H = d.Height;
            Material dark = RotMaterials.Unlit(new Color(0.01f, 0.01f, 0.015f));
            float px = W * 0.5f + 0.3f;
            ProcBuild.Primitive(root.transform, "PilarE", PrimitiveType.Cube, new Vector3(-px, H * 0.5f, 0f), new Vector3(0.6f, H, 0.7f), stone, true);
            ProcBuild.Primitive(root.transform, "PilarD", PrimitiveType.Cube, new Vector3(px, H * 0.5f, 0f), new Vector3(0.6f, H, 0.7f), stone, true);
            ProcBuild.Primitive(root.transform, "Verga", PrimitiveType.Cube, new Vector3(0f, H + 0.25f, 0f), new Vector3(W + 1.5f, 0.5f, 0.7f), stone);
            // fundo escuro dentro do vão (a passagem)
            ProcBuild.Primitive(root.transform, "Vao", PrimitiveType.Cube, new Vector3(0f, H * 0.5f, 0.3f), new Vector3(W, H, 0.05f), dark);
            d.slab = ProcBuild.Primitive(root.transform, "Laje", PrimitiveType.Cube, new Vector3(0f, H * 0.5f, -0.1f), new Vector3(W, H, 0.3f), stone, true);
            d.slabY0 = d.slab.position.y;
            d.rise = H + 0.2f;
            d.RecordZone();
            return d;
        }

        void RecordZone()
        {
            Bounds b = PackageAssets.BoundsOf(gameObject);
            zoneZ0 = b.min.z - transform.position.z - 0.3f;
            zoneZ1 = b.max.z - transform.position.z;
        }

        /// <summary>Encosta a porta na parede: a face de trás fica em wallZ (a porta fica virada para -Z).</summary>
        public void PushAgainst(float wallZ)
        {
            Bounds b = PackageAssets.BoundsOf(gameObject);
            transform.position += Vector3.forward * (wallZ - 0.02f - b.max.z);
        }

        /// <summary>Porta de pedra do pacote (Large_stone_door): a laje "Stone_door" sobe sozinha. Só no Editor; false se não der.</summary>
        static bool BuildPrefabDoor(Transform root, ExitDoor d, float fitWidth)
        {
            GameObject g = PackageAssets.Spawn("Prefabs/Large_stone_door.prefab", root);
            if (g == null) return false;
            Transform slab = null;
            foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) if (t.name == "Stone_door") { slab = t; break; }
            if (slab == null) { Destroy(g); return false; }
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            // escala para a porta ter ~3,6 m de altura; base no chão e centrada
            Bounds b = PackageAssets.BoundsOf(g);
            if (b.size.y < 0.01f) { Destroy(g); return false; }
            // na parede (ao longo de X) o lado largo do batente tem que ficar paralelo a ela
            if (fitWidth > 0.01f && b.size.z > b.size.x)
            {
                g.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                b = PackageAssets.BoundsOf(g);
            }
            g.transform.localScale = Vector3.one * (fitWidth > 0.01f ? fitWidth / Mathf.Max(b.size.x, b.size.z) : 3.6f / b.size.y);
            b = PackageAssets.BoundsOf(g);
            g.transform.position += root.position - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>();
            Bounds sb = PackageAssets.BoundsOf(slab.gameObject);
            d.slab = slab;
            d.slabY0 = slab.position.y;
            d.rise = sb.size.y + 0.2f;
            d.Height = sb.size.y;
            d.Width = Mathf.Max(1.2f, sb.size.x * 0.8f);
            d.RecordZone();
            return true;
        }

        public void Open() { if (!open) { open = true; StartCoroutine(Rise()); } }

        IEnumerator Rise()
        {
            AudioDirector.Sfx("rumble", 1f);
            CameraShake.Hold(0.04f);
            Vector3 from = slab.position, to = from + Vector3.up * rise;
            GameObject pg = GameObject.FindGameObjectWithTag("Player");
            SimpleFPSController look = pg != null ? pg.GetComponent<SimpleFPSController>() : null;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 3.2f)
            {
                // o jogador vira a cabeça para a porta enquanto ela abre
                if (look != null && t < 0.8f) look.LookAtPoint(transform.position + Vector3.up * 1.6f, Mathf.Clamp01(Time.deltaTime * 4f));
                slab.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            slab.position = to;
        }

        void Update()
        {
            if (!open || fired) return;
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                player = p.transform;
            }
            Vector3 d = transform.InverseTransformPoint(player.position);
            if (Mathf.Abs(d.x) < Width * 0.6f && d.z > 0f && slab.position.y > slabY0 + rise * 0.5f)      // assim que passa do centro da porta
            {
                fired = true;
                if (onEnter != null) onEnter();
            }
        }
    }
}
