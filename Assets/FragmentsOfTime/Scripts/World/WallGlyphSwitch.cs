using System;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>O desenho (olho de Hórus) da parede de hieróglifos: ao tocá-lo, a porta se abre. Só funciona depois de armado.</summary>
    public class WallGlyphSwitch : Interactable
    {
        public event Action Pressed;
        bool armed, used;

        public static WallGlyphSwitch Create(Vector3 pos)
        {
            var g = new GameObject("Desenho da Parede");
            g.transform.position = pos;
            return g.AddComponent<WallGlyphSwitch>();
        }

        public void Arm() { armed = true; }

        public override bool IsAvailable { get { return armed && !used; } }
        public override float Radius { get { return 3.4f; } }
        public override string Prompt { get { return "[E] Tocar no desenho"; } }

        public override void Interact(PlayerInteractor who)
        {
            if (used) return;
            used = true;
            AudioDirector.Sfx("rumble", 0.8f);
            if (Pressed != null) Pressed();
        }
    }
}
