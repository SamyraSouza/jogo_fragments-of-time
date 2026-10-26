using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Câmera de cena: sai do jogador, dá zoom e gira devagar em volta de um alvo; ao terminar, volta para o jogador.</summary>
    public class CinematicCam : MonoBehaviour
    {
        Transform target;
        Camera cam;
        Vector3 homeLocalPos, fromPos, lastPos;
        Quaternion homeLocalRot, fromRot, lastRot;
        float radius, height, lookHeight, fov, orbitSpeed, angle, blend, blendDir, homeFov;
        readonly List<Renderer> hidden = new List<Renderer>();
        bool active;

        public static CinematicCam On(Camera c)
        {
            CinematicCam cc = c.GetComponent<CinematicCam>();
            if (cc == null) cc = c.gameObject.AddComponent<CinematicCam>();
            return cc;
        }

        public void Begin(Transform t, float radius, float height, float lookHeight, float fov, float orbitDegPerSec)
        {
            cam = GetComponent<Camera>();
            if (!active)
            {
                homeLocalPos = transform.localPosition;
                homeLocalRot = transform.localRotation;
                homeFov = cam.fieldOfView;
                // as mãos/adaga ficam escondidas enquanto a câmera está fora do corpo
                HideBody();
                Vector3 d = transform.position - t.position; d.y = 0f;
                angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                blend = 0f;
            }
            fromPos = transform.position;
            fromRot = transform.rotation;
            target = t; this.radius = radius; this.height = height; this.lookHeight = lookHeight; this.fov = fov; orbitSpeed = orbitDegPerSec;
            blendDir = 1f; active = true;
        }

        public void End() { blendDir = -1f; }

        float nextHide;

        /// <summary>Ponto da órbita; se houver parede no caminho ou nenhum chão embaixo (fora do mapa), a câmera chega mais perto do alvo.</summary>
        Vector3 OrbitPoint(float r)
        {
            Vector3 focus = target.position + Vector3.up * Mathf.Min(lookHeight, height + 1f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = target.position + dir * r + Vector3.up * height;
            for (; r > 1.5f; r -= 0.75f)
            {
                p = target.position + dir * r + Vector3.up * height;
                if (!Blocked(focus, p) && HasGroundBelow(p)) return p;
            }
            return target.position + dir * 1.5f + Vector3.up * height;
        }

        bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            foreach (RaycastHit h in Physics.SphereCastAll(from, 0.25f, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(target) || h.transform.root == transform.root) continue;
                return true;
            }
            return false;
        }

        bool HasGroundBelow(Vector3 p)
        {
            foreach (RaycastHit h in Physics.RaycastAll(p, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(target) && h.transform.root != transform.root) return true;
            return false;
        }

        /// <summary>Esconde tudo do jogador (braços, itens, corpo): fora da câmera, nada dele aparece.</summary>
        void HideBody()
        {
            foreach (Renderer r in transform.root.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) { r.enabled = false; hidden.Add(r); }
        }

        void LateUpdate()
        {
            if (!active) return;
            blend = Mathf.Clamp01(blend + blendDir * Time.deltaTime / 1.2f);
            float k = Mathf.SmoothStep(0f, 1f, blend);

            if (blendDir < 0f)
            {
                Transform par = transform.parent;
                Vector3 hp = par != null ? par.TransformPoint(homeLocalPos) : homeLocalPos;
                Quaternion hr = par != null ? par.rotation * homeLocalRot : homeLocalRot;
                transform.SetPositionAndRotation(Vector3.Lerp(hp, lastPos, k), Quaternion.Slerp(hr, lastRot, k));
                cam.fieldOfView = Mathf.Lerp(homeFov, fov, k);
                if (blend <= 0f)
                {
                    active = false;
                    cam.fieldOfView = homeFov;
                    transform.localPosition = homeLocalPos;
                    transform.localRotation = homeLocalRot;
                    foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                    hidden.Clear();
                    Destroy(this);
                }
                return;
            }

            if (Time.time >= nextHide) { nextHide = Time.time + 0.2f; HideBody(); }
            if (target == null) return;
            angle += orbitSpeed * Time.deltaTime;
            Vector3 p = OrbitPoint(radius);
            Quaternion r2 = Quaternion.LookRotation(target.position + Vector3.up * lookHeight - p);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, p, k), Quaternion.Slerp(fromRot, r2, k));
            lastPos = transform.position; lastRot = transform.rotation;
            cam.fieldOfView = Mathf.Lerp(homeFov, fov, k);
        }
    }
}
