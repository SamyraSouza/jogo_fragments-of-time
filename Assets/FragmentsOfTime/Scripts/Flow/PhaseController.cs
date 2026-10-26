using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RiftOfTime
{
    public enum PhaseKind { Fase1, Fase2, Fase3, Final }

    /// <summary>Orquestra uma fase de jogo (telas 02 a 05 do storyboard): prepara o jogador, o HUD, o áudio e</summary>
    public class PhaseController : MonoBehaviour
    {
        /// <summary>Chave (EditorPrefs, guardada por máquina, não no projeto) do atalho de teste "pular para a porta da câmara da Fase 2". Menu: Rift Of Time ▸ Teste.</summary>
        public const string TestKeyFase2AtDoor = "RoT_Fase2NaPorta";

        public PhaseKind phase;

        [Header("Referências da cena")]
        public MummyGuardian guardian;
        public FragmentPickup fragment;
        public PapyrusPickup papyrus;
        public TimeMachine machine;
        public CollapseController collapse;
        public TrapdoorFloor trapdoor;
        public AudioClip windClip;

        [Header("Segurança")]
        public float killY = -80f;

        GameManager gm;
        HUD hud;
        Transform playerT;
        PlayerHealth health;
        SimpleFPSController fps;
        bool ending, paused, skipping;

        void Start()
        {
            gm = GameManager.Ensure();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameManager.ApplyVolume();

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) { Debug.LogError("[RiftOfTime] Nenhum objeto com a tag Player na cena."); return; }
            playerT = p.transform;

            hud = HUD.Create();
            SetupPlayer();

            gm.FragmentsChanged += OnFragmentsChanged;
            hud.ResumeRequested += () => SetPaused(false);
            hud.MenuRequested += () => { SetPaused(false); gm.ToMenu(); };

#if UNITY_EDITOR
            // teste (desligado por padrão): com a opção ligada, a Fase 1 pula direto para a Fase 2 (que já começa na porta da câmara)
            if (phase == PhaseKind.Fase1 && UnityEditor.EditorPrefs.GetBool(TestKeyFase2AtDoor, false))
            {
                StartCoroutine(SkipToFase2());
                return;
            }
#endif
            GroundTorches();
            switch (phase)
            {
                case PhaseKind.Fase1: gm.SetFragments(0); BeginFase1(); break;
                case PhaseKind.Fase2: gm.SetFragments(1); BeginFase2(); break;
                case PhaseKind.Fase3: gm.SetFragments(2); BeginFase3(); break;
                case PhaseKind.Final: gm.SetFragments(3); BeginFinal(); break;
            }
            hud.SetFragments(gm.Fragments);
            hud.SetHealth(1f);
            gm.SaveCheckpoint(SceneManager.GetActiveScene().name);
        }

        void OnDestroy()
        {
            if (gm != null) gm.FragmentsChanged -= OnFragmentsChanged;
            CameraShake.Hold(0f);
        }

        void OnFragmentsChanged(int n) { if (hud != null) hud.SetFragments(n); }

        void SetupPlayer()
        {
            health = playerT.GetComponent<PlayerHealth>();
            if (health == null) health = playerT.gameObject.AddComponent<PlayerHealth>();
            if (playerT.GetComponent<PlayerCombat>() == null) playerT.gameObject.AddComponent<PlayerCombat>();
            if (playerT.GetComponent<PlayerInteractor>() == null) playerT.gameObject.AddComponent<PlayerInteractor>();
            fps = playerT.GetComponent<SimpleFPSController>();
            if (fps != null) { fps.walkSpeed = 2f; fps.runSpeed = 3.5f; }     // anda devagar como no prólogo (Shift só trota)

            Camera cam = playerT.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.gameObject.tag = "MainCamera";
                if (RenderSettings.skybox == null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = RenderSettings.fog ? RenderSettings.fogColor : Color.black;
                }
                if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }

            health.Changed += (cur, max) => hud.SetHealth(cur / max);
            health.Damaged += amount =>
            {
                hud.FlashDamage();
                if (health.Current <= 0f) return;          // o golpe que mata não faz barulho nem treme
                CameraShake.Kick(0.1f, 0.3f);
            };
            health.Died += OnPlayerDied;
        }

        void Update()
        {
            if (hud == null) return;
            if (playerT == null || ending) { HandlePause(); return; }
            if (playerT.position.y < killY) { ending = true; gm.Reload(); return; }
            HandlePause();
        }

        /// <summary>Atalhos de teste: F1 a F4 pulam para a fase; F5 (só na Fase 2) leva o jogador ao fim do labirinto, diante da porta da câmara do Faraó.</summary>
        void HandleCheats()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || paused || skipping) return;

            string target = null; string title = null, sub = null; Color fade = Color.black;
            if (kb.f1Key.wasPressedThisFrame) { target = SceneNames.Fase1; }
            else if (kb.f2Key.wasPressedThisFrame) { target = SceneNames.Fase2; title = "FASE 2"; sub = "As Ruínas do Deserto"; }
            else if (kb.f3Key.wasPressedThisFrame) { target = SceneNames.Fase3; title = "FASE 3"; sub = "A Câmara do Tempo"; }
            else if (kb.f4Key.wasPressedThisFrame) { target = SceneNames.Final; fade = Color.white; }

            if (target != null)
            {
                skipping = true;
                gm.GoTo(target, fade, title, sub);
                return;
            }

            if (phase == PhaseKind.Fase2 && kb.f5Key.wasPressedThisFrame) TeleportToChamberDoor();
        }

        void TeleportToChamberDoor()
        {
            if (playerT == null) return;
            Vector3 pos = new Vector3(2.5f, 0.35f, -7.5f);
            SimpleFPSController ctrl = playerT.GetComponent<SimpleFPSController>();
            if (ctrl != null) { ctrl.TeleportTo(pos, 0f); return; }
            playerT.SetPositionAndRotation(pos, Quaternion.identity);
        }

#if UNITY_EDITOR
        IEnumerator SkipToFase2()
        {
            // a troca de cena que nos trouxe até aqui ainda está no fade de entrada, e GoTo ignora pedidos nesse período
            while (gm.IsLoading) yield return null;
            gm.GoTo(SceneNames.Fase2, Color.black);
        }

        IEnumerator StartAtDoorInEditor()
        {
            yield return null;
            yield return null;
            TeleportToChamberDoor();
        }
#endif

        void HandlePause()
        {
            HandleCheats();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !ending && !hud.IsReading)
                SetPaused(!paused);
        }

        void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            AudioListener.pause = value;
            if (fps != null) fps.enabled = !value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            hud.ShowPause(value);
        }

        void OnPlayerDied()
        {
            if (ending) return;
            ending = true;
            StartCoroutine(DeathSequence());
        }

        // sem mensagem: ele cai de joelhos, tomba para a frente e aparece o GAME OVER
        IEnumerator DeathSequence()
        {
            if (fps != null) fps.enabled = false;
            var combat = playerT.GetComponent<PlayerCombat>(); if (combat != null) combat.enabled = false;
            var inter = playerT.GetComponent<PlayerInteractor>(); if (inter != null) inter.enabled = false;
            var rb = playerT.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
            foreach (string key in new[] { "music", "ambient", "wind", "drums" }) AudioDirector.Instance.StopLoop(key);
            hud.SetGameplayVisible(false);
            hud.ClearLowHealth();

            Camera cam = playerT.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.03f);
                // uma luz fraca acompanha a câmera: no escuro da fase a queda continua visível
                var glow = new GameObject("LuzQueda").AddComponent<Light>();
                glow.transform.SetParent(cam.transform, false);
                glow.type = LightType.Point; glow.range = 6f; glow.intensity = 1.2f; glow.color = new Color(1f, 0.85f, 0.65f);
                glow.shadows = LightShadows.None;
                foreach (var sway in playerT.GetComponentsInChildren<CameraSway>()) sway.enabled = false; foreach (var pre in playerT.GetComponentsInChildren<CameraSwayPre>()) pre.enabled = false;
                foreach (var bob in playerT.GetComponentsInChildren<ArmsWalkBob>()) bob.enabled = false;
                Transform arms = cam.transform.Find("Arms");
                if (arms != null) arms.gameObject.SetActive(false);
                Vector3 p0 = cam.transform.localPosition;

                // 1) os joelhos cedem (sem som)
                for (float t = 0f; t < 0.75f; t += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / 0.75f);
                    cam.transform.localPosition = p0 + new Vector3(0f, -0.3f * p0.y * k, 0.05f * k);
                    cam.transform.localRotation = Quaternion.Euler(16f * k, 0f, 5f * k);
                    yield return null;
                }
                yield return new WaitForSeconds(0.25f);

                // 2) tomba para o lado (sem tremer); o barulho da queda toca quando bate no chão
                Vector3 p1 = cam.transform.localPosition;
                for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                {
                    float k = (t / 0.8f) * (t / 0.8f);
                    cam.transform.localPosition = p1 + new Vector3(0.45f * k, -0.25f * p0.y * k, 0.05f * k);
                    cam.transform.localRotation = Quaternion.Euler(Mathf.Lerp(16f, 8f, k), 0f, Mathf.Lerp(5f, 75f, k));
                    yield return null;
                }
                AudioDirector.Sfx("fall", 1f);   // o corpo bate no chão
            }

            yield return new WaitForSeconds(1.1f);
            hud.ShowGameOver(() => gm.Reload(), () => gm.ToMenu());
        }

        IEnumerator ReloadAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            gm.Reload();
        }

        void HookGuardian(System.Action<MummyGuardian> onDied)
        {
            guardian.Awakened += g =>
            {
                hud.ShowBoss(g.displayName, 1f);
                AudioDirector.Instance.Loop("drums", "drums", 0.6f);
            };
            guardian.HealthChanged += hud.SetBoss;
            guardian.Died += g =>
            {
                hud.HideBoss();
                AudioDirector.Instance.StopLoop("drums");
                if (onDied != null) onDied(g);
            };
        }

        void PlayPhaseMusic()
        {
            AudioClip music = Resources.Load<AudioClip>("Music/Prince of Egypt");
            if (music != null) AudioDirector.Instance.Loop("music", music, 0.5f);
        }

        /// <summary>As paredes do templo são de uma face só e ficavam invisíveis por dentro: desliga o descarte da face de trás.</summary>
        static void MakeWallsDoubleSided()
        {
            GameObject temple = GameObject.Find("Templo Perdido");
            if (temple == null) return;
            foreach (Renderer r in temple.GetComponentsInChildren<Renderer>(true))
            {
                Transform t = r.transform;
                bool wall = false;
                for (; t != null && t != temple.transform; t = t.parent) if (t.name.StartsWith("Wall")) { wall = true; break; }
                if (!wall) continue;
                foreach (Material m in r.materials) if (m != null && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            }
        }

        /// <summary>A máquina quebrada fica no chão, atrás do Marco (que acorda mais para dentro da sala), virada para ele.</summary>
        void PlaceBrokenMachine()
        {
            GameObject m = GameObject.Find("Máquina do Tempo (quebrada)");
            if (m == null || playerT == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(playerT.forward, Vector3.up);
            fwd = fwd.sqrMagnitude < 0.01f ? Vector3.forward : fwd.normalized;
            Vector3 p = playerT.position;
            if (p.z < 8f && fps != null) { p = new Vector3(p.x, p.y, 9f); fps.TeleportTo(p, playerT.eulerAngles.y); }
            // altura do chão: o ponto onde o Marco pisa (a raiz da máquina fica na base dela)
            float floorY = 0f;
            RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (RaycastHit h in hits)
                if (!h.transform.IsChildOf(playerT) && !h.transform.IsChildOf(m.transform) && h.distance < best) { best = h.distance; floorY = h.point.y; }
            m.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            m.transform.position = new Vector3(p.x, floorY, 2.2f);
            // o modelo traz cenário de deserto/neve/grama (cacto, tufos) junto da máquina: some, fica só a máquina
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    string mn = mat.name.ToLowerInvariant();
                    if (mn.Contains("desert") || mn.Contains("winter") || mn.Contains("grass")) { r.gameObject.SetActive(false); break; }
                }
            }
        }

        /// <summary>Troca o pedestal por duas Pedras de Roseta empilhadas (a segunda um pouco girada) e assenta o objeto por cima.</summary>
        static void PutOnTwoStones(Transform item)
        {
            Vector3 p = item.position;
            float top = BuildTwoStones(p);
            if (top == float.MinValue) return;
            Transform ped = item.Find("Pedestal");
            if (ped != null) ped.gameObject.SetActive(false);
            float delta = top - (p.y + 1.0f);      // o topo do pedestal antigo ficava a 1 m
            foreach (Transform c in item)
                if (c != ped) c.position += Vector3.up * delta;
            foreach (Collider col in item.GetComponents<Collider>())
            {
                var box = col as BoxCollider; if (box != null) box.center += Vector3.up * delta;
                var cap = col as CapsuleCollider; if (cap != null) cap.center += Vector3.up * delta;
                var sph = col as SphereCollider; if (sph != null) sph.center += Vector3.up * delta;
            }
        }

        /// <summary>Fragmento sobre as duas pedras, no chão logo abaixo dele.</summary>
        static void PutFragmentOnTwoStones(Transform item)
        {
            Vector3 p = item.position;
            float floorY = p.y;
            RaycastHit[] hits = Physics.RaycastAll(p, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (RaycastHit h in hits)
                if (!h.transform.IsChildOf(item) && h.distance < best) { best = h.distance; floorY = h.point.y; }
            float top = BuildTwoStones(new Vector3(p.x, floorY, p.z));
            if (top == float.MinValue) return;
            item.position = new Vector3(p.x, top, p.z);
            // apoia a base do modelo no topo da pedra (um fiozinho para dentro, já que o topo é irregular)
            Renderer[] fr = item.GetComponentsInChildren<Renderer>();
            float minY = float.MaxValue;
            foreach (Renderer r in fr)
                if (!(r is ParticleSystemRenderer)) minY = Mathf.Min(minY, r.bounds.min.y);
            if (minY != float.MaxValue) item.position += Vector3.up * (top - 0.14f - minY);
        }

        /// <summary>Tira o altar inteiro (degraus e pedestal): só ficam as pedras no chão.</summary>
        static void FlattenAltar()
        {
            GameObject altar = GameObject.Find("Altar");
            if (altar != null) altar.SetActive(false);
        }

        /// <summary>Empilha duas Pedras de Roseta (a segunda um pouco girada) e devolve a altura do topo (float.MinValue se faltar o modelo).</summary>
        static float BuildTwoStones(Vector3 p)
        {
            GameObject s1 = MazePapyrus.BuildStone(null, p, 1.5f);
            if (s1 == null) return float.MinValue;
            Renderer[] sr = s1.GetComponentsInChildren<Renderer>();
            float top = sr[0].bounds.max.y;
            foreach (Renderer r in sr) top = Mathf.Max(top, r.bounds.max.y);
            float h1 = top - p.y;
            GameObject s2 = MazePapyrus.BuildStone(null, new Vector3(p.x, top - h1 * 0.45f, p.z), 1.2f);
            if (s2 != null)
            {
                sr = s2.GetComponentsInChildren<Renderer>();
                Bounds sb = sr[0].bounds;
                foreach (Renderer r in sr) sb.Encapsulate(r.bounds);
                s2.transform.RotateAround(sb.center, Vector3.up, 28f);
                top = sr[0].bounds.max.y;
                foreach (Renderer r in sr) top = Mathf.Max(top, r.bounds.max.y);
            }
            return top;
        }

        /// <summary>Parede esquerda da sala (x = -10, vista de dentro) coberta de hieróglifos.</summary>
        WallGlyphSwitch wallSwitch;

        /// <summary>A adaga fica no chão, na frente do altar do fragmento.</summary>
        void BuildDaggerPickup()
        {
            Vector3 p = new Vector3(0f, 3f, 23.6f);
            RaycastHit[] hits = Physics.RaycastAll(p, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            float floorY = 0f, best = float.MaxValue;
            foreach (RaycastHit h in hits)
                if (h.distance < best) { best = h.distance; floorY = h.point.y; }
            DaggerPickup.Create(new Vector3(p.x, floorY + 0.03f, p.z));
        }

        void BuildHieroglyphWall()
        {
            Texture2D tex = Resources.Load<Texture2D>("Papiro/parede_hieroglifos");
            if (tex == null) return;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Parede de Hieróglifos";
            Destroy(q.GetComponent<Collider>());
            q.transform.position = new Vector3(-9.7f, 2.0f, 15f);
            q.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            q.transform.localScale = new Vector3(18f, 18f * tex.height / tex.width, 1f);
            Material m = RotMaterials.Lit(Color.white);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            q.GetComponent<Renderer>().sharedMaterial = m;
        }

        IEnumerator PapyrusTranslationLines()
        {
            hud.ShowDialogue("Hieróglifos... não entendo nada disso. Ainda bem que eu trouxe a minha máquina de tradução.", 6f);
            yield return new WaitForSeconds(6.5f);
            hud.ShowDialogue("É só ler o código com o celular, e o papiro vai aparecer em realidade aumentada.", 6f);
        }

        void BeginFase1()
        {
            MakeWallsDoubleSided();
            EnsureWallMeshColliders();
            BuildRoomLimits();
            PlaceBrokenMachine();
            PlayPhaseMusic();
            AudioDirector.Instance.Loop("ambient", "ambient", 0.3f);
            PinkGuard.Fix("fase 1");
            // sem o título "Fase 1": o Marco acorda no chão do templo, anda até a máquina quebrada e a examina (WakeUpSequence)
            if (GetComponent<WakeUpSequence>() == null) gameObject.AddComponent<WakeUpSequence>().Begin(hud, playerT, fps);

            // sem Faraó/Guardiã: o fragmento já está no altar e o papiro fica sobre as duas pedras
            if (guardian != null) guardian.gameObject.SetActive(false);
            fragment.guardian = null;
            fragment.gameObject.SetActive(true);
            fragment.autoCollect = true;
            FlattenAltar();
            PutFragmentOnTwoStones(fragment.transform);

            if (papyrus != null)
            {
                PutOnTwoStones(papyrus.transform);
                papyrus.qrImage = Resources.Load<Texture2D>("Papiro/qr_ar");
                if (papyrus.qrImage != null) papyrus.Read += () => StartCoroutine(PapyrusTranslationLines());
            }
            BuildHieroglyphWall();
            BuildDaggerPickup();
            wallSwitch = WallGlyphSwitch.Create(new Vector3(-9.5f, 2.0f, 10f));
            wallSwitch.Pressed += () => StartCoroutine(Fase1OpenDoor());

            fragment.Collected += f => StartCoroutine(Fase1Complete());
        }

        IEnumerator Fase1Complete()
        {
            hud.ShowDialogue("Achei você! Faltam só mais duas.", 4f);
            yield return new WaitForSeconds(5f);
            hud.ShowDialogue("Mas... não tem nenhuma passagem aqui.", 4.5f);
            yield return new WaitForSeconds(5.5f);
            hud.ShowDialogue("Esses desenhos na parede... talvez algum deles seja um mecanismo.", 5f);
            if (wallSwitch != null) wallSwitch.Arm();
        }

        /// <summary>Uma porta de pedra se abre na parede do fundo; ao atravessá-la, vai para a Fase 2 (sem cartão de título).</summary>
        IEnumerator Fase1OpenDoor()
        {
            // o jogador fica parado e a câmera vira devagar para a parede do fundo, onde a porta vai se abrir
            float walk = 0f, run = 0f, lookSp = 0f;
            if (fps != null) { walk = fps.walkSpeed; run = fps.runSpeed; lookSp = fps.lookSpeed; fps.walkSpeed = 0f; fps.runSpeed = 0f; fps.lookSpeed = 0f; }
            Vector3 doorPoint = new Vector3(2.5f, 1.8f, 30f);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                if (fps != null) fps.LookAtPoint(doorPoint, Mathf.Clamp01(Time.deltaTime * 3f));
                yield return null;
            }
            float wallZ = 30f;
            RaycastHit wh;
            GameObject wall = null;
            if (Physics.Raycast(new Vector3(2.5f, 1.5f, 22f), Vector3.forward, out wh, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                wallZ = wh.point.z;
                for (Transform t = wh.collider.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Wall_5x5m")) { wall = t.gameObject; break; }
            }
            System.Action enter = () =>
            {
                if (ending) return;
                ending = true;
                gm.GoTo(SceneNames.Fase2, Color.black);
            };

            ExitDoor door;
            if (wall != null)
            {
                Bounds wb = PackageAssets.BoundsOf(wall);
                GameObject filler = Instantiate(wall, wall.transform.parent);
                door = ExitDoor.Create(new Vector3(wb.center.x, wb.min.y, wb.center.z), enter, Mathf.Max(wb.size.x, wb.size.z));
                wall.SetActive(false);
                float doorTop = PackageAssets.BoundsOf(door.gameObject).max.y;
                filler.transform.position += Vector3.up * (doorTop - 0.2f - wb.min.y);
            }
            else
            {
                door = ExitDoor.Create(new Vector3(2.5f, 0.2f, wallZ), enter);
                door.PushAgainst(wallZ);
            }
            // atrás da porta, preto total (sem o céu aparecendo pela abertura)
            Transform dark = ProcBuild.Cube(null, "Escuro da Porta", new Vector3(2.5f, 2.5f, wallZ + 0.9f), new Vector3(12f, 7f, 0.2f), RotMaterials.Lit(Color.black));
            foreach (Collider dc in dark.GetComponentsInChildren<Collider>()) Destroy(dc);
            door.Open();
            for (float t = 0f; t < 5.2f; t += Time.deltaTime)
            {
                if (fps != null) fps.LookAtPoint(doorPoint, Mathf.Clamp01(Time.deltaTime * 3f));
                yield return null;
            }
            if (fps != null) { fps.walkSpeed = walk; fps.runSpeed = run; fps.lookSpeed = lookSp; }
        }

        // escurece a Fase 2 para as tochas e a lanterna fazerem sentido: quase sem luz ambiente, sem sol e com névoa preta
        void DarkenScene()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = RenderSettings.ambientLight * 0.18f;
            RenderSettings.ambientIntensity = 0.5f;
            RenderSettings.reflectionIntensity = 0.1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogDensity = 0.035f;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { l.intensity *= 0.06f; l.shadows = LightShadows.None; }     // a 6% de intensidade a sombra do sol não aparece, só custa
            Camera cam = playerT != null ? playerT.GetComponentInChildren<Camera>() : null;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }
        }

        /// <summary>Limites invisíveis atrás das paredes laterais e do fundo da sala (x -10..10, z 0..30): o jogador não sai da sala nem se a parede falhar.</summary>
        static void BuildRoomLimits()
        {
            var root = new GameObject("Limites da Sala").transform;
            AddLimit(root, new Vector3(-10.25f, 3f, 15f), new Vector3(0.5f, 6f, 32f));
            AddLimit(root, new Vector3(10.25f, 3f, 15f), new Vector3(0.5f, 6f, 32f));
            AddLimit(root, new Vector3(0f, 3f, -0.25f), new Vector3(21f, 6f, 0.5f));
        }

        static void AddLimit(Transform root, Vector3 pos, Vector3 size)
        {
            var g = new GameObject("Limite");
            g.transform.SetParent(root, false);
            g.transform.position = pos;
            g.AddComponent<BoxCollider>().size = size;
        }

        // paredes com colisor de verdade (malha): sem isso a múmia e o jogador atravessam peças que só têm uma caixa pequena ou nada
        void EnsureWallMeshColliders()
        {
            int added = 0;
            foreach (MeshFilter mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshCollider>() != null) continue;
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled) continue;
                string n = (mf.name + " " + (mf.transform.parent != null ? mf.transform.parent.name : "")).ToLowerInvariant();
                if (!(n.Contains("wall") || n.Contains("parede") || n.Contains("door") || n.Contains("arch") || n.Contains("corridor") || n.Contains("pyramid"))) continue;
                if (n.Contains("horizon")) continue;
                mf.gameObject.AddComponent<MeshCollider>();
                added++;
            }
            if (added > 0) Debug.Log("[Paredes] colisor de malha adicionado em " + added + " peça(s)");
        }

        void BeginFase2()
        {
            PinkGuard.Fix("fase 2");
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(TestKeyFase2AtDoor, false)) StartCoroutine(StartAtDoorInEditor());
#endif
            DarkenScene();
            EnsureWallMeshColliders();
            if (windClip != null) AudioDirector.Instance.Loop("wind", windClip, 0.22f);
            if (guardian != null) guardian.gameObject.SetActive(false);       // sem Faraó: só as 4 múmias da câmara
            ChamberAmbush ambush = FindFirstObjectByType<ChamberAmbush>();
            if (ambush == null) ambush = gameObject.AddComponent<ChamberAmbush>();
            ambush.fragment = fragment;
            ChamberAmbush.RestOnStone(fragment);
            fragment.gameObject.SetActive(false);       // aparece quando as múmias da câmara caem
            AudioDirector.Instance.Loop("ambient", "ambient", 0.1f);
            hud.ShowSubtitle("Fase 2 — Dentro da pirâmide. Ache o papiro com o mapa, cuidado com as múmias e chegue à câmara do Faraó.", 7f);

            fragment.guardian = null;
            fragment.autoCollect = false;
            fragment.seenLine = "Achei você... falta só mais um.";
            fragment.Collected += f => StartCoroutine(Fase2Complete());
        }

        /// <summary>Todas as tochas ficam apoiadas no chão (y = 0).</summary>
        void GroundTorches()
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Torchfire")) continue;
                float bottom = float.MaxValue;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                    if (!(r is ParticleSystemRenderer)) bottom = Mathf.Min(bottom, r.bounds.min.y);
                if (bottom == float.MaxValue) continue;
                t.position += Vector3.up * (0f - bottom);
            }
        }

        /// <summary>Tochas no chão e luz só onde há tocha.</summary>
        void FixTorchesAndLights()
        {
            var torches = new List<Transform>();
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Torchfire")) torches.Add(t);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point || l.name != "PointLight") continue;
                bool near = false;
                foreach (Transform t in torches) if (Vector3.Distance(l.transform.position, t.position) < 5f) { near = true; break; }
                if (!near) l.gameObject.SetActive(false);
            }
            foreach (Transform t in torches)
            {
                float bottom = float.MaxValue;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                    if (!(r is ParticleSystemRenderer)) bottom = Mathf.Min(bottom, r.bounds.min.y);
                if (bottom == float.MaxValue) continue;
                t.position += Vector3.up * (0f - bottom);
            }
        }

        /// <summary>Todas as entradas (painéis de porta) viram o arco do pacote, encaixado na largura da parede. Só no Editor.</summary>
        void FixFirstEntrance()
        {
            var olds = new List<GameObject>();
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Wall_5x5m_door")) olds.Add(t.gameObject);
            foreach (GameObject old in olds)
            {
                Bounds ob = PackageAssets.BoundsOf(old);
                GameObject arch = PackageAssets.Spawn("Prefabs/Large_door_arch.prefab", null);
                if (arch == null) return;
                Bounds b = PackageAssets.BoundsOf(arch);
                if (b.size.y < 0.01f) { Destroy(arch); return; }
                if ((b.size.x >= b.size.z) != (ob.size.x >= ob.size.z)) arch.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                b = PackageAssets.BoundsOf(arch);
                float wallLong = Mathf.Max(ob.size.x, ob.size.z), archLong = Mathf.Max(b.size.x, b.size.z);
                arch.transform.localScale = Vector3.one * (wallLong / archLong);
                b = PackageAssets.BoundsOf(arch);
                arch.transform.position += new Vector3(ob.center.x - b.center.x, ob.min.y - b.min.y, ob.center.z - b.center.z);
                foreach (MeshFilter mf in arch.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>();
                old.SetActive(false);
            }
        }

        IEnumerator Fase2Complete()
        {
            // uma porta de pedra se abre na parede do fundo da câmara; ao atravessá-la, vai para a Fase 3 (sem cartão de título)
            yield return new WaitForSeconds(1.2f);
            float wallZ = 15f;
            RaycastHit wh;
            GameObject wall = null;
            if (Physics.Raycast(new Vector3(6f, 1.5f, 9f), Vector3.forward, out wh, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                wallZ = wh.point.z;
                for (Transform t = wh.collider.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Wall_5x5m")) { wall = t.gameObject; break; }
            }
            System.Action enter = () =>
            {
                if (ending) return;
                ending = true;
                gm.GoTo(SceneNames.Fase3, Color.black);
            };

            ExitDoor door;
            if (wall != null)
            {
                // a porta ocupa o lugar da peça de parede (encaixada nela, não na frente dela)
                Bounds wb = PackageAssets.BoundsOf(wall);
                GameObject filler = Instantiate(wall, wall.transform.parent);
                door = ExitDoor.Create(new Vector3(wb.center.x, wb.min.y, wb.center.z), enter, Mathf.Max(wb.size.x, wb.size.z));
                wall.SetActive(false);
                // o resto da parede acima da porta
                float doorTop = PackageAssets.BoundsOf(door.gameObject).max.y;
                filler.transform.position += Vector3.up * (doorTop - 0.2f - wb.min.y);
            }
            else
            {
                door = ExitDoor.Create(new Vector3(6f, 0.2f, wallZ), enter);
                door.PushAgainst(wallZ);
            }
            door.Open();
        }

        void BeginFase3()
        {
            PinkGuard.Fix("fase 3");
            AudioDirector.Instance.Loop("ambient", "ambient", 0.25f);     // sem música de fundo na Fase 3
            StartCoroutine(Fase3Arrival());
            FixTorchesAndLights();
            FixFirstEntrance();
            ChamberAmbush.RestOnStone(fragment, 0f);
            BladeTrap.SpawnCorridor();

            machine.SetRebuilt(false);
            machine.Break();
            machine.canInteract = false;
            machine.Activated += m => StartCoroutine(Fase3Complete());

            fragment.autoCollect = false;
            fragment.locked = true;
            fragment.Collected += OnLastFragment;
            collapse.TimeUp += OnCollapseTimeUp;
            StartCoroutine(BossEncounter());
        }

        /// <summary>Fala do chefe (ruge) ou do jogador (voz dele); a câmera de cena continua girando em volta do chefe.</summary>
        IEnumerator BossTalk(FalconBoss boss, string line, float seconds, bool bossSpeaks)
        {
            hud.ShowDialogue(line, seconds, !bossSpeaks);
            if (bossSpeaks) boss.Growl(seconds);
            for (float t = 0f; t < seconds + 0.4f; t += Time.deltaTime)
            {
                if (fps != null) fps.LookAtPoint(boss.transform.position + Vector3.up * 2.2f, Mathf.Clamp01(Time.deltaTime * 4f));
                yield return null;
            }
        }

        /// <summary>Falcão Guardião: parado na sala do fragmento, conversa, persegue por 3 minutos, é esmagado por uma pedra.</summary>
        IEnumerator BossEncounter()
        {
            FalconBoss boss = FalconBoss.Spawn(new Vector3(68f, 0f, 15f), new Vector3(50f, 0f, 12.5f), playerT);
            PlayerHealth ph = playerT.GetComponent<PlayerHealth>();
            // a lanterna fica acesa e vai piscando cada vez mais conforme ele chega perto do chefe; apaga quando chega
            Light beam = null;
            float baseIntensity = 0f;
            while (playerT.position.x < 54f)
            {
                if (beam == null)
                {
                    foreach (Light l in playerT.GetComponentsInChildren<Light>(true))
                        if (l.name.ToLower().Contains("flashlight")) { beam = l; baseIntensity = l.intensity; break; }
                }
                else
                {
                    float k = Mathf.InverseLerp(26f, 54f, playerT.position.x);
                    if (k <= 0f) beam.intensity = baseIntensity;
                    else
                    {
                        float noise = Mathf.PerlinNoise(Time.time * Mathf.Lerp(7f, 22f, k), 3.3f);
                        bool dark = noise < Mathf.Lerp(0.05f, 0.62f, k * k);
                        beam.intensity = dark ? baseIntensity * 0.04f : baseIntensity * Mathf.Lerp(1f, 0.65f, k);
                    }
                }
                yield return null;
            }
            if (beam != null) beam.enabled = false;

            float walk = 0f, run = 0f;
            if (fps != null) { walk = fps.walkSpeed; run = fps.runSpeed; fps.walkSpeed = 0f; fps.runSpeed = 0f; }
            boss.FaceInstant(playerT.position);
            Camera pcam = fps != null ? fps.playerCamera : null;
            CinematicCam cine = pcam != null ? CinematicCam.On(pcam) : null;
            if (cine != null) cine.Begin(boss.transform, 2.9f, 1.7f, 2.3f, 26f, 4f);
            string n = FalconBoss.DisplayName + ": ";
            yield return BossTalk(boss, n + "Pare aí, mortal. Esta câmara é propriedade minha.", 3.8f, true);
            yield return BossTalk(boss, "Eu só preciso do fragmento que está atrás de você.", 3.5f, false);
            yield return BossTalk(boss, n + "Esse fragmento é meu há mil anos. Ninguém entra nas minhas terras e sai vivo.", 4.5f, true);
            yield return BossTalk(boss, "Eu não quero lutar. Só me deixe passar.", 3.2f, false);
            yield return BossTalk(boss, n + "Então prove que merece. Sobreviva às minhas garras!", 3.8f, true);
            if (cine != null) cine.End();
            if (fps != null) { fps.walkSpeed = walk; fps.runSpeed = run; }

            PlayerDodge dodge = playerT.gameObject.AddComponent<PlayerDodge>();
            AudioDirector.Instance.StopLoop("ambient");
            AudioDirector.Instance.Loop("tense", "tense", 0.6f);
            boss.Chasing = true;

            float left = 120f;
#if UNITY_EDITOR
            left = 60f;     // teste no Editor: 1 minuto (a versão final usa 2)
#endif
            hud.SetObjective("Sobreviva ao " + FalconBoss.DisplayName + " por 2 minutos");
            StartCoroutine(ClearObjectiveAfter(6f));
            bool frenzy = false;
            while (left > 0f && !ending && (ph == null || !ph.IsDead))
            {
                left -= Time.deltaTime;
                if (!frenzy && left <= 30f) { frenzy = true; boss.StartFrenzy(); }
                yield return null;
            }
            hud.SetObjective("");
            Destroy(dodge);
            if (ending || (ph != null && ph.IsDead)) { boss.Freeze(); yield break; }

            // uma pedra cai sobre o chefe
            boss.Freeze();
            PinkGuard.Fix("pedra");
            Vector3 bp = boss.transform.position;
            float walk2 = 0f, run2 = 0f;
            if (fps != null) { walk2 = fps.walkSpeed; run2 = fps.runSpeed; fps.walkSpeed = 0f; fps.runSpeed = 0f; }

            // tudo treme, cai poeira do teto e a câmera vira para o chefe
            AudioDirector.Sfx("rumble", 1f);
            CameraShake.Hold(0.05f);
            CameraShake.Kick(0.15f, 1.5f);
            CinematicCam cine2 = pcam != null ? CinematicCam.On(pcam) : null;
            if (cine2 != null) cine2.Begin(boss.transform, 11f, 1.2f, 3.2f, 48f, 3f);
            // a pedra só cai onde há chão de verdade (nunca sobre uma lacuna): se o chefe estiver perto de uma, ele é levado para o chão firme mais próximo
            Vector3 safe = SolidGroundNear(bp, boss.transform, playerT);
            for (float t = 0f; t < 1.8f; t += Time.deltaTime)
            {
                boss.transform.position = Vector3.MoveTowards(boss.transform.position, new Vector3(safe.x, bp.y, safe.z), 6f * Time.deltaTime);
                if (fps != null) fps.LookAtPoint(boss.transform.position + Vector3.up * 2f, Mathf.Clamp01(Time.deltaTime * 4f));
                yield return null;
            }
            bp = new Vector3(safe.x, bp.y, safe.z);
            boss.transform.position = bp;

            var rock = new GameObject("FallingRock");
            rock.AddComponent<FallingRock>().Run(new Vector3(bp.x, 0f, bp.z), 1.8f, 2.4f, 0f, playerT, collapse.rockPrefab);
            // ele some assim que a pedra o atinge (sem a animação de queda, que o levava para fora do mapa)
            yield return new WaitForSeconds(1.8f + 0.55f);
            boss.Hide();
            boss.Growl(0f);
            yield return new WaitForSeconds(3f);
            CameraShake.Hold(0f);
            if (cine2 != null) cine2.End();
            if (fps != null) { fps.walkSpeed = walk2; fps.runSpeed = run2; }
            yield return new WaitForSeconds(1f);
            fragment.locked = false;
            StartCoroutine(LastFragmentSequence());
        }

        static bool HasFloorAt(float x, float z, float y, Transform ignoreA, Transform ignoreB)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, y + 3f, z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits)
            {
                if (ignoreA != null && h.transform.IsChildOf(ignoreA)) continue;
                if (ignoreB != null && h.transform.IsChildOf(ignoreB)) continue;
                if (h.normal.y > 0.7f && h.point.y > y - 0.5f && h.point.y < y + 0.5f) return true;
            }
            return false;
        }

        /// <summary>Ponto de chão firme mais perto de p (a pedra ocupa uns 2,4 m): o próprio p se estiver tudo sólido; se a sala não tiver colisor de chão, devolve p.</summary>
        static Vector3 SolidGroundNear(Vector3 p, Transform ignoreA, Transform ignoreB)
        {
            const float R = 2.6f;
            Vector2[] ring = { Vector2.zero, new Vector2(R, 0f), new Vector2(-R, 0f), new Vector2(0f, R), new Vector2(0f, -R),
                new Vector2(R * 0.7f, R * 0.7f), new Vector2(-R * 0.7f, R * 0.7f), new Vector2(R * 0.7f, -R * 0.7f), new Vector2(-R * 0.7f, -R * 0.7f) };
            if (ignoreB != null && !HasFloorAt(ignoreB.position.x, ignoreB.position.z, p.y, ignoreA, ignoreB)) return p;   // sem colisor de chão: não dá para validar
            for (float d = 0f; d <= 14f; d += 1f)
            {
                int steps = d < 0.1f ? 1 : Mathf.CeilToInt(d * 6f);
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    float cx = Mathf.Clamp(p.x + Mathf.Cos(a) * d, 53f, 77f), cz = Mathf.Clamp(p.z + Mathf.Sin(a) * d, 3f, 27f);
                    bool ok = true;
                    foreach (Vector2 o in ring)
                        if (!HasFloorAt(cx + o.x, cz + o.y, p.y, ignoreA, ignoreB)) { ok = false; break; }
                    if (ok) return new Vector3(cx, p.y, cz);
                }
            }
            return p;
        }

        IEnumerator ClearObjectiveAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            hud.SetObjective("");
        }

        /// <summary>Abertura da Fase 3: o jogador cai do teto para dentro da câmara; o objetivo aparece depois que ele pousa.</summary>
        IEnumerator Fase3Arrival()
        {
            // entra pela porta da Fase 2: aparece em pé dentro da câmara, sem cair do teto
            SimpleFPSController ctrl = playerT.GetComponent<SimpleFPSController>();
            if (ctrl != null) ctrl.TeleportTo(new Vector3(3f, 0.1f, 6f), 90f);
            yield return null;
        }

        void OnLastFragment(FragmentPickup f)
        {
            BladeTrap.Arm();
        }

        /// <summary>Tudo treme, ele olha para cima de um lado para o outro e fala; só então começa a contagem e a queda das pedras.</summary>
        IEnumerator LastFragmentSequence()
        {
            AudioDirector.Instance.StopLoop("ambient");
            AudioDirector.Sfx("rumble", 1f);
            CameraShake.Hold(0.06f);
            CameraShake.Kick(0.2f, 1.2f);
            hud.ShowDialogue("A câmara vai desabar! Preciso pegar o fragmento e sair daqui logo.", 4.5f);
            Vector3 eye = playerT.position + Vector3.up * 1.6f;
            Vector3 fwd = playerT.forward, right = playerT.right;
            Vector3[] looks = { eye + fwd * 4f - right * 4f + Vector3.up * 5f, eye + fwd * 4f + right * 4f + Vector3.up * 5f, eye + fwd * 6f - right * 3f + Vector3.up * 4f, eye + fwd * 6f };
            for (int i = 0; i < looks.Length; i++)
                for (float t = 0f; t < 0.85f; t += Time.deltaTime)
                {
                    if (fps != null) fps.LookAtPoint(looks[i], Mathf.Clamp01(Time.deltaTime * 5f));
                    yield return null;
                }

            collapse.path = null;
            collapse.pebbles = true;
            collapse.duration = 100f;
            collapse.Begin(playerT);
            AudioDirector.Instance.Loop("tomb", Resources.Load<AudioClip>("Sfx/tomb_ambience"), 0.7f);
            machine.canInteract = true;
            machine.prompt = "[E] Reconstruir a máquina";
            AudioDirector.Instance.StopLoop("ambient");
            AudioDirector.Instance.Loop("tense", "tense", 0.6f);
            AudioDirector.Instance.Loop("heart", "heartbeat", 0.7f);
        }

        void OnCollapseTimeUp()
        {
            if (ending) return;
            ending = true;
            if (fps != null) fps.enabled = false;
            hud.ShowMessage("A FENDA COLAPSOU");
            StartCoroutine(ReloadAfter(3.5f));
        }

        IEnumerator Fase3Complete()
        {
            ending = true;
            collapse.Stop();
            AudioDirector.Instance.StopLoop("heart");
            AudioDirector.Instance.StopLoop("tense");
            AudioDirector.Instance.StopLoop("tomb");

            // ele mesmo vai até cada vão do anel e a mão encaixa a peça (o jogador não controla nada)
            float walk = 0f, run = 0f, look = 0f;
            if (fps != null) { walk = fps.walkSpeed; run = fps.runSpeed; look = fps.lookSpeed; fps.walkSpeed = 0f; fps.runSpeed = 0f; fps.lookSpeed = 0f; }
            Camera pc = playerT.GetComponentInChildren<Camera>(true);
            PunchAnimator punch = playerT.GetComponentInChildren<PunchAnimator>(true);

            yield return machine.PlacePieces(playerT, fps, pc != null ? pc.transform : playerT, punch);
            machine.Restore();
            AudioDirector.Sfx("portal", 1f);
            CameraShake.Kick(0.08f, 1.5f);
            if (fps != null) { fps.walkSpeed = walk; fps.runSpeed = run; fps.lookSpeed = look; }

            while (true)
            {
                Vector3 d = machine.CenterWorld() - playerT.position;
                d.y = 0f;
                if (d.magnitude < 1.8f) break;
                yield return null;
            }
            GameManager.Epilogue = true;
            gm.GoTo(SceneNames.Prologo, Color.black, null, null, 0f, 0.3f);
        }

        IEnumerator KeepLooking(Vector3 point, System.Func<bool> active)
        {
            while (active())
            {
                if (fps != null) fps.LookAtPoint(point, Mathf.Clamp01(Time.deltaTime * 4f));
                yield return null;
            }
        }

        void BeginFinal()
        {
            AudioDirector.Instance.Loop("tense", "tense", 0.35f);
            hud.ShowSubtitle("Encaixe os três fragmentos e ative a máquina.", 6f);

            machine.SetRebuilt(false);
            machine.canInteract = true;
            machine.prompt = "[E] Encaixar os fragmentos e ativar a máquina";
            machine.Activated += m => StartCoroutine(FinalSequence());

            if (collapse != null) collapse.gameObject.SetActive(false);
        }

        IEnumerator FinalSequence()
        {
            ending = true;
            if (collapse != null) collapse.Stop();
            AudioDirector.Instance.StopLoop("tense");
            hud.ShowSubtitle("Encaixando os fragmentos...", 3.5f);
            CameraShake.Hold(0.05f);
            yield return machine.DockFragments(playerT.position + Vector3.up * 1.3f, 3.2f);

            CameraShake.Hold(0f);
            AudioDirector.Sfx("portal", 1f);
            AudioDirector.Instance.Loop("calm", "calm", 0.5f);
            hud.ShowBanner("EXPEDIÇÃO CONCLUÍDA");
            hud.ShowSubtitle("A fenda se fecha. O portal leva de volta ao futuro.", 4.5f);
            yield return new WaitForSeconds(5f);

            yield return gm.Fader.FadeTo(Color.white, 1.4f);
            if (fps != null) fps.enabled = false;
            hud.SetGameplayVisible(false);
            hud.ShowBanner("");
            CreditsScreen.Show();
            yield return gm.Fader.FadeFrom(1.6f);
        }
    }
}
