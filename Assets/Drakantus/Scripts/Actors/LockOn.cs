using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Trava de mira (componente no Player). Tab trava no inimigo mais próximo do cursor (chefes têm prioridade);
    /// Tab de novo passa para o próximo e, depois do último, destrava. Destrava sozinho se o alvo morrer,
    /// ficar a mais de 18 m ou o herói não puder lutar. Enquanto travado, Player.AimDir/AimPoint apontam para o alvo.
    /// </summary>
    public class LockOn : MonoBehaviour
    {
        public const float MaxRange = 18f, BossBonus = 8f;

        /// <summary>Alvo travado (null = livre).</summary>
        public Enemy Target { get; private set; }
        public bool Locked => Target != null;

        Player player;
        Transform marker, ring, gem;
        Renderer ringR;
        float t;
        readonly List<Enemy> cands = new();

        void Awake() { player = GetComponent<Player>(); }

        void Update()
        {
            if (player == null) return;
            if (Target != null && !Valid(Target)) Release();
            bool can = player.canFight && !player.inputLocked && player.state != "dead" && Time.timeScale > 0.01f;
            if (!can)
            {
                if (Target != null && (!player.canFight || player.state == "dead")) Release();
                return;
            }
            if (InputW.Down(K.Tab)) Cycle();
        }

        bool Valid(Enemy e)
        {
            if (e == null || e.dead || !e.gameObject.activeInHierarchy) return false;
            if (Game.I == null || !Game.I.enemies.Contains(e)) return false;
            return U.Flat(e.transform.position - transform.position).magnitude <= MaxRange;
        }

        /// <summary>Trava no melhor alvo ou passa para o próximo; depois do último, destrava.</summary>
        public void Cycle()
        {
            if (Game.I == null) return;
            Vector3 refP = transform.position;
            if (U.MouseOnGround(CameraRig.Cam, transform.position.y, out var mp)) refP = mp;
            cands.Clear();
            foreach (var e in Game.I.enemies)
                if (Valid(e)) cands.Add(e);
            if (cands.Count == 0)
            {
                if (Target != null) Release();
                else GameState.Notify("Nenhum inimigo por perto para travar.");
                return;
            }
            cands.Sort((a, b) => Score(a, refP).CompareTo(Score(b, refP)));
            if (Target == null) { Lock(cands[0]); return; }
            int i = cands.IndexOf(Target);
            if (i < 0) { Lock(cands[0]); return; }
            if (i + 1 >= cands.Count) { Release(); Sfx.Play("ui_close", null, 0.5f); return; }
            Lock(cands[i + 1]);
        }

        static float Score(Enemy e, Vector3 refP)
        {
            float d = U.Flat(e.transform.position - refP).magnitude;
            if (e.def != null && e.def.boss) d -= BossBonus;
            return d;
        }

        public void Lock(Enemy e)
        {
            if (e == null) return;
            Target = e;
            EnsureMarker();
            marker.gameObject.SetActive(true);
            t = 0f;
            Sfx.Play("ui_click", null, 0.6f);
            FX.Ring(e.transform.position, e.radius * 1.8f + 0.4f, U.Hex("ff6a4a"), 0.25f, 1.6f);
        }

        public void Release()
        {
            Target = null;
            if (marker != null) marker.gameObject.SetActive(false);
        }

        void EnsureMarker()
        {
            if (marker != null) return;
            marker = new GameObject("trava_alvo").transform;
            marker.SetParent(transform, false);
            var r = FX.FlatQuad("anel_trava", transform.position, U.RingTexture(), U.Hex("ff5a3a", 0.9f), true);
            r.transform.SetParent(marker, true);
            ring = r.transform;
            ringR = r.GetComponent<Renderer>();
            var g = U.Prim(PrimitiveType.Cube, marker, Vector3.zero, new Vector3(0.22f, 0.22f, 0.22f), U.Hex("ff7a4a"));
            var gr = g.GetComponent<Renderer>();
            gr.sharedMaterial = U.Lit(U.Hex("ff7a4a"), 0.6f, U.Hex("ff4a2a") * 2.5f);
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gem = g.transform;
        }

        void LateUpdate()
        {
            if (Target == null || marker == null) return;
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            t += Time.unscaledDeltaTime;
            Vector3 p = Target.transform.position;
            float pulse = 1f + 0.1f * Mathf.Sin(t * 7f);
            ring.position = p + Vector3.up * 0.07f;
            ring.rotation = Quaternion.Euler(90f, t * 120f, 0f);
            ring.localScale = Vector3.one * (Target.radius * 3.4f + 0.4f) * pulse;
            if (ringR != null) FX.SetColor(ringR, new Color(1f, 0.35f, 0.22f, 0.65f + 0.3f * Mathf.Sin(t * 7f)));
            float top = Mathf.Max(Target.visual != null ? Target.visual.height : 0f, 1.6f * Target.scale);
            float y = top + (Target.def != null && Target.def.boss ? 1.3f : 0.8f) + 0.15f * Mathf.Sin(t * 4f);
            gem.position = p + Vector3.up * y;
            gem.rotation = Quaternion.Euler(45f, t * 180f, 45f);
        }

        void OnDisable() { Release(); }

        void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }
    }
}
