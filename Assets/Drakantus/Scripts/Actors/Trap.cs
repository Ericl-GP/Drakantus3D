using UnityEngine;

namespace Drakantus
{
    /// <summary>Armadilha explosiva do arqueiro: arma em 0,4 s e explode quando um inimigo encosta.</summary>
    public static class Trap
    {
        public static void Spawn(Vector3 pos, SkillDef s, int dmg)
        {
            // apoia no chão
            Vector3 from = pos + Vector3.up * 2f;
            var hits = Physics.RaycastAll(from, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider == null || Projectile.IsActor(h.collider)) continue;
                if (h.distance < best) { best = h.distance; pos = h.point; }
            }
            var go = new GameObject("armadilha");
            Transform parent = Game.I != null && Game.I.levelRoot != null ? Game.I.levelRoot : FX.Root;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.AddComponent<TrapObject>();
            t.Setup(s, dmg);
        }
    }

    /// <summary>Componente da armadilha no chão.</summary>
    public class TrapObject : MonoBehaviour
    {
        int dmg;
        float age, life = 20f, trigger = 1.1f, radius = 2.5f, stun;
        bool armed, done;
        Color color;
        Light glow;
        GameObject ring;

        public void Setup(SkillDef s, int dmg)
        {
            this.dmg = Mathf.Max(1, dmg);
            if (s != null)
            {
                if (s.life > 0f) life = s.life;
                if (s.trigger > 0f) trigger = s.trigger;
                if (s.radius > 0f) radius = s.radius;
                stun = s.stun;
            }
            color = s != null && !string.IsNullOrEmpty(s.color) ? U.Hex(s.color) : U.Hex("ffb347");

            var plate = U.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.75f, 0.03f, 0.75f), U.Hex("4a4440"));
            var core = U.Prim(PrimitiveType.Sphere, transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.28f, 0.12f, 0.28f), color);
            core.GetComponent<Renderer>().sharedMaterial = U.Lit(color, 0.6f, color * 2.5f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var spike = U.Prim(PrimitiveType.Cube, transform, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.08f, 0.3f), new Vector3(0.06f, 0.12f, 0.06f), U.Hex("8a8480"));
                spike.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (plate != null) plate.name = "base";

            ring = FX.FlatQuad("raio", transform.position + Vector3.up * 0.04f, U.RingTexture(), new Color(color.r, color.g, color.b, 0.35f), true);
            ring.transform.SetParent(transform, true);
            ring.transform.localScale = Vector3.one * trigger * 2f;

            var lg = new GameObject("luz");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            glow = lg.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = 3f;
            glow.intensity = 0.8f;
            glow.shadows = LightShadows.None;
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            age += dt;
            if (!armed && age >= 0.4f)
            {
                armed = true;
                FX.Ring(transform.position, trigger, color, 0.3f, 0.3f);
            }
            if (glow != null) glow.intensity = armed ? 1.2f + 0.8f * Mathf.Sin(Time.time * 8f) : 0.5f;
            if (ring != null) FX.SetColor(ring.GetComponent<Renderer>(), new Color(color.r, color.g, color.b, armed ? 0.35f + 0.2f * Mathf.Sin(Time.time * 6f) : 0.15f));

            if (armed && Game.I != null)
            {
                foreach (var e in Game.I.enemies)
                {
                    if (e == null || e.dead) continue;
                    if (U.Flat(e.transform.position - transform.position).magnitude <= trigger + e.radius) { Explode(); return; }
                }
            }
            if (age >= life)
            {
                done = true;
                FX.Dust(transform.position, U.Hex("a09080"), 0.6f);
                Destroy(gameObject);
            }
        }

        void Explode()
        {
            done = true;
            Vector3 c = transform.position;
            FX.Burst(c + Vector3.up * 0.5f, color, 1.4f, 40);
            FX.Burst(c + Vector3.up * 0.3f, U.Hex("ffb347"), 1f, 20);
            FX.Ring(c, radius, color, 0.4f);
            FX.Dust(c, U.Hex("6a5a4a"), 1.4f);
            Sfx.Play("explosion", c);
            var g = Game.I;
            if (g != null)
            {
                g.Shake(0.3f);
                bool any = false;
                foreach (var e in g.enemies.ToArray())
                {
                    if (e == null || e.dead) continue;
                    if (U.Flat(e.transform.position - c).magnitude > radius + e.radius) continue;
                    e.TakeHit(dmg, c, false);
                    if (stun > 0f && !e.dead) e.Stun(stun);
                    any = true;
                }
                if (any) g.Hitstop(0.05f);
            }
            Breakable.HitArea(c, radius, dmg);   // [Quebraveis] a explosão também quebra barris/caixas (e detona os explosivos)
            Destroy(gameObject);
        }
    }
}
