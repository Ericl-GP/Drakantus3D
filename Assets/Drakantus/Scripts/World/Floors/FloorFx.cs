using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Partículas de ambiente montadas quando o jogo roda (neve, areia ao vento, brasas, vaga-lumes,
    /// poeira, cinzas, fumaça, névoa rasteira, folhas, chama de tocha). No editor aparece só o gizmo.
    /// </summary>
    public class FloorFx : MonoBehaviour
    {
        public FloorFxKind kind = FloorFxKind.Poeira;
        public Vector3 size = new Vector3(12, 4, 12);
        public Color color = Color.white;
        [Range(1, 400)] public int amount = 40;
        public float scale = 1f;

        ParticleSystem ps;

        void Start()
        {
            if (!Application.isPlaying) return;
            Build();
        }

        public void Build()
        {
            if (ps != null) return;
            switch (kind)
            {
                case FloorFxKind.Chama:
                    ps = LevelDecor.Flame(transform, Vector3.zero, Mathf.Max(0.3f, scale), color, Mathf.Clamp(amount, 6, 40));
                    break;
                case FloorFxKind.Vagalumes:
                    ps = LevelDecor.Motes(transform, Vector3.zero, size, color, amount, 0.02f);
                    break;
                case FloorFxKind.Brasas:
                    ps = LevelDecor.Motes(transform, Vector3.zero, size, color, amount, 0.25f);
                    break;
                case FloorFxKind.Poeira:
                    ps = LevelDecor.Dust(transform, Vector3.zero, size, color, amount);
                    break;
                default:
                    ps = Custom();
                    break;
            }
        }

        ParticleSystem Custom()
        {
            bool additive = kind == FloorFxKind.Faiscas;
            var go = new GameObject("Particulas_" + kind);
            go.transform.SetParent(transform, false);
            var p = go.AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = p.main;
            m.loop = true;
            m.playOnAwake = true;
            m.prewarm = true;
            m.duration = 5f;
            m.maxParticles = Mathf.Max(4, amount);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startColor = color;
            var sh = p.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = size;
            var em = p.emission;
            var no = p.noise;
            float life = 5f;
            switch (kind)
            {
                case FloorFxKind.Neve:
                    life = Mathf.Max(2f, size.y / 1.1f);
                    go.transform.localPosition = new Vector3(0, size.y * 0.5f, 0);
                    sh.scale = new Vector3(size.x, 0.2f, size.z);
                    m.startSpeed = 0f;
                    m.gravityModifier = 0.06f;
                    m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                    no.enabled = true; no.strength = 0.35f; no.frequency = 0.25f;
                    break;
                case FloorFxKind.Areia:
                    life = 3f;
                    m.startSpeed = 0f;
                    m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.5f);
                    var vel = p.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                    vel.x = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
                    vel.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.4f);
                    vel.z = new ParticleSystem.MinMaxCurve(1f, 2.5f);
                    no.enabled = true; no.strength = 0.6f; no.frequency = 0.4f;
                    break;
                case FloorFxKind.Cinzas:
                    life = 7f;
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                    m.gravityModifier = 0.01f;
                    m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
                    no.enabled = true; no.strength = 0.3f; no.frequency = 0.2f;
                    break;
                case FloorFxKind.Fumaca:
                    life = 6f;
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                    m.gravityModifier = -0.04f;
                    m.startSize = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.6f * scale);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    sh.shapeType = ParticleSystemShapeType.Box;
                    var sz = p.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.6f));
                    break;
                case FloorFxKind.Nevoa:
                    life = 9f;
                    sh.scale = new Vector3(size.x, 0.4f, size.z);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.15f);
                    m.startSize = new ParticleSystem.MinMaxCurve(2.5f * scale, 4.5f * scale);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    break;
                case FloorFxKind.Folhas:
                    life = 6f;
                    go.transform.localPosition = new Vector3(0, size.y * 0.5f, 0);
                    sh.scale = new Vector3(size.x, 0.2f, size.z);
                    m.startSpeed = 0f;
                    m.gravityModifier = 0.03f;
                    m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    no.enabled = true; no.strength = 0.6f; no.frequency = 0.3f;
                    break;
                case FloorFxKind.Faiscas:
                    life = 1.2f;
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
                    m.gravityModifier = 0.4f;
                    m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                    break;
            }
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            em.rateOverTime = Mathf.Max(1f, amount / life);
            var col = p.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            float a = color.a;
            gr.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
                       new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(a, 0.2f), new GradientAlphaKey(a, 0.75f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = U.Fx(additive);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (kind == FloorFxKind.Fumaca || kind == FloorFxKind.Nevoa) r.maxParticleSize = 3f;
            p.Play();
            return p;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.35f);
            if (kind == FloorFxKind.Chama) Gizmos.DrawWireSphere(transform.position, 0.25f * Mathf.Max(0.5f, scale));
            else Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
