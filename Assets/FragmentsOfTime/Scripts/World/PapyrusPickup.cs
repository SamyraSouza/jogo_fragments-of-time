using System;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Papiro que conta a história da pirâmide (Fase 1, conforme o relatório).</summary>
    public class PapyrusPickup : Interactable
    {
        public string title = "O Papiro da Fenda";
        [TextArea(4, 12)]
        public string body =
            "Este templo guarda a máquina que nossos ancestrais construíram para selar uma fenda natural no tempo, " +
            "sob a pirâmide.\n\nSe o selo for rompido, qualquer coisa pode atravessar as eras sem controle.\n\n" +
            "Reúna os fragmentos, reconstrua a máquina e faça a travessia segura de volta.";

        /// <summary>Se definida, o papiro mostra só esta imagem (QR code) em vez do texto.</summary>
        public Texture qrImage;

        public event Action Read;
        bool hasRead;

        protected virtual void Awake()
        {
            Material parchment = RotMaterials.Lit(new Color(0.9f, 0.82f, 0.6f));
            Material wood = RotMaterials.Lit(new Color(0.4f, 0.28f, 0.15f));
            Transform scroll = ProcBuild.Primitive(transform, "Scroll", PrimitiveType.Cylinder,
                new Vector3(0f, 1.15f, 0f), new Vector3(0.18f, 0.35f, 0.18f), parchment);
            scroll.localRotation = Quaternion.Euler(0f, 0f, 90f);
            ProcBuild.Primitive(transform, "RodL", PrimitiveType.Cylinder, new Vector3(-0.37f, 1.15f, 0f), new Vector3(0.05f, 0.02f, 0.05f), wood).localRotation = Quaternion.Euler(0f, 0f, 90f);
            ProcBuild.Primitive(transform, "RodR", PrimitiveType.Cylinder, new Vector3(0.37f, 1.15f, 0f), new Vector3(0.05f, 0.02f, 0.05f), wood).localRotation = Quaternion.Euler(0f, 0f, 90f);
            ProcBuild.Primitive(transform, "Pedestal", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 0.5f, 0.7f),
                RotMaterials.Lit(new Color(0.55f, 0.48f, 0.38f)));
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.5f);
            l.range = 5f;
            l.intensity = 2f;
        }

        public override string Prompt { get { return "[E] Ler o papiro"; } }

        public override void Interact(PlayerInteractor who)
        {
            if (HUD.Instance == null) return;
            if (qrImage != null) HUD.Instance.ShowReadableImage(qrImage);
            else HUD.Instance.ShowReadable(title, body.Replace("As três peças do circuito foram escondidas por toda a pirâmide, cada uma vigiada por uma múmia guardiã. ", ""));
            if (!hasRead)
            {
                hasRead = true;
                if (Read != null) Read();
            }
        }
    }
}
