using System.Collections;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Coloca a adaga de Tutancâmon (Assets/Resources/Dagger) na mão direita dos braços em primeira pessoa.</summary>
    public class HeldDagger : MonoBehaviour
    {
        const string ModelResource = "Dagger/Dagger";
        const string TextureResource = "Dagger/DaggerTexture";

        [Tooltip("Comprimento da adaga, em metros.")]
        public float length = 0.42f;
        [Tooltip("Quanto do comprimento, a partir da ponta, fica o centro do cabo (0 = ponta, 1 = pomo). O modelo tem o cabo entre 0,55 e 0,85.")]
        [Range(0.5f, 0.9f)] public float gripAt = 0.72f;
        [Tooltip("Deslocamento extra, em metros, no espaço da mão (X = lado do polegar, Y = palma, Z = para onde os dedos apontam).")]
        public Vector3 handOffset = Vector3.zero;
        [Tooltip("Gira a adaga em volta do próprio eixo, em graus (se a lâmina sair de lado, tente 90, 180 ou 270).")]
        public float rollDegrees = 0f;

        GameObject holder;

        IEnumerator Start()
        {
            for (int i = 0; i < 4; i++) yield return null;

            GameObject prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null) { Debug.LogWarning("[HeldDagger] não achei Resources/" + ModelResource + " (Assets/Resources/Dagger/Dagger.obj)."); yield break; }

            Transform wrist = FindBone("R_wrist", "wrist");
            if (wrist == null) { Debug.LogWarning("[HeldDagger] osso R_wrist não encontrado nos braços."); yield break; }
            Transform middle = FindBone("R_middle1", "R_point1");

            Camera cam = GetComponentInParent<Camera>();
            if (cam == null) cam = Camera.main;
            Quaternion view = cam != null ? cam.transform.rotation : Quaternion.identity;
            Vector3 tipDirWorld = view * (Quaternion.Euler(PitchUp, YawIn, 0f) * Vector3.forward);
            Quaternion handRot = Quaternion.LookRotation(tipDirWorld, view * Vector3.up);

            holder = new GameObject("AdagaTutancamon");
            holder.transform.position = middle != null ? Vector3.Lerp(wrist.position, middle.position, 0.5f) : wrist.position;
            holder.transform.rotation = handRot;
            holder.transform.SetParent(wrist, true);

            GameObject model = Instantiate(prefab, holder.transform);
            model.name = "Modelo";
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);

            Material mat = RotMaterials.Lit(Color.white);
            Texture2D tex = Resources.Load<Texture2D>(TextureResource);
            if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; }
            RotMaterials.SetColor(mat, new Color(0.75f, 0.75f, 0.75f));
            mat.SetFloat("_Smoothness", 0.18f);
            mat.SetFloat("_Metallic", 0f);
            PunchAnimator.AddSelfFill(mat, 0.3f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                var ms = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                r.sharedMaterials = ms;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[HeldDagger] o modelo não tem malha."); yield break; }

            Bounds b = mf.sharedMesh.bounds;
            int axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : (b.size.y >= b.size.z ? 1 : 2);
            float tipSign = FindTipSign(mf.sharedMesh, axis, b);
            Vector3 tipAxis = Vector3.zero; tipAxis[axis] = tipSign;
            Debug.Log("[HeldDagger] eixo do comprimento=" + "XYZ"[axis] + ", ponta em " + (tipSign > 0 ? "+" : "-") + "; tamanho da malha=" + b.size.ToString("F3"));
            Vector3 widthAxis = axis == 1 ? Vector3.right : Vector3.up;

            Quaternion toHand = Quaternion.Inverse(Quaternion.LookRotation(tipAxis, widthAxis));
            toHand = Quaternion.AngleAxis(rollDegrees, Vector3.forward) * toHand;
            float s = length / Mathf.Max(0.0001f, b.size[axis]);
            Transform t = model.transform;
            t.localRotation = toHand;
            t.localScale = Vector3.one * s;

            Vector3 tipEnd = b.center + tipAxis * (b.size[axis] * 0.5f);
            Vector3 grip = tipEnd - tipAxis * (b.size[axis] * gripAt);
            t.localPosition = -(toHand * (grip * s)) + handOffset;

            StartCoroutine(Diagnose(cam, model));
        }

        /// <summary>Escreve no Console o que está pendurado na câmera e onde a adaga aparece na tela (ajuda a achar o que está errado).</summary>
        IEnumerator Diagnose(Camera cam, GameObject model)
        {
            yield return null; yield return null;
            if (cam == null) yield break;
            var sb = new System.Text.StringBuilder("[HeldDagger] renderers sob a câmera:\n");
            foreach (Renderer r in cam.GetComponentsInChildren<Renderer>(true))
            {
                string mats = "";
                foreach (Material m in r.sharedMaterials)
                    mats += (m == null ? "VAZIO" : m.name + " (" + (m.shader != null ? m.shader.name : "sem shader") + ", cor " + RotMaterials.GetColor(m) + ")") + "; ";
                Vector3 vp = cam.WorldToViewportPoint(r.bounds.center);
                sb.AppendLine("  " + PathOf(r.transform, cam.transform) + " ativo=" + r.gameObject.activeInHierarchy + " tela=(" + vp.x.ToString("F2") + "," + vp.y.ToString("F2") + ") dist=" + vp.z.ToString("F2") + " m | " + mats);
            }
            Debug.Log(sb.ToString());
        }

        static string PathOf(Transform t, Transform stop)
        {
            string p = t.name;
            while (t.parent != null && t.parent != stop) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        const float PitchUp = -28f, YawIn = -14f;

        Transform FindBone(params string[] names)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            foreach (string n in names)
                foreach (Transform tr in all)
                    if (tr.name == n) return tr;
            foreach (string n in names)
                foreach (Transform tr in all)
                    if (tr.name.ToLowerInvariant().Contains(n.ToLowerInvariant())) return tr;
            return null;
        }

        /// <summary>+1 ou -1: em qual extremidade do eixo está a ponta (a de seção mais fina).</summary>
        static float FindTipSign(Mesh mesh, int axis, Bounds b)
        {
            try
            {
                Vector3[] v = mesh.vertices;
                float lo = b.min[axis], len = b.size[axis], slice = len * 0.12f;
                int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                float min1 = float.MaxValue, max1 = float.MinValue, min2 = float.MaxValue, max2 = float.MinValue;
                foreach (Vector3 p in v)
                {
                    float x = p[axis];
                    if (x <= lo + slice) { Grow(ref minA, ref maxA, p[a1]); Grow(ref minB, ref maxB, p[a2]); }
                    else if (x >= lo + len - slice) { Grow(ref min1, ref max1, p[a1]); Grow(ref min2, ref max2, p[a2]); }
                }
                float lowArea = (maxA - minA) * (maxB - minB), highArea = (max1 - min1) * (max2 - min2);
                return lowArea < highArea ? -1f : 1f;
            }
            catch (System.Exception)
            {
                return -1f;
            }
        }

        static void Grow(ref float min, ref float max, float v)
        {
            if (v < min) min = v;
            if (v > max) max = v;
        }
    }
}
