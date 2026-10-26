using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>Verdadeiro enquanto uma cena move o jogador sozinho: os passos continuam tocando mesmo com o controle desligado.</summary>
    public static class ScriptedWalk { public static bool Active; }

    /// <summary>
    /// Abertura da Fase 1, dentro do templo: o Marco cai do portal, acorda no chão, levanta, anda até a máquina do tempo
    /// (quebrada, parada, com 3 peças do anel a menos) e a examina, falando sozinho em legendas.
    ///  - queda: vento subindo no escuro e o baque; depois batimento, zumbido no ouvido e som abafado;
    ///  - pisca: pálpebras (shader EyeLid, só preto) abrem um pouco, fecham, abrem mais, fecham e abrem de vez; a visão começa
    ///    embaçada e estourada de luz e vai ficando nítida (desfoque, vinheta e exposição);
    ///  - senta, levanta cambaleando, olha em volta, vê a máquina, ANDA até ela e a examina;
    ///  - o controle só volta no fim. Enter pula a cena.
    /// A máquina fica inerte: a animação do modelo é parada e nada a "ativa".
    /// </summary>
    public class WakeUpSequence : MonoBehaviour
    {
        // --------------------------------------------------------------------------------------- ajustes
        public float lyingHeight = 0.28f;     // altura dos olhos deitado (m acima do chão)
        public float lyingPitch = -72f;       // olhando para cima (graus)
        public bool startWithFall = true;     // começa caindo (vento) e bate no chão
        public float examineDistance = 3.2f;  // a que distância da máquina ele para
        public float walkSpeed = 2f;         // m/s, devagar, ainda tonto
        public Transform lookAtMachine;       // opcional: se vazio, procura "Máquina do Tempo (quebrada)" ou um TimeMachine

        Transform player;
        Camera cam;
        SimpleFPSController fps;
        HUD hud;
        Rigidbody rb;
        Transform arms;

        Material lidMat;
        Image lids, glare;
        Canvas canvas;
        Volume volume;
        DepthOfField dof;
        Vignette vignette;
        ColorAdjustments color;
        AudioLowPassFilter lowPass;
        AudioSource ringing;
        float open, blur = 1f, exposure = 2.5f, vig = 0.6f, glareAlpha;
        bool skip;
        float startedAt;

        Vector3 basePos;
        Quaternion baseRot;
        float eyeHeight, fovBase;
        float pitch, yaw, roll, height;

        public void Begin(HUD hudRef, Transform playerT, SimpleFPSController controller)
        {
            hud = hudRef; player = playerT; fps = controller;
            cam = player.GetComponentInChildren<Camera>();
            rb = player.GetComponent<Rigidbody>();
            StartCoroutine(Run());
        }

        // --------------------------------------------------------------------------------------- roteiro
        IEnumerator Run()
        {
            if (cam == null) { Finish(); yield break; }
            startedAt = Time.unscaledTime;
            Transform machine = lookAtMachine != null ? lookAtMachine : FindMachine();
            FreezeMachine(machine);
            Setup();

            // 0) queda e impacto
            SetPose(lyingPitch, 0f, 12f, lyingHeight);
            Apply();
            if (startWithFall) yield return Fall();

            // 1) escuro: batimento e zumbido
            yield return Wait(1.3f);

            // 2) pisca: abre um pouco, fecha, abre mais, fecha, abre de vez
            yield return Blink(0.17f, 0.55f, 0.35f, 0.5f);
            yield return Wait(0.35f);
            yield return Blink(0.32f, 0.5f, 0.55f, 0.35f);
            Say("Ugh... minha cabeça...", 2.8f);
            yield return Wait(0.5f);
            yield return Blink(0.55f, 0.6f, 0.75f, 0.3f);
            yield return Wait(0.25f);
            yield return OpenFully(2.6f);

            // 3) senta
            Say("O que... aconteceu?", 2.6f);
            yield return Move(lyingPitch, -18f, 12f, -6f, lyingHeight, 0.85f, 3.2f, 0f);
            yield return Wait(0.5f);

            // 4) levanta cambaleando
            Say("Onde eu estou?", 2.6f);
            yield return Move(-18f, 4f, -6f, 0f, 0.85f, eyeHeight, 3f, 4f);
            yield return Wait(0.3f);

            // 5) olha em volta
            Say("Isso não é a câmara... que lugar é esse?", 3.4f);
            yield return Move(4f, 2f, 0f, -42f, eyeHeight, eyeHeight, 1.5f, 2f);
            yield return Wait(0.35f);
            yield return Move(2f, 0f, -42f, 46f, eyeHeight, eyeHeight, 2.1f, -2f);
            yield return Wait(0.3f);
            Say("Essas paredes... é um templo. Será que eu estou no passado?", 4f);
            yield return Wait(2.6f);

            if (machine != null && !skip)
            {
                // 6) vira para trás e vê a máquina (ela fica atrás dele)
                Vector3 to = machine.position + Vector3.up * 1.4f - cam.transform.position;
                Vector3 local = player.InverseTransformDirection(to);
                float yawTo = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float pitchTo = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                yield return Move(0f, pitchTo, 46f, yawTo, eyeHeight, eyeHeight, 1.8f, 0f);
                if (!skip) CameraShake.Kick(0.05f, 0.4f);
                Say("A máquina!", 1.8f);
                yield return Wait(1.5f);
                Say("Está caída... preciso ver o que sobrou.", 3.2f);

                // vira o corpo para a máquina (a visão não muda, só passa a valer como "frente")
                FaceBody(yawTo);
                yaw = 0f;
                Apply();

                // 7) anda até ela
                yield return WalkTo(machine, examineDistance);

                // 8) examina: o anel, o buraco das peças, a base
                float fov0 = cam.fieldOfView;
                Say("O anel está quebrado.", 2.6f);
                yield return Move(pitch, -6f, 0f, -16f, eyeHeight, eyeHeight, 2f, 1f, fov0 - 8f);
                yield return Wait(0.5f);
                Say("Três peças arrancadas. Olha esses cortes...", 3.4f);
                yield return Move(-6f, -12f, -16f, 8f, eyeHeight, eyeHeight, 2.4f, 0f);
                yield return Wait(0.4f);
                Say("Sem elas o portal não abre. É por isso que está apagada.", 4f);
                yield return Move(-12f, 14f, 8f, 0f, eyeHeight, eyeHeight, 2.2f, 0f);
                yield return Wait(0.9f);
                Say("Devem ter caído por aqui, neste templo. Preciso achar as três.", 4.2f);
                yield return Move(14f, 0f, 0f, 0f, eyeHeight, eyeHeight, 2f, 0f, fov0);
                yield return Wait(1.2f);
            }
            else if (!skip)
            {
                Say("Preciso descobrir onde estou... e como voltar.", 3.4f);
                yield return Wait(2.6f);
            }
            Finish();
        }

        // --------------------------------------------------------------------------------------- preparação
        void Setup()
        {
            if (fps != null) fps.enabled = false;
            var combat = player.GetComponent<PlayerCombat>(); if (combat != null) combat.enabled = false;
            var inter = player.GetComponent<PlayerInteractor>(); if (inter != null) inter.enabled = false;
            hud.SetGameplayVisible(false);

            arms = cam.transform.Find("Arms");
            ShowArms(false);          // as mãos ficam escondidas enquanto ele está deitado/tonto e voltam quando ele começa a andar
            fovBase = cam.fieldOfView;
            basePos = cam.transform.localPosition;
            baseRot = cam.transform.localRotation;
            float ground = cam.transform.position.y - 1.7f;
            float bestY = float.NegativeInfinity;
            foreach (RaycastHit h in Physics.RaycastAll(cam.transform.position, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(player) && h.normal.y > 0.5f && h.point.y > bestY) bestY = h.point.y;
            if (!float.IsNegativeInfinity(bestY)) ground = bestY;
            eyeHeight = Mathf.Max(1.2f, cam.transform.position.y - ground);
            height = eyeHeight;

            // pálpebras (tela cheia, por cima de tudo): só preto
            var cgo = new GameObject("WakeCanvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 350;
            var lgo = new GameObject("Palpebras", typeof(RectTransform));
            lgo.transform.SetParent(cgo.transform, false);
            lids = lgo.AddComponent<Image>();
            lids.raycastTarget = false;
            var rt = lids.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            Shader sh = Shader.Find("RiftOfTime/EyeLid");
            if (sh != null) { lidMat = new Material(sh); lids.material = lidMat; lids.color = Color.white; }
            else lids.color = Color.black;
            var ggo = new GameObject("Clarao", typeof(RectTransform));
            ggo.transform.SetParent(cgo.transform, false);
            glare = ggo.AddComponent<Image>();
            glare.raycastTarget = false;
            glare.color = new Color(1f, 0.98f, 0.92f, 0f);
            var grt = glare.rectTransform; grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = grt.offsetMax = Vector2.zero;

            // visão: desfoque, vinheta e exposição (Volume global do URP), sem cor
            var vgo = new GameObject("WakeVolume");
            volume = vgo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 80;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            dof = prof.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0f);
            dof.gaussianEnd.Override(0.2f);
            dof.gaussianMaxRadius.Override(1.5f);
            vignette = prof.Add<Vignette>(true);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.6f);
            vignette.smoothness.Override(0.7f);
            color = prof.Add<ColorAdjustments>(true);
            color.postExposure.Override(2.5f);
            color.saturation.Override(-35f);
            volume.sharedProfile = prof;

            // som: abafado + zumbido no ouvido + batimento
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener != null)
            {
                lowPass = listener.gameObject.GetComponent<AudioLowPassFilter>();
                if (lowPass == null) lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
                lowPass.cutoffFrequency = 520f;
            }
            ringing = gameObject.AddComponent<AudioSource>();
            ringing.clip = MakeRinging();
            ringing.loop = true; ringing.spatialBlend = 0f; ringing.volume = 0.18f;
            ringing.Play();
            if (AudioDirector.Instance != null) AudioDirector.Instance.Loop("wake_pulse", "heartbeat", 0.9f);

            open = 0f; blur = 1f; exposure = 2.5f; vig = 0.6f;
            ApplyEffects();
        }

        // a máquina está quebrada e APAGADA: sem animação do modelo, sem luzes nem partículas, nada que a ative
        static void FreezeMachine(Transform machine)
        {
            if (machine == null) return;
            foreach (var a in machine.GetComponentsInChildren<Animation>(true)) { a.Stop(); a.enabled = false; }
            foreach (var a in machine.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var t in machine.GetComponentsInChildren<TimeMachine>(true)) t.SetAnimationSpeed(0f);
            foreach (var ps in machine.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var l in machine.GetComponentsInChildren<Light>(true)) l.enabled = false;
        }

        // --------------------------------------------------------------------------------------- queda
        IEnumerator Fall()
        {
            var wind = gameObject.AddComponent<AudioSource>();
            wind.clip = MakeWind();
            wind.loop = true; wind.spatialBlend = 0f; wind.volume = 0f; wind.pitch = 0.7f;
            wind.Play();
            if (ringing != null) ringing.volume = 0f;
            float dur = 2.6f;
            for (float t = 0f; t < dur && !skip; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                wind.volume = Mathf.Lerp(0.05f, 0.85f, k * k);
                wind.pitch = Mathf.Lerp(0.7f, 1.7f, k);
                if (lowPass != null) lowPass.cutoffFrequency = 22000f;
                CameraShake.Hold(Mathf.Lerp(0f, 0.05f, k));
                Apply();
                yield return null;
            }
            CameraShake.Hold(0f);
            wind.Stop(); Destroy(wind);
            if (!skip)
            {
                var go = new GameObject("Impacto");
                var src = go.AddComponent<AudioSource>();
                AudioClip thump = Resources.Load<AudioClip>("Sfx/queda");
                src.clip = thump != null ? thump : MakeImpact(); src.spatialBlend = 0f; src.volume = 1f; src.Play();
                Destroy(go, 3f);
                CameraShake.Kick(0.3f, 0.7f);
                glareAlpha = 0.9f;
                if (lowPass != null) lowPass.cutoffFrequency = 520f;
                if (ringing != null) ringing.volume = 0.18f;
            }
            for (float t = 0f; t < 0.9f && !skip; t += Time.unscaledDeltaTime) { Apply(); yield return null; }
        }

        // --------------------------------------------------------------------------------------- pisca
        IEnumerator Blink(float peak, float openTime, float hold, float closeTime)
        {
            float from = open;
            glareAlpha = peak < 0.3f ? 0.5f : 0.2f;
            for (float t = 0f; t < openTime && !skip; t += Time.unscaledDeltaTime)
            {
                open = Mathf.Lerp(from, peak, Mathf.SmoothStep(0f, 1f, t / openTime));
                Settle(peak); Apply();
                yield return null;
            }
            open = peak;
            for (float t = 0f; t < hold && !skip; t += Time.unscaledDeltaTime)
            {
                open = peak + (Mathf.PerlinNoise(Time.unscaledTime * 9f, 1.3f) - 0.5f) * 0.05f;
                Settle(peak); Apply();
                yield return null;
            }
            for (float t = 0f; t < closeTime && !skip; t += Time.unscaledDeltaTime)
            {
                open = Mathf.Lerp(peak, 0f, Mathf.SmoothStep(0f, 1f, t / closeTime));
                Settle(peak * 0.7f); Apply();
                yield return null;
            }
            open = 0f;
            Apply();
        }

        IEnumerator OpenFully(float seconds)
        {
            float from = open;
            for (float t = 0f; t < seconds && !skip; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                open = Mathf.Lerp(from, 1f, k);
                Settle(1f); Apply();
                yield return null;
            }
            open = 1f;
            Apply();
        }

        // quanto mais aberto, menos embaçado, menos estourado de luz e menos vinheta
        void Settle(float openness)
        {
            float target = Mathf.Clamp01(openness);
            blur = Mathf.MoveTowards(blur, Mathf.Lerp(1f, 0.25f, target), Time.unscaledDeltaTime * 0.7f);
            exposure = Mathf.MoveTowards(exposure, Mathf.Lerp(2.5f, 0.3f, target), Time.unscaledDeltaTime * 1.4f);
            vig = Mathf.MoveTowards(vig, Mathf.Lerp(0.6f, 0.38f, target), Time.unscaledDeltaTime * 0.25f);
            glareAlpha = Mathf.MoveTowards(glareAlpha, 0f, Time.unscaledDeltaTime * 0.45f);
        }

        // --------------------------------------------------------------------------------------- câmera e corpo
        void Say(string text, float seconds) { if (!skip && hud != null) hud.ShowDialogue(text, seconds); }

        // as mãos ficam ativas (o PunchAnimator monta o braço com a câmera na pose normal); só somem da tela
        readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<Light> hiddenLights = new System.Collections.Generic.List<Light>();
        bool armsHidden;

        // ao voltar, os braços sobem de baixo da tela em vez de aparecerem do nada
        float riseT = 1f;
        Vector3 riseApplied;

        void ShowArms(bool on)
        {
            if (on && armsHidden) riseT = 0f;
            armsHidden = !on;
            if (on)
            {
                foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
                foreach (Light l in hiddenLights) if (l != null) l.enabled = true;
                hiddenRenderers.Clear();
                hiddenLights.Clear();
            }
            else HideArmParts();
        }

        void HideArmParts()
        {
            if (arms == null) return;
            foreach (Renderer r in arms.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && !(r is ParticleSystemRenderer)) { r.enabled = false; hiddenRenderers.Add(r); }
            foreach (Light l in arms.GetComponentsInChildren<Light>(false))
                if (l.enabled) { l.enabled = false; hiddenLights.Add(l); }
        }

        void LateUpdate()
        {
            if (armsHidden) HideArmParts();
            if (arms == null) return;
            riseT = Mathf.MoveTowards(riseT, 1f, Time.unscaledDeltaTime / 1.6f);
            float k = 1f - Mathf.SmoothStep(0f, 1f, riseT);
            arms.localPosition -= riseApplied;
            riseApplied = armsHidden ? Vector3.zero : Vector3.down * 0.7f * k;
            arms.localPosition += riseApplied;
        }

        void OnDestroy()
        {
            if (arms != null) arms.localPosition -= riseApplied;
            riseApplied = Vector3.zero;
        }

        void SetPose(float p, float y, float r, float h) { pitch = p; yaw = y; roll = r; height = h; }

        // anima a pose da câmera (pitch, yaw ou roll, altura) com tontura (balanço) de amplitude 'dizzy'
        // a0/a1: roll quando a altura muda (sentar/levantar), yaw quando a altura é igual (olhar em volta)
        IEnumerator Move(float p0, float p1, float a0, float a1, float h0, float h1, float seconds, float dizzy, float fovTarget = -1f)
        {
            float fov0 = cam.fieldOfView;
            bool rollMode = Mathf.Abs(h1 - h0) > 0.3f;
            for (float t = 0f; t < seconds && !skip; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                pitch = Mathf.Lerp(p0, p1, k);
                height = Mathf.Lerp(h0, h1, k);
                float a = Mathf.Lerp(a0, a1, k);
                if (rollMode) roll = a; else { yaw = a; roll = 0f; }
                float sway = Mathf.Sin(Time.unscaledTime * 2.3f) * dizzy * Mathf.Sin(k * Mathf.PI);
                roll += sway * 0.9f;
                yaw += Mathf.Sin(Time.unscaledTime * 1.7f + 1f) * dizzy * 0.8f * Mathf.Sin(k * Mathf.PI);
                if (fovTarget > 0f) cam.fieldOfView = Mathf.Lerp(fov0, fovTarget, k);
                Settle(1f);
                Apply();
                yield return null;
            }
            if (fovTarget > 0f) cam.fieldOfView = fovTarget;
            pitch = p1; height = h1;
            if (rollMode) roll = a1; else { yaw = a1; roll = 0f; }
            Apply();
        }

        // gira o corpo do jogador em 'degrees' (a câmera, filha dele, acompanha)
        void FaceBody(float degrees)
        {
            Quaternion r = Quaternion.Euler(0f, player.eulerAngles.y + degrees, 0f);
            if (rb != null) rb.rotation = r;
            player.rotation = r;
        }

        // anda em linha reta até 'stopDistance' da máquina, devagar e balançando
        IEnumerator WalkTo(Transform machine, float stopDistance)
        {
            Vector3 flat = machine.position - player.position; flat.y = 0f;
            float total = Mathf.Max(0f, flat.magnitude - stopDistance);
            if (total < 0.2f) yield break;
            Vector3 dir = flat.normalized;
            ShowArms(true);
            ScriptedWalk.Active = true;
            bool prevKin = rb != null && rb.isKinematic;
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
            float walked = 0f, speed = 0f;
            while (walked < total && !skip)
            {
                float dt = Time.unscaledDeltaTime;
                speed = Mathf.MoveTowards(speed, walkSpeed, dt * 2.5f);
                float slow = Mathf.Clamp01((total - walked) / 1.2f);
                float step = speed * Mathf.Lerp(0.35f, 1f, slow) * dt;
                if (Physics.Raycast(player.position + Vector3.up * 0.6f, dir, out RaycastHit hit, 0.7f, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(player) && !hit.transform.IsChildOf(machine)) break;
                player.position += dir * step;
                walked += step;
                float bob = Mathf.Sin(Time.unscaledTime * 6.2f);
                roll = Mathf.Sin(Time.unscaledTime * 3.1f) * 1.2f;
                height = eyeHeight + bob * 0.025f * (speed / walkSpeed);
                Settle(1f); Apply();
                yield return null;
            }
            ScriptedWalk.Active = false;
            if (rb != null) { rb.isKinematic = prevKin; rb.linearVelocity = Vector3.zero; }
            roll = 0f; height = eyeHeight;
            Apply();
        }

        void Apply()
        {
            if (skip) return;
            if (Keyboard.current != null && Time.unscaledTime - startedAt > 1.5f &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)) { skip = true; return; }
            Vector3 lp = basePos;
            lp.y = basePos.y - (eyeHeight - height);
            cam.transform.localPosition = lp;
            cam.transform.localRotation = Quaternion.Euler(pitch, yaw, roll);
            ApplyEffects();
        }

        void ApplyEffects()
        {
            if (lidMat != null)
            {
                lidMat.SetFloat("_Open", open);
                lidMat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
                lidMat.SetFloat("_Feather", Mathf.Lerp(0.05f, 0.16f, open));
            }
            else if (lids != null) lids.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(open * 1.6f));
            if (glare != null) { var c = glare.color; c.a = glareAlpha * Mathf.Clamp01(open * 2f); glare.color = c; }
            if (dof != null) { dof.active = blur > 0.06f; dof.gaussianMaxRadius.value = Mathf.Lerp(0.5f, 1.5f, blur); }
            if (vignette != null) vignette.intensity.value = vig;
            if (color != null) color.postExposure.value = exposure;
            if (lowPass != null) lowPass.cutoffFrequency = Mathf.Lerp(520f, 22000f, Mathf.Pow(Mathf.Clamp01(1f - blur + 0.05f), 1.5f));
            if (ringing != null) ringing.volume = Mathf.Lerp(0.18f, 0f, 1f - blur);
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !skip; t += Time.unscaledDeltaTime)
            {
                Apply();
                yield return null;
            }
        }

        // --------------------------------------------------------------------------------------- fim
        void Finish()
        {
            if (cam != null)
            {
                cam.transform.localPosition = basePos;
                cam.transform.localRotation = baseRot;
                cam.fieldOfView = fovBase;
            }
            if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; }
            ScriptedWalk.Active = false;
            ShowArms(true);
            SyncController();
            if (fps != null) fps.enabled = true;
            var combat = player.GetComponent<PlayerCombat>(); if (combat != null) combat.enabled = true;
            var inter = player.GetComponent<PlayerInteractor>(); if (inter != null) inter.enabled = true;
            if (hud != null) hud.SetGameplayVisible(true);
            if (AudioDirector.Instance != null) AudioDirector.Instance.StopLoop("wake_pulse");
            if (lowPass != null) Destroy(lowPass);
            if (volume != null) Destroy(volume.gameObject);
            if (canvas != null) Destroy(canvas.gameObject);
            if (ringing != null) Destroy(ringing);
            if (lidMat != null) Destroy(lidMat);
            Destroy(this);
        }

        // o controlador guarda a direção em campos privados: ajusta para a direção atual do corpo,
        // senão ao ligar ele giraria o corpo de volta para o ângulo antigo
        void SyncController()
        {
            if (fps == null) return;
            float y = player.eulerAngles.y;
            var t = fps.GetType();
            const BindingFlags f = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var fy = t.GetField("targetRotationY", f); if (fy != null) fy.SetValue(fps, y);
            var fx = t.GetField("targetRotationX", f); if (fx != null) fx.SetValue(fps, 0f);
            var fb = t.GetField("bodyTargetRot", f); if (fb != null) fb.SetValue(fps, Quaternion.Euler(0f, y, 0f));
        }

        static Transform FindMachine()
        {
            var tm = FindFirstObjectByType<TimeMachine>();
            if (tm != null) return tm.transform;
            var go = GameObject.Find("Máquina do Tempo (quebrada)");
            return go != null ? go.transform : null;
        }

        // --------------------------------------------------------------------------------------- sons gerados
        static AudioClip MakeRinging()
        {
            const int rate = 22050;
            int n = rate * 2;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 5400f * t) * 0.25f) * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 1f * t));
            }
            var clip = AudioClip.Create("Zumbido", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static AudioClip MakeWind()
        {
            const int rate = 22050;
            int n = rate * 3;
            var d = new float[n];
            var rng = new System.Random(4);
            float lp = 0f, bp = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.18f * (w - lp);
                bp += 0.55f * (lp - bp);
                float gust = 0.65f + 0.35f * Mathf.Sin(i * 2f * Mathf.PI / rate * 1.3f);
                d[i] = Mathf.Clamp(bp * 3.2f * gust, -1f, 1f);
            }
            var clip = AudioClip.Create("Vento", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static AudioClip MakeImpact()
        {
            const int rate = 22050;
            int n = (int)(1.6f * rate);
            var d = new float[n];
            var rng = new System.Random(9);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 32f, Mathf.Clamp01(t / 0.35f)) * t) * Mathf.Exp(-t * 4.2f);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.22f * (w - lp);
                float sand = lp * Mathf.Exp(-t * 6f) * 0.7f * Mathf.Clamp01(t / 0.01f);
                float crack = w * Mathf.Exp(-t * 80f) * 0.6f;
                d[i] = Mathf.Clamp(boom + sand + crack, -1f, 1f);
            }
            var clip = AudioClip.Create("Impacto", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
