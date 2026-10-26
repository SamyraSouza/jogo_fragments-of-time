using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Base para objetos com os quais o jogador interage apertando [E].</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        public virtual bool IsAvailable { get { return true; } }
        public virtual float Radius { get { return 2.6f; } }
        public abstract string Prompt { get; }
        public abstract void Interact(PlayerInteractor who);
    }
}
