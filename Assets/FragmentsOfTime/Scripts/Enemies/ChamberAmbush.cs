using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Fase 2, câmara do Faraó (o fim do labirinto, onde fica o fragmento): quando o jogador passa pela porta ao sul, as múmias que esperavam</summary>
    public class ChamberAmbush : MonoBehaviour
    {
        [Tooltip("Quantas múmias esperam na câmara.")]
        public int count = 4;
        [Tooltip("Altura do piso da câmara.")]
        public float floorY = 0.2f;
        public float mummyHealth = 100f;     // o soco tira 34: caem com 3 golpes
        public float mummySpeed = 1.7f;
        public float mummyDamage = 12f;

        static readonly Vector2[] Spots =
        {
            new Vector2(-6.5f, 2.5f), new Vector2(6.5f, 2.5f),
            new Vector2(0f, 4f), new Vector2(0f, 8f),
        };

        readonly List<MummyGuardian> mummies = new List<MummyGuardian>();
        Transform player;
        bool triggered, revealed;

        void Start()
        {
            if (fragment != null) fragment.gameObject.SetActive(false);
            int n = Mathf.Clamp(count, 1, Spots.Length);
            for (int i = 0; i < n; i++) mummies.Add(Spawn(i, Spots[i]));
        }

        /// <summary>Assenta a peça sobre a pedra do altar (não flutua). Chamar com o fragmento ainda ativo.</summary>
        public static void RestOnStone(FragmentPickup fragment, float floorY = 0.2f)
        {
            if (fragment != null)
            {
                // no lugar do altar, a mesma Pedra de Roseta onde fica o papiro
                GameObject altar = GameObject.Find("Altar");
                if (altar != null) altar.SetActive(false);
                Vector3 fp = fragment.transform.position;
                GameObject stone = MazePapyrus.BuildStone(null, new Vector3(fp.x, floorY, fp.z), 1.5f);
                if (stone == null) return;
                Renderer[] sr = stone.GetComponentsInChildren<Renderer>();
                float top = sr[0].bounds.max.y;
                foreach (Renderer r in sr) top = Mathf.Max(top, r.bounds.max.y);
                // segunda pedra por cima da primeira, um pouco girada, para ficar mais alto
                float h1 = top - floorY;
                GameObject stone2 = MazePapyrus.BuildStone(null, new Vector3(fp.x, top - h1 * 0.45f, fp.z), 1.2f);
                if (stone2 != null)
                {
                    sr = stone2.GetComponentsInChildren<Renderer>();
                    Bounds sb = sr[0].bounds;
                    foreach (Renderer r in sr) sb.Encapsulate(r.bounds);
                    stone2.transform.RotateAround(sb.center, Vector3.up, 28f);
                    sb = sr[0].bounds;
                    foreach (Renderer r in sr) sb.Encapsulate(r.bounds);
                    top = sb.max.y;
                }
                Renderer[] rs = fragment.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    float bottom = rs[0].bounds.min.y;
                    foreach (Renderer r in rs) bottom = Mathf.Min(bottom, r.bounds.min.y);
                    fragment.transform.position += Vector3.up * (top - bottom - 0.1f);   // a pedra é irregular: afunda um pouco para assentar
                }
            }
        }

        MummyGuardian Spawn(int index, Vector2 xz)
        {
            var go = new GameObject("Múmia da Câmara " + (index + 1));
            go.SetActive(false);
            go.transform.SetParent(transform, true);
            go.transform.position = new Vector3(xz.x, floorY, xz.y);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            go.AddComponent<CharacterController>();
            var m = go.AddComponent<MummyGuardian>();
            m.displayName = "Múmia";
            m.maxHealth = mummyHealth;
            m.moveSpeed = mummySpeed;
            m.attackDamage = mummyDamage;
            var mm = go.AddComponent<MazeMummy>();
            mm.useMazePath = false;
            mm.revealRoute = false;
            mm.height = 1.9f;       // tamanho de gente: a câmara é menor que o labirinto
            mm.minHealth = 0f;
            go.SetActive(true);
            return m;
        }

        /// <summary>O fragmento (que fica no altar) só aparece quando as múmias da câmara caem.</summary>
        public FragmentPickup fragment;

        void Update()
        {
            if (triggered && fragment != null && !revealed)
            {
                foreach (MummyGuardian m in mummies) if (m != null && !m.IsDead) return;
                revealed = true;
                fragment.gameObject.SetActive(true);
            }
            if (triggered) return;
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                player = p.transform;
            }
            Vector3 pos = player.position;
            if (pos.x > -10f && pos.x < 10f && pos.z > -3.5f && pos.z < 15f && pos.y < floorY + 4f)
            {
                triggered = true;
                StartCoroutine(Release());
            }
        }

        IEnumerator Release()
        {
            if (HUD.Instance != null) HUD.Instance.ShowSubtitle("As múmias da câmara despertaram! Elas vêm na sua direção.", 4f);
            foreach (MummyGuardian m in mummies)
            {
                if (m != null && !m.IsDead) m.StartChasing();
                yield return new WaitForSeconds(0.25f);
            }
        }
    }
}
