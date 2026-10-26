using System.Collections;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Trecho do chão que desce (transição Fase 2 -> Fase 3: "chão se abre para o subterrâneo").</summary>
    public class TrapdoorFloor : MonoBehaviour
    {
        public float dropDistance = 12f;

        public void Open(float seconds)
        {
            StartCoroutine(OpenRoutine(seconds));
        }

        IEnumerator OpenRoutine(float seconds)
        {
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.down * dropDistance;
            AudioDirector.Sfx("rumble", 1f);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                transform.position = Vector3.Lerp(start, end, k);
                yield return null;
            }
        }
    }
}
