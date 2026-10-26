using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>
    /// Coloca a máquina do tempo QUEBRADA e APAGADA dentro do templo da Fase 1 (02_Fase1_TemploPerdido), na frente do Marco:
    ///  - modelo do portal com 3 segmentos do anel removidos;
    ///  - sem o componente TimeMachine, sem aura e com a animação do modelo desligada: ela fica parada, nada a ativa;
    ///  - um colisor de caixa para o jogador não atravessar.
    /// O WakeUpSequence (criado pelo PhaseController ao iniciar a fase) faz o Marco acordar, andar até ela e examiná-la.
    /// Menu: Rift Of Time > 15. Fase 1: colocar a máquina quebrada no templo. Pode rodar de novo: refaz a máquina.
    /// </summary>
    public static class Fase1MachineSetup
    {
        const string TempleScene = "Assets/Scenes/FragmentsOfTime/02_Fase1_TemploPerdido.unity";
        const string GateModel = "Assets/Ancient Modular Tombs/Meshes/portal_gate_sci-fi.glb";
        const string MachineName = "Máquina do Tempo (quebrada)";
        const float GateScale = 0.5f;     // igual à máquina do prólogo
        const float PlayerStartZ = 9f;     // onde o Marco acorda (a sala vai de z=0 a z=30)
        const float MachineZ = 2.2f;       // a máquina, atrás dele, junto à parede de z=0

        // 3 segmentos consecutivos do anel e os "gêmeos" de brilho que ficam no mesmo lugar
        static readonly string[] MissingNodes =
        {
            "Object430", "Gsf_mat2-003_low002",
            "Object433", "Gsf_mat2-003_low003",
            "Object434", "Gsf_mat2-003_low004",
        };

        [MenuItem("Rift Of Time/15. Fase 1: colocar a máquina quebrada no templo")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var gate = AssetDatabase.LoadAssetAtPath<GameObject>(GateModel);
            if (gate == null) { EditorUtility.DisplayDialog("Máquina", "Não achei o modelo da máquina:\n" + GateModel + "\nVerifique o pacote glTFast e se o arquivo foi baixado (git lfs pull).", "OK"); return; }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TempleScene) == null) { EditorUtility.DisplayDialog("Máquina", "Não achei a cena:\n" + TempleScene, "OK"); return; }

            var scene = EditorSceneManager.OpenScene(TempleScene, OpenSceneMode.Single);
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) { EditorUtility.DisplayDialog("Máquina", "Não há Player na cena do templo.", "OK"); return; }

            var old = GameObject.Find(MachineName);
            if (old != null) Object.DestroyImmediate(old);
            Physics.SyncTransforms();

            // o Marco começa mais para dentro da sala e a máquina fica ATRÁS dele, perto da parede do fundo, virada para ele
            Vector3 fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 p = player.transform.position;
            p.z = PlayerStartZ;
            player.transform.position = p;
            Physics.SyncTransforms();
            Vector3 pos = new Vector3(p.x, 0f, MachineZ);
            if (Physics.Raycast(new Vector3(pos.x, 3f, pos.z), Vector3.down, out RaycastHit ground, 12f, ~0, QueryTriggerInteraction.Ignore) && !ground.transform.IsChildOf(player.transform))
                pos = ground.point;
            float dist = Vector3.Distance(p, pos);

            var root = new GameObject(MachineName);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);       // a "frente" (-Z) da máquina fica virada para o Marco
            var model = (GameObject)PrefabUtility.InstantiatePrefab(gate, root.transform);
            model.name = "Modelo";
            model.transform.localScale = Vector3.one * GateScale;

            // peças removidas
            int removed = 0;
            foreach (string n in MissingNodes)
            {
                var t = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
                if (t != null) { t.gameObject.SetActive(false); removed++; }
                else Debug.LogWarning("[Máquina] nó '" + n + "' não encontrado no modelo da máquina");
            }

            // parada: nenhuma animação, luz ou partícula
            foreach (var a in model.GetComponentsInChildren<Animation>(true)) { a.playAutomatically = false; a.enabled = false; }
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var l in model.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var ps in model.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);

            // apoia no chão (base levemente enterrada) e cria um colisor de caixa do tamanho do modelo
            // apoia no chão: só conta o que é sólido (ignora brilhos/transparentes, que ficam abaixo da base e a faziam flutuar)
            var rends = model.GetComponentsInChildren<Renderer>().Where(r => r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToArray();
            var solid = rends.Where(r => r.sharedMaterial != null && r.sharedMaterial.renderQueue < 3000 && r.bounds.size.y > 0.03f).ToArray();
            if (solid.Length == 0) solid = rends;
            if (rends.Length > 0)
            {
                float minY = solid.Min(r => r.bounds.min.y);
                root.transform.position += Vector3.up * (pos.y - minY - 0.02f);

                Bounds bb = solid[0].bounds;
                foreach (var r in solid) bb.Encapsulate(r.bounds);
                var box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(bb.center);
                Vector3 sz = root.transform.InverseTransformVector(bb.size);
                box.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Max(0.8f, Mathf.Abs(sz.z)));
            }

            int flipped = FixWalls();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            string msg = "Máquina quebrada colocada no templo, atrás do Marco, a " + dist.ToString("0.#") + " m dele (" + removed + "/" + MissingNodes.Length + " peças removidas).\nEla fica parada: sem animação e sem ativação.\nParedes viradas para dentro: " + flipped + ".\nCena salva.";
            Debug.Log("[Máquina] " + msg);
            EditorUtility.DisplayDialog("Máquina", msg, "OK");
        }

        // as paredes são de uma face só: as que apontam para fora ficam invisíveis por dentro da sala.
        // Vira 180° as peças cuja face (pela ordem dos triângulos) aponta para longe do centro da sala.
        static int FixWalls()
        {
            var temple = GameObject.Find("Templo Perdido");
            if (temple == null) return 0;
            Vector3 center = new Vector3(0f, 0f, 15f);
            int count = 0;
            foreach (var mf in temple.GetComponentsInChildren<MeshFilter>(true))
            {
                Transform wall = mf.transform;
                while (wall != null && wall != temple.transform && !wall.name.StartsWith("Wall")) wall = wall.parent;
                if (wall == null || wall == temple.transform || mf.sharedMesh == null) continue;
                if (mf.transform != wall && mf.transform.parent != wall && mf.transform.parent.parent != wall) continue;
                var mesh = mf.sharedMesh;
                var v = mesh.vertices; var t = mesh.triangles;
                Vector3 sum = Vector3.zero;
                for (int i = 0; i + 2 < t.Length; i += 3)
                    sum += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Vector3 n = mf.transform.TransformDirection(sum).normalized;
                var rend = mf.GetComponent<Renderer>();
                Vector3 c = rend != null ? rend.bounds.center : mf.transform.position;
                Vector3 toRoom = center - c; toRoom.y = 0f;
                if (Mathf.Abs(n.x) < 0.2f && Mathf.Abs(n.z) < 0.2f) continue;
                // a face visível é a que o cull mostra: normal (pela ordem dos triângulos) voltada para o centro da sala
                bool wallIsAlongX = Mathf.Abs(n.z) > Mathf.Abs(n.x);
                Vector3 axisToRoom = wallIsAlongX ? new Vector3(0f, 0f, Mathf.Sign(toRoom.z)) : new Vector3(Mathf.Sign(toRoom.x), 0f, 0f);
                if (Vector3.Dot(new Vector3(n.x, 0f, n.z).normalized, axisToRoom) < 0f)
                {
                    Vector3 pivot = new Vector3(c.x, wall.position.y, c.z);
                    wall.RotateAround(pivot, Vector3.up, 180f);
                    count++;
                }
            }
            return count;
        }
    }
}
