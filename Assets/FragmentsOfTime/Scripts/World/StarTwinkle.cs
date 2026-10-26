using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Faz os filhos "piscarem" como estrelas (tela inicial: estrelas piscando).</summary>
    public class StarTwinkle : MonoBehaviour
    {
        Transform[] stars;
        Vector3[] baseScale;
        float[] phase, speed;

        void Start()
        {
            int n = transform.childCount;
            stars = new Transform[n];
            baseScale = new Vector3[n];
            phase = new float[n];
            speed = new float[n];
            for (int i = 0; i < n; i++)
            {
                stars[i] = transform.GetChild(i);
                baseScale[i] = stars[i].localScale;
                phase[i] = Random.value * 6.28f;
                speed[i] = Random.Range(1.2f, 3.5f);
            }
        }

        void Update()
        {
            for (int i = 0; i < stars.Length; i++)
            {
                float k = 0.55f + 0.45f * Mathf.Sin(Time.time * speed[i] + phase[i]);
                stars[i].localScale = baseScale[i] * k;
            }
        }
    }
}
