using UnityEngine;

namespace Drakantus
{
    /// <summary>Faz a luz da tocha tremular: duas frequências de ruído Perlin + quedas rápidas ocasionais.</summary>
    public class TorchFlicker : MonoBehaviour
    {
        public float baseIntensity = 2.5f;
        public float baseRange;          // 0 = não mexe no alcance
        public float amount = 0.18f;
        public float speed = 6f;
        public float seed;
        Light l;

        void Awake() { l = GetComponent<Light>(); }

        void Update()
        {
            if (l == null) return;
            float t = Time.time;
            float slow = Mathf.PerlinNoise(seed, t * speed * 0.35f) - 0.5f;
            float fast = Mathf.PerlinNoise(seed + 7.3f, t * speed * 2.2f) - 0.5f;
            float n = slow * 1.3f + fast * 0.7f;
            // "engasgo" da chama de vez em quando
            float gust = Mathf.PerlinNoise(seed + 19.1f, t * 0.9f);
            if (gust > 0.72f) n -= (gust - 0.72f) * 2.2f;
            float k = 1f + n * 2f * amount;
            l.intensity = baseIntensity * Mathf.Max(0.2f, k);
            if (baseRange > 0f) l.range = baseRange * (1f + n * 0.25f * amount);
        }
    }
}
