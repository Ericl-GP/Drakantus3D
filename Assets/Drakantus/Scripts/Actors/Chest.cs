using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Baú de chefe: cai quicando, brilha e abre quando o herói encosta (GameState.RollChest).</summary>
    public class Chest : MonoBehaviour
    {
        public int tier = 1;
        public bool opened;

        Transform model;
        Light glow;
        GameObject ring;
        float vy, groundY, sparkleClock, punch;
        int bounces;
        bool landed;

        public static Chest Spawn(Vector3 pos, int tier)
        {
            var go = new GameObject("Bau");
            Transform parent = Game.I != null && Game.I.levelRoot != null ? Game.I.levelRoot : null;
            if (parent != null) go.transform.SetParent(parent, false);
            float gy = Ground(pos);
            go.transform.position = new Vector3(pos.x, gy + 3.5f, pos.z);
            float yaw = 225f;
            if (Game.I != null && Game.I.player != null)
            {
                Vector3 to = U.Flat(Game.I.player.transform.position - pos);
                if (to.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            }
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var c = go.AddComponent<Chest>();
            c.tier = Mathf.Max(1, tier);
            c.groundY = gy;
            c.Build();
            return c;
        }

        static float Ground(Vector3 pos)
        {
            float y = pos.y;
            var hits = Physics.RaycastAll(pos + Vector3.up * 2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider == null || Projectile.IsActor(h.collider)) continue;
                if (h.distance < best) { best = h.distance; y = h.point.y; }
            }
            return y;
        }

        void Build()
        {
            var g = Models.SpawnOr("chest", transform, Vector3.zero, 0f, 1f);
            model = g.transform;
            // normaliza o tamanho (~1,1 m de largura)
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float w = Mathf.Max(b.size.x, b.size.z);
                if (w > 0.01f) model.localScale *= (tier >= 2 ? 1.35f : 1.1f) / w;
            }
            foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;

            var lg = new GameObject("brilho");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            glow = lg.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = U.Hex("ffc84a");
            glow.range = 5f;
            glow.intensity = tier >= 2 ? 3.5f : 2.5f;
            glow.shadows = LightShadows.None;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!landed)
            {
                Vector3 p = transform.position;
                vy -= 30f * dt;
                p.y += vy * dt;
                if (p.y <= groundY)
                {
                    p.y = groundY;
                    if (bounces == 0)
                    {
                        if (Game.I != null) Game.I.Shake(0.2f);
                        FX.Dust(p, U.Hex("b0a080"), 1.2f);
                        Sfx.Play("coin", p);
                    }
                    if (bounces < 2 && vy < -3f) { vy = -vy * 0.35f; bounces++; punch = 1f; }
                    else { landed = true; OnLand(); }
                }
                transform.position = p;
            }
            else
            {
                if (glow != null) glow.intensity = (opened ? 1f : (tier >= 2 ? 3.5f : 2.5f)) * (0.8f + 0.2f * Mathf.Sin(Time.time * 4f));
                if (!opened)
                {
                    sparkleClock -= dt;
                    if (sparkleClock <= 0f) { sparkleClock = 0.5f; FX.Sparkle(transform.position + Vector3.up * 0.5f, U.Hex("ffd870"), 0.6f, 0.2f); }
                    var gm = Game.I;
                    if (gm != null && gm.player != null && gm.player.state != "dead"
                        && U.Flat(gm.player.transform.position - transform.position).magnitude < 1.6f)
                        Open();
                }
            }
            if (punch > 0f && model != null)
            {
                punch = Mathf.Max(0f, punch - dt * 4f);
                float s = 1f + 0.18f * punch;
                transform.localScale = new Vector3(s, 1f / s, s);
            }
        }

        void OnLand()
        {
            ring = FX.FlatQuad("anel_bau", transform.position + Vector3.up * 0.04f, U.RingTexture(), U.Hex("ffc84a", 0.7f), true);
            ring.transform.SetParent(transform, true);
            ring.transform.localScale = Vector3.one * 2.2f;
            FX.Ring(transform.position, 1.6f, U.Hex("ffd870"), 0.4f);
        }

        void Open()
        {
            if (opened) return;
            opened = true;
            punch = 1f;
            StartCoroutine(Reward());
        }

        IEnumerator Reward()
        {
            Vector3 pos = transform.position;
            Sfx.Play("chest_open", pos);
            FX.Burst(pos + Vector3.up * 0.8f, U.Hex("ffd870"), 1.2f, 40);
            FX.Flash(pos + Vector3.up, U.Hex("ffd870"), 5f, 0.5f);
            var items = GameState.RollChest(tier);
            yield return new WaitForSeconds(0.25f);
            foreach (var id in items)
            {
                var d = GameData.Item(id);
                if (d == null) continue;
                GameState.Grant(id);
                Color c = GameData.RarityColor(d);
                string rn = GameData.RarityName(d);
                bool leg = d.rarity == "lendario", rare = d.rarity == "raro";
                Sfx.Play(leg ? "item_legendary" : rare ? "item_rare" : "coin", pos);
                FX.Pillar(pos, c, leg ? 1.6f : rare ? 1.2f : 0.7f);
                HUD.Popup(pos + Vector3.up * 1.8f, d.name, c, rare || leg);
                if (HUD.I != null) HUD.I.Toast((string.IsNullOrEmpty(rn) ? "" : "[" + rn + "] ") + d.name);
                yield return new WaitForSeconds(leg ? 0.6f : 0.35f);
            }
            GameState.Save();
            yield return new WaitForSeconds(2f);
            if (ring != null) Destroy(ring);
        }
    }
}
