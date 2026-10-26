using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>Tela 01 – Ativação acidental e portal temporal.</summary>
    public class PrologueDirector : MonoBehaviour
    {
        [Header("Fundo")]
        [Tooltip("Câmera com fundo preto (sem céu) e neblina escura, para o que não existe no cenário não aparecer como céu.")]
        public bool hideSky = true;
        public float fogDensity = 0.007f;

        [Header("Cena")]
        [Tooltip("A máquina do tempo. Vazio = procura na cena.")]
        public TimeMachine machine;
        [Tooltip("Centro do portal (objeto AlvoDoPortal). Vazio = 1,6 m acima da máquina.")]
        public Transform lookTarget;

        [Header("Ativação")]
        [Tooltip("Distância (no chão, em metros) entre o jogador e o portal para a máquina ativar.")]
        public float triggerDistance = 4f;
        [Tooltip("Segundos de carga (o jogador ainda pode andar) antes do arrasto.")]
        public float chargeSeconds = 2f;
        [Tooltip("Segundos que o portal leva para arrastar o jogador.")]
        public float pullSeconds = 1.5f;
        [Tooltip("Depois do arrasto (e do walkie caído) a carga e a entrada no portal são mais curtas: quase sem pausa.")]
        public float chargeAfterDrag = 0.35f;
        public float pullAfterDrag = 0.9f;
        [Tooltip("O arrasto para tantos metros antes do centro do portal.")]
        public float stopShort = 0.4f;

        [Header("Arrasto (a máquina puxa o jogador antes do portal)")]
        [Tooltip("Distância (no chão, até a máquina) em que o jogador perde o controle e começa a ser arrastado. Se 'autoDragDistance' estiver ligado, vira 30% da distância em que o jogador começou.")]
        public float dragStartDistance = 20f;
        public bool autoDragDistance = true;
        public float dragSpeedStart = 2.5f;
        public float dragSpeedEnd = 13f;
        [Tooltip("Segundos até o walkie escapar da mão.")]
        public float dropWalkieAfter = 0.9f;

        [Header("Animação da máquina (1 = velocidade original do clipe, que dura 5 s)")]
        [Tooltip("Velocidade antes de ativar. 0 = máquina parada.")]
        public float idleAnimSpeed = 0.15f;
        [Tooltip("Velocidade no pico da ativação. Fórmula: 5 ÷ segundos desejados por volta (2 s = 2,5).")]
        public float activeAnimSpeed = 3f;

        GameManager gm;
        HUD hud;
        Transform player;
        Camera cam;
        SimpleFPSController fps;
        Rigidbody rb;
        GameObject scannerRoot, exclaim;
        Text errorText;
        bool finishing, controlTaken, dragStarted, dragDistanceSet, paused;
        TimeMachineAura auraRef;
        Coroutine blinkRoutine, sequenceRoutine;

        void Start()
        {
            gm = GameManager.Ensure();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            hud = HUD.Create();
            hud.SetGameplayVisible(false);
            hud.ResumeRequested += () => SetPaused(false);
            hud.MenuRequested += () => { SetPaused(false); gm.ToMenu(); };
            BuildScanner();

            if (!SetupPlayer()) return;

            if (machine == null) machine = FindAnyObjectByType<TimeMachine>();
            if (machine == null && lookTarget == null)
            {
                Debug.LogError("[RiftOfTime] Prólogo: não achei a Máquina do Tempo nem o AlvoDoPortal na cena.");
                return;
            }
            if (GameManager.Epilogue)
            {
                GameManager.Epilogue = false;
                StartCoroutine(Epilogue());
                return;
            }
            if (machine != null) machine.SetAnimationSpeed(idleAnimSpeed);

            var aura = GetComponent<TimeMachineAura>();
            auraRef = aura;
            if (aura == null)
            {
                aura = gameObject.AddComponent<TimeMachineAura>();
                aura.Begin(PortalPoint(), player, triggerDistance);
            }
            if (GetComponent<WalkieConversation>() == null) gameObject.AddComponent<WalkieConversation>().Begin(aura);
            {
                var swp = System.Diagnostics.Stopwatch.StartNew();
                PinkGuard.Fix("início");
                Debug.Log("[Carregar] PinkGuard em " + swp.ElapsedMilliseconds + " ms");
            }

            AudioDirector.Instance.Loop("ambient", "ambient", 0.3f);
            sequenceRoutine = StartCoroutine(Sequence());
        }

        /// <summary>Volta do futuro: a máquina sumiu, o walkie está no chão e o Leo chama; ao pegá-lo, vêm as considerações finais.</summary>
        IEnumerator Epilogue()
        {
            PunchAnimator punch = FindFirstObjectByType<PunchAnimator>();
            if (fps != null) { fps.walkSpeed = 0f; fps.runSpeed = 0f; fps.lookSpeed = 0f; }
            hud.SetGameplayVisible(true);

            // ele é jogado para fora no lugar onde a máquina estava; ela some e o walkie fica caído mais adiante
            Vector3 center = PortalPoint();
            Vector3 away = player.position - center;
            away.y = 0f;
            away = away.sqrMagnitude < 0.01f ? Vector3.forward : away.normalized;
            float g0 = GroundY(player.position);
            float offset = float.IsNaN(g0) ? 0f : player.position.y - g0;
            float yaw = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
            if (machine != null) machine.gameObject.SetActive(false);
            Vector3 startPos = center + away * 1.8f;
            float gs = GroundY(startPos);
            startPos.y = (float.IsNaN(gs) ? (float.IsNaN(g0) ? player.position.y : g0) : gs) + offset;
            if (fps != null) fps.TeleportTo(startPos, yaw);
            Vector3 wPos = center + away * 5.5f;
            float gw = GroundY(wPos);
            wPos.y = (float.IsNaN(gw) ? startPos.y - offset : gw) + 0.1f;

            AudioDirector.Instance.Loop("ambient", "ambient", 0.3f);
            CameraShake.Kick(0.25f, 1.2f);
            yield return new WaitForSeconds(1.8f);

            Transform walkie = punch != null ? punch.DropWalkie() : null;
            AudioSource hissSrc = null, clickSrc = null;
            if (walkie != null)
            {
                walkie.position = wPos;
                walkie.rotation = Quaternion.Euler(0f, yaw + 35f, 0f);
                Rigidbody wrb = walkie.GetComponent<Rigidbody>();
                if (wrb != null) { wrb.linearVelocity = Vector3.zero; wrb.angularVelocity = Vector3.zero; }
                hissSrc = walkie.gameObject.AddComponent<AudioSource>();
                hissSrc.clip = WalkieConversation.MakeHissLoop();
                hissSrc.loop = true;
                hissSrc.volume = 0.25f;
                hissSrc.spatialBlend = 0.8f;
                hissSrc.rolloffMode = AudioRolloffMode.Linear;
                hissSrc.minDistance = 2f;
                hissSrc.maxDistance = 40f;
                hissSrc.Play();
                clickSrc = walkie.gameObject.AddComponent<AudioSource>();
                clickSrc.spatialBlend = 0f;
            }
            AudioClip clickClip = WalkieConversation.MakeClick();
            yield return new WaitForSeconds(2.2f);

            // o Leo chama pelo rádio caído no chão (só legenda: chiado e estalo do walkie)
            if (clickSrc != null) clickSrc.PlayOneShot(clickClip, 1f);
            if (hissSrc != null) hissSrc.volume = 0.7f;
            // a fala do Leo usa o mesmo som de voz do prólogo
            AudioSource leoSrc = gameObject.AddComponent<AudioSource>();
            leoSrc.clip = Resources.Load<AudioClip>("Walkie/sinal_ruim");
            leoSrc.spatialBlend = 0f;
            leoSrc.volume = 0.95f;
            if (leoSrc.clip != null) leoSrc.Play();
            hud.ShowDialogue("Leo: Você está aí?", 3.2f, false);
            yield return new WaitForSeconds(3.2f);
            leoSrc.Stop();
            Destroy(leoSrc);
            if (clickSrc != null) clickSrc.PlayOneShot(clickClip, 0.8f);
            if (hissSrc != null) hissSrc.volume = 0.25f;
            yield return new WaitForSeconds(0.6f);

            // ele mesmo vai até lá, agacha, pega o walkie e liga
            if (walkie != null)
            {
                Vector3 stand = wPos - away * 0.9f;
                stand.y = startPos.y;
                while ((player.position - stand).sqrMagnitude > 0.01f)
                {
                    Vector3 np = Vector3.MoveTowards(player.position, stand, 1.7f * Time.deltaTime);
                    if (fps != null) { fps.TeleportTo(np, yaw); fps.LookAtPoint(wPos, Mathf.Clamp01(Time.deltaTime * 3f)); }
                    yield return null;
                }

                Vector3 camRest = cam.transform.localPosition;
                for (float k = 0f; k < 1f; k += Time.deltaTime / 1f)
                {
                    cam.transform.localPosition = camRest + Vector3.down * (0.8f * Mathf.SmoothStep(0f, 1f, k));
                    if (fps != null) fps.LookAtPoint(wPos, Mathf.Clamp01(Time.deltaTime * 6f));
                    yield return null;
                }
                AudioDirector.Sfx("hit", 0.15f);
                yield return punch.RetrieveWalkie(0.8f);
                if (hissSrc != null) { hissSrc.spatialBlend = 0.2f; hissSrc.volume = 0.2f; }
                for (float k = 0f; k < 1f; k += Time.deltaTime / 1f)
                {
                    cam.transform.localPosition = camRest + Vector3.down * (0.8f * (1f - Mathf.SmoothStep(0f, 1f, k)));
                    if (fps != null) fps.LookAtPoint(wPos + Vector3.up * 3f, Mathf.Clamp01(Time.deltaTime * 4f));
                    yield return null;
                }
                cam.transform.localPosition = camRest;
            }

            if (punch == null) punch = FindFirstObjectByType<PunchAnimator>();
            if (punch != null) punch.SetTalking(true);      // dobra o braço com o walkie perto da boca, como no prólogo
            yield return new WaitForSeconds(0.6f);
            if (clickSrc != null) clickSrc.PlayOneShot(clickClip, 0.9f);
            hud.ShowDialogue("Marco: Sim, estou bem.", 3f, true);
            yield return new WaitForSeconds(3f);
            if (clickSrc != null) clickSrc.PlayOneShot(clickClip, 0.8f);
            yield return new WaitForSeconds(0.4f);
            if (punch != null) punch.SetTalking(false);
            yield return new WaitForSeconds(1.2f);

            // a tela escurece e fica em silêncio até os créditos
            if (AudioDirector.Instance != null) AudioDirector.Instance.StopAllLoops(2f);
            if (hissSrc != null) Destroy(hissSrc);
            yield return gm.Fader.FadeTo(Color.black, 3f);
            hud.SetGameplayVisible(false);
            yield return new WaitForSeconds(1.5f);
            CreditsScreen.Show();
            yield return gm.Fader.FadeFrom(1.2f);
        }

        bool SetupPlayer()
        {
            GameObject p = null;
            foreach (GameObject c in GameObject.FindGameObjectsWithTag("Player"))
                if (c.GetComponentInChildren<Camera>(true) != null) { p = c; break; }
            if (p == null) p = GameObject.FindGameObjectWithTag("Player");
            if (p == null)
            {
                Debug.LogError("[RiftOfTime] Prólogo: coloque o Player.prefab (tag Player) na cena.");
                return false;
            }
            player = p.transform;
            if (p.GetComponent<PlayerCombat>() == null) p.AddComponent<PlayerCombat>();
            fps = p.GetComponent<SimpleFPSController>();
            rb = p.GetComponent<Rigidbody>();
            cam = p.GetComponentInChildren<Camera>(true);
            if (cam == null) cam = p.transform.root.GetComponentInChildren<Camera>(true);
            if (cam == null)
            {
                Debug.LogError("[RiftOfTime] Prólogo: o objeto com a tag Player ('" + p.name + "') não tem câmera. " +
                               "Use o Player.prefab inteiro e não tire a câmera dele.", p);
                return false;
            }

            cam.gameObject.tag = "MainCamera";

            if (hideSky)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.015f, 0.012f, 0.01f);
                RenderSettings.fogDensity = fogDensity;
            }
            if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            foreach (Camera other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (other == cam || other.transform.IsChildOf(player)) continue;
                Debug.LogWarning("[RiftOfTime] Prólogo: desliguei a câmera extra '" + other.name + "'. Apague o CameraRig da cena.");
                other.gameObject.SetActive(false);
            }
            return true;
        }

        /// <summary>Tira o controle do jogador: sem teclado, sem física, sem colisão.</summary>
        void TakeControl()
        {
            if (controlTaken) return;
            controlTaken = true;
            if (fps != null) fps.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.isKinematic = true;
            }
            foreach (Collider c in player.GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        bool portalCached;
        Vector3 portalCenter;

        Vector3 PortalPoint()
        {
            if (machine != null)
            {
                if (!portalCached)
                {
                    portalCached = true;
                    portalCenter = machine.transform.position + Vector3.up * 1.6f;
                    var rr = machine.GetComponentsInChildren<Renderer>();
                    if (rr.Length > 0)
                    {
                        Bounds bb = rr[0].bounds;
                        foreach (var r in rr) bb.Encapsulate(r.bounds);
                        portalCenter = bb.center;
                    }
                    if (lookTarget != null && Vector3.Distance(lookTarget.position, portalCenter) > 10f)
                        Debug.LogWarning("[RiftOfTime] O AlvoDoPortal (" + lookTarget.position.ToString("F1") + ") está longe da máquina (" + portalCenter.ToString("F1") + "); usando o centro da máquina.");
                }
                return portalCenter;
            }
            if (lookTarget != null) return lookTarget.position;
            return player.position + player.forward * 10f;
        }

        bool NearPortal()
        {
            Vector3 d = PortalPoint() - player.position;
            d.y = 0f;
            return d.magnitude <= triggerDistance;
        }

        void BuildScanner()
        {
            scannerRoot = new GameObject("ScannerVazio");
            scannerRoot.transform.SetParent(transform, false);
            exclaim = new GameObject("ExclamacaoVazia");
            exclaim.transform.SetParent(transform, false);
            scannerRoot.SetActive(false);
            exclaim.SetActive(false);
        }

        void Update()
        {
            if (!dragDistanceSet && player != null && (machine != null || lookTarget != null))
            {
                dragDistanceSet = true;
                if (autoDragDistance) dragStartDistance = Mathf.Max(triggerDistance * 3f, HorizontalDistance() * 0.3f);
                Debug.Log("[Arrasto] jogador começa a " + HorizontalDistance().ToString("F1") + " m da máquina; o arrasto começa a " + dragStartDistance.ToString("F1") + " m");
            }
            if (!dragStarted && !finishing && !controlTaken && player != null && (machine != null || lookTarget != null) && HorizontalDistance() <= dragStartDistance)
                StartCoroutine(DragSequence());

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !finishing && !dragStarted && !controlTaken)
                SetPaused(!paused);
            if (paused) return;

            if (!finishing && Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
                StartCoroutine(Finish());
            }
        }

        float HorizontalDistance()
        {
            Vector3 d = PortalPoint() - player.position;
            d.y = 0f;
            return d.magnitude;
        }

        float GroundY(Vector3 p)
        {
            float best = float.NaN;
            foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 1.2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(player) || h.normal.y < 0.5f) continue;
                if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
            }
            return best;
        }

        /// <summary>A 80,5 m da máquina o jogador perde o controle: é arrastado até ela PELO CAMINHO ANDÁVEL (corredores, portas</summary>
        IEnumerator DragSequence()
        {
            dragStarted = true;
            PinkGuard.Fix("arrasto");
            var conv = GetComponent<WalkieConversation>();
            if (conv != null) conv.Abort();
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;
            if (fps != null) fps.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.isKinematic = true;
            }
            if (auraRef != null) auraRef.driveCamera = false;

            float g0 = GroundY(player.position);
            float groundOffset = float.IsNaN(g0) ? 0f : player.position.y - g0;
            Vector3 feet = new Vector3(player.position.x, float.IsNaN(g0) ? player.position.y : g0, player.position.z);
            float fovStart = cam.fieldOfView;
            var punch = FindFirstObjectByType<PunchAnimator>();

            {
                Vector3 mc = Vector3.zero; string mn = "(sem máquina)";
                if (machine != null)
                {
                    var rr = machine.GetComponentsInChildren<Renderer>();
                    mn = machine.name + " em " + machine.transform.position.ToString("F1");
                    if (rr.Length > 0) { Bounds bb = rr[0].bounds; foreach (var r in rr) bb.Encapsulate(r.bounds); mc = bb.center; mn += ", malhas centradas em " + mc.ToString("F1") + " tamanho " + bb.size.ToString("F1"); }
                }
                Debug.Log("[Arrasto] jogador " + player.position.ToString("F1") + " (chão " + (float.IsNaN(g0) ? "?" : g0.ToString("F1")) + "), PortalPoint " + PortalPoint().ToString("F1") +
                          ", lookTarget " + (lookTarget != null ? lookTarget.position.ToString("F1") : "nenhum") + ", " + mn + ", câmera olha " + cam.transform.forward.ToString("F2"));
            }

            var finder = new DragPath(feet, PortalPoint(), player, machine != null ? machine.transform : null);

            AudioDirector.Sfx("rumble", 1f);
            AudioDirector.Sfx("crackle", 1f);
            AudioDirector.Sfx("portal", 0.9f);
            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.Loop("drag_pulse", "heartbeat", 1f);
                AudioDirector.Instance.Loop("drag_tense", "tense", 0.7f);
            }
            CameraShake.Kick(0.25f, 0.7f);

            string[] cries =
            {
                "Marco: O quê...? Eu não consigo andar!",
                "Marco: Meu rádio!",
                "Marco: Me solta! ME SOLTA!",
                "Marco: Leo!! Alguém!",
            };
            float[] cryAt = { 0.2f, 1.6f, 3.2f, 5.6f };
            int cryIdx = 0;

            bool dropped = false;
            float t = 0f, nextJerk = 0.6f, jerkYaw = 0f, jerkPitch = 0f;

            List<Vector3> pts = null;
            List<float> cum = null;
            float total = 0f, s = 0f, lateral = 0f;
            bool straightInit = false;
            Vector3 straightBase = player.position;
            Vector3 moveDir = (PortalPoint() - player.position); moveDir.y = 0f; moveDir.Normalize();
            Vector3 straightGoal = PortalPoint();

            while (!finishing && !NearPortal())
            {
                float dt = Time.deltaTime;
                t += dt;
                if (Mathf.FloorToInt(t) != Mathf.FloorToInt(t - dt))
                    Debug.Log("[Arrasto] t=" + t.ToString("F0") + " jogador " + player.position.ToString("F1") + " distância " + HorizontalDistance().ToString("F1") +
                              " modo " + (pts == null ? "procurando" : pts.Count > 1 ? "caminho" : "reta") + " (células " + finder.Expanded + ", feito " + finder.Done + ")");

                if (!finder.Done && t < 5f) finder.Step(260);
                if (pts == null && (finder.Done || t >= 5f))
                {
                    if (finder.Found)
                    {
                        pts = finder.Path;
                        cum = new List<float> { 0f };
                        for (int i = 1; i < pts.Count; i++) cum.Add(cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]));
                        total = cum[cum.Count - 1];
                        Debug.Log("[Arrasto] caminho achado: " + pts.Count + " pontos, " + total.ToString("F0") + " m (" + finder.Expanded + " células)");
                    }
                    else
                    {
                        pts = new List<Vector3>();
                        Debug.LogWarning("[Arrasto] não achei caminho andável; arrastando em linha reta");
                    }
                }

                float u;
                Vector3 look;
                if (pts != null && pts.Count > 1)
                {
                    u = Mathf.Clamp01(s / Mathf.Max(1f, total));
                    float speed = Mathf.Lerp(dragSpeedStart, dragSpeedEnd, Mathf.Pow(u, 1.5f)) * Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 1.0f);
                    s = Mathf.Min(total, s + speed * dt);
                    Vector3 p = SampleBlend(pts, cum, s);
                    Vector3 ahead = SampleBlend(pts, cum, Mathf.Min(total, s + 2f));
                    Vector3 dv = ahead - p; dv.y = 0f;
                    if (dv.sqrMagnitude > 1e-4f) moveDir = Vector3.Slerp(moveDir, dv.normalized, 1f - Mathf.Exp(-5f * dt));
                    lateral = CenterOffset(p, moveDir, lateral, dt);
                    Vector3 perp = Vector3.Cross(Vector3.up, moveDir).normalized;
                    player.position = new Vector3(p.x, p.y + groundOffset, p.z) + perp * lateral;
                    look = moveDir;
                }
                else if (pts != null)
                {
                    if (!straightInit) { straightInit = true; straightBase = player.position; lateral = 0f; }
                    Vector3 flat = straightGoal - straightBase; flat.y = 0f;
                    float dist = flat.magnitude;
                    Vector3 dir = dist > 0.01f ? flat / dist : player.forward;
                    u = Mathf.Clamp01((dragStartDistance - dist) / Mathf.Max(1f, dragStartDistance - triggerDistance));
                    float speed = Mathf.Lerp(dragSpeedStart, dragSpeedEnd, Mathf.Pow(u, 1.5f)) * Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 1.0f);
                    straightBase += dir * Mathf.Min(speed * dt, dist);
                    lateral = CenterOffset(straightBase, dir, lateral, dt);
                    Vector3 p = straightBase + Vector3.Cross(Vector3.up, dir).normalized * lateral;
                    float gy = GroundY(p);
                    if (!float.IsNaN(gy)) p.y = Mathf.Lerp(player.position.y, gy + groundOffset, 1f - Mathf.Exp(-14f * dt));
                    straightBase.y = p.y;
                    player.position = p;
                    moveDir = dir;
                    look = dir;
                }
                else
                {
                    u = 0f;
                    look = moveDir;
                }

                nextJerk -= dt;
                if (nextJerk <= 0f)
                {
                    nextJerk = Random.Range(0.7f, 1.5f);
                    jerkYaw = Random.Range(-9f, 9f);
                    jerkPitch = Random.Range(-5f, 7f);
                    CameraShake.Kick(Random.Range(0.12f, 0.22f), 0.3f);
                }
                jerkYaw = Mathf.Lerp(jerkYaw, 0f, 1f - Mathf.Exp(-5f * dt));
                jerkPitch = Mathf.Lerp(jerkPitch, 0f, 1f - Mathf.Exp(-5f * dt));
                Vector3 back = -look; back.y = 0.14f;
                float roll = Mathf.Sin(t * 6.3f) * 3.5f + (Mathf.PerlinNoise(t * 2f, 0.4f) - 0.5f) * 8f;
                Quaternion want = Quaternion.LookRotation(back.normalized, Vector3.up) * Quaternion.Euler(jerkPitch, jerkYaw, roll);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-3.2f * dt));

                float fov = Mathf.Lerp(fovStart + 16f, 46f, Mathf.SmoothStep(0f, 1f, u * 1.2f));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Exp(4f, dt));
                CameraShake.Hold(Mathf.Lerp(0.05f, 0.15f, u));

                if (!dropped && t >= dropWalkieAfter)
                {
                    dropped = true;
                    if (punch != null)
                    {
                        Transform fallen = punch.DropWalkie();
                        if (fallen != null && conv != null) conv.AttachHissTo(fallen);
                    }
                    CameraShake.Kick(0.3f, 0.5f);
                }

                if (cryIdx < cries.Length && t >= cryAt[cryIdx])
                {
                    if (hud != null) hud.ShowDialogue(cries[cryIdx], 2.4f, false);
                    cryIdx++;
                }

                if (pts != null && pts.Count > 1 && s >= total) { straightGoal = PortalPoint(); pts = new List<Vector3>(); }
                yield return null;
            }
        }

        static Vector3 SampleBlend(List<Vector3> pts, List<float> cum, float s)
        {
            return (Sample(pts, cum, s - 1.2f) + Sample(pts, cum, s) * 2f + Sample(pts, cum, s + 1.2f)) * 0.25f;
        }

        static Vector3 Sample(List<Vector3> pts, List<float> cum, float s)
        {
            s = Mathf.Clamp(s, 0f, cum[cum.Count - 1]);
            int lo = 0, hi = cum.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (cum[mid] <= s) lo = mid; else hi = mid;
            }
            float span = Mathf.Max(1e-4f, cum[hi] - cum[lo]);
            return Vector3.Lerp(pts[lo], pts[hi], (s - cum[lo]) / span);
        }

        float CenterOffset(Vector3 p, Vector3 forward, float current, float dt)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float dl = WallDistance(p, -right), dr = WallDistance(p, right);
            float target = 0f;
            if (dl < 8f && dr < 8f) target = (dr - dl) * 0.5f;
            else if (dl < 1.6f) target = 1.6f - dl;
            else if (dr < 1.6f) target = -(1.6f - dr);
            target = Mathf.Clamp(target, -3f, 3f);
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-4f * dt));
        }

        float WallDistance(Vector3 p, Vector3 dir)
        {
            float best = 8f;
            foreach (float h in new[] { -0.2f, 0.5f })
                foreach (RaycastHit hit in Physics.RaycastAll(p + Vector3.up * h, dir, 8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(player)) continue;
                    if (hit.normal.y > 0.6f) continue;
                    if (hit.distance < best) best = hit.distance;
                }
            return best;
        }

        static AudioClip pullWhoosh;

        static AudioClip PullWhoosh()
        {
            if (pullWhoosh != null) return pullWhoosh;
            const int rate = 22050;
            int n = (int)(1.3f * rate);
            var d = new float[n];
            var rng = new System.Random(5);
            float lp = 0f, ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 40f, Mathf.Clamp01(t / 0.25f)) * t) * Mathf.Exp(-t * 7f) * 0.95f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += Mathf.Lerp(0.05f, 0.6f, t / 1.3f) * (noise - lp);
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-Mathf.Max(0f, t - 0.15f) * 2.2f);
                ph += 2f * Mathf.PI * Mathf.Lerp(180f, 1500f, (t / 1.3f) * (t / 1.3f)) / rate;
                d[i] = Mathf.Clamp(thump + lp * 1.8f * env + Mathf.Sin(ph) * 0.25f * env, -1f, 1f);
            }
            pullWhoosh = AudioClip.Create("PullWhoosh", n, 1, rate, false);
            pullWhoosh.SetData(d, 0);
            return pullWhoosh;
        }

        static float Exp(float k, float dt) { return Mathf.Exp(-k * dt); }

        void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            AudioListener.pause = value;
            if (fps != null) fps.enabled = !value;
            var combat = player != null ? player.GetComponent<PlayerCombat>() : null;
            if (combat != null) combat.enabled = !value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            hud.ShowPause(value);
        }

        IEnumerator Sequence()
        {
            string[] intro =
            {
                "Meu nome é Marco. Meu trabalho é vasculhar sítios arqueológicos de alto risco.",
                "Dentro de uma pirâmide recém-descoberta, encontrei uma câmara oculta. Nela, uma máquina de circuito incompatível com qualquer registro."
            };
            for (int i = 0; i < intro.Length && !NearPortal(); i++)
            {
                hud.ShowSubtitle(intro[i], 5f);
                float until = Time.time + 5.2f;
                while (Time.time < until && !NearPortal()) yield return null;
            }
            if (!NearPortal())
            {
                hud.ShowSubtitle("W A S D para andar, mouse para olhar. Explore a câmara e examine a máquina.", 8f);
                while (!NearPortal()) yield return null;
            }

            hud.ShowSubtitle("A curiosidade fala mais alto. Chego perto para examinar o mecanismo...", 6f);
            scannerRoot.SetActive(true);
            exclaim.SetActive(true);
            AudioDirector.Instance.StopLoop("ambient");
            AudioDirector.Sfx("rumble", 1f);
            AudioDirector.Sfx("crackle", 1f);
            blinkRoutine = StartCoroutine(Blink());
            Light glow = CreateGlow();

            float t = 0f;
            float charge = dragStarted ? chargeAfterDrag : chargeSeconds;
            float pull = dragStarted ? pullAfterDrag : pullSeconds;
            while (t < charge)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / charge);
                if (machine != null) machine.SetAnimationSpeed(Mathf.Lerp(idleAnimSpeed, activeAnimSpeed, k * k));
                CameraShake.Hold(Mathf.Lerp(0.005f, 0.04f, k));
                glow.intensity = Mathf.Lerp(0f, 6f, k);
                yield return null;
            }

            AudioDirector.Sfx(dragStarted ? PullWhoosh() : null, 1f);
            if (dragStarted) AudioDirector.Sfx("rumble", 1f); else AudioDirector.Sfx("portal", 1f);
            TakeControl();
            Vector3 aim = PortalPoint();
            Vector3 start = player.position;
            Vector3 camOffset = cam.transform.position - player.position;
            Vector3 end = aim - camOffset;
            Vector3 way = end - start;
            if (way.magnitude > stopShort) end = start + way * ((way.magnitude - stopShort) / way.magnitude);

            float fov0 = cam.fieldOfView;
            t = 0f;
            while (t < pull)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / pull);
                player.position = Vector3.Lerp(start, end, k * k);
                Quaternion look = Quaternion.LookRotation(aim - cam.transform.position, Vector3.up);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, Time.deltaTime * 3f);
                cam.fieldOfView = Mathf.Lerp(fov0, 32f, k * k);
                CameraShake.Hold(Mathf.Lerp(0.04f, 0.12f, k));
                glow.intensity = Mathf.Lerp(6f, 14f, k);
                yield return null;
            }

            yield return Finish();
        }

        Light CreateGlow()
        {
            var g = new GameObject("PortalGlow");
            Vector3 front = machine != null ? -machine.transform.forward * 0.8f : Vector3.back * 0.8f;
            g.transform.position = PortalPoint() + front;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.35f, 0.9f, 1f);
            l.range = 14f;
            l.intensity = 0f;
            return l;
        }

        IEnumerator Blink()
        {
            if (errorText == null) yield break;
            while (true)
            {
                errorText.enabled = !errorText.enabled;
                exclaim.SetActive(errorText.enabled);
                yield return new WaitForSeconds(0.14f);
            }
        }

        IEnumerator Finish()
        {
            if (finishing) yield break;
            finishing = true;
            if (player != null) TakeControl();
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            AudioListener.volume = 0f;
            CameraShake.Hold(0f);
            scannerRoot.SetActive(false);
            exclaim.SetActive(false);
            yield return new WaitForSeconds(0.9f);
            yield return gm.Fader.FadeTo(Color.white, 0.25f);
            GameManager.ApplyVolume();
            gm.GoTo(SceneNames.Fase1, Color.white);      // sem cartão de título: a Fase 1 abre com o Marco acordando
        }
    }
}
