using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Passivas de combo do herói (componente no Player). Conta acertos consecutivos do herói
    /// (golpes, projéteis, habilidades, ultimate); zera após 2,6 s sem acertar ou ao levar dano.
    /// Patamares:  10 = +10% velocidade de ataque · 25 = +15% de dano ·
    ///             50 = FRENESI por 6 s (passiva da classe):
    ///               Guerreiro: roubo de vida 15% · Arqueiro: cada 3º tiro dispara 3 flechas ·
    ///               Mago: habilidades sem custo de mana · Tank: reflete 30% do dano recebido.
    /// A HUD pode ler Combo, ActivePassive, TierText, FrenzyLeft.
    /// </summary>
    public class ComboPassives : MonoBehaviour
    {
        public const float Window = 2.6f, FrenzyDuration = 6f;
        public const int TierSpeed = 10, TierDamage = 25, TierFrenzy = 50;
        public const float SpeedBonus = 1.10f, DamageBonus = 1.15f, LifeStealPct = 0.15f, ReflectPct = 0.30f;

        /// <summary>Acertos consecutivos atuais.</summary>
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        /// <summary>Segundos restantes até o combo zerar.</summary>
        public float TimeLeft => timer;
        /// <summary>Segundos restantes do Frenesi (0 = inativo).</summary>
        public float FrenzyLeft => frenzyT;
        public bool Frenzy => frenzyT > 0f;
        /// <summary>Classe que ativou o Frenesi atual.</summary>
        public string FrenzyClass { get; private set; } = "";

        /// <summary>Nome da passiva de Frenesi ativa ("" se nenhuma).</summary>
        public string ActivePassive => Frenzy ? PassiveName(FrenzyClass) : "";

        /// <summary>Texto curto dos bônus de patamar ativos (para a HUD).</summary>
        public string TierText
        {
            get
            {
                if (Frenzy) return "FRENESI: " + PassiveName(FrenzyClass);
                if (Combo >= TierDamage) return "+10% vel. ataque · +15% dano";
                if (Combo >= TierSpeed) return "+10% vel. ataque";
                return "";
            }
        }

        public float AttackSpeedMult => Combo >= TierSpeed ? SpeedBonus : 1f;
        public float DamageMult => Combo >= TierDamage ? DamageBonus : 1f;
        public bool LifeSteal => Frenzy && FrenzyClass == "guerreiro";
        public bool TripleShot => Frenzy && FrenzyClass == "arqueiro";
        public bool FreeMana => Frenzy && FrenzyClass == "mago";
        public bool ReflectDamage => Frenzy && FrenzyClass == "tank";

        float timer, frenzyT, healAcc, healPopT;
        int nextFrenzyAt = TierFrenzy;
        GameObject aura;
        Player player;

        void Awake() { player = GetComponent<Player>(); }

        public static string PassiveName(string cls)
        {
            switch (cls)
            {
                case "guerreiro": return "Roubo de vida 15%";
                case "arqueiro": return "Rajada tripla (cada 3º tiro)";
                case "mago": return "Habilidades sem mana";
                case "tank": return "Reflete 30% do dano";
                default: return "Frenesi";
            }
        }

        static Color ClassColor(string cls)
        {
            switch (cls)
            {
                case "guerreiro": return SkillFX.Blood;
                case "arqueiro": return SkillFX.Leaf;
                case "mago": return SkillFX.Arcane;
                case "tank": return SkillFX.Gold;
                default: return SkillFX.Fire;
            }
        }

        // ------------------------------------------------------------------ eventos
        /// <summary>Um acerto do herói (dmg = dano causado, usado no roubo de vida).</summary>
        public void OnHit(int dmg)
        {
            if (player != null && player.state == "dead") return;
            Combo++;
            if (Combo > BestCombo) BestCombo = Combo;
            timer = Window;
            if (Combo == TierSpeed) Announce("COMBO x" + TierSpeed + ": +10% velocidade de ataque", U.Hex("ffe08a"), 0.8f);
            else if (Combo == TierDamage) Announce("COMBO x" + TierDamage + ": +15% de dano", U.Hex("ffb347"), 1.1f);
            if (Combo >= nextFrenzyAt)
            {
                nextFrenzyAt = (Combo / TierFrenzy + 1) * TierFrenzy;
                if (!Frenzy) StartFrenzy();
            }
            if (LifeSteal && dmg > 0) Heal(dmg * LifeStealPct);
        }

        /// <summary>Zera o combo (levou dano). O Frenesi já ativo continua até acabar.</summary>
        public void ResetCombo()
        {
            Combo = 0;
            timer = 0f;
            nextFrenzyAt = TierFrenzy;
        }

        /// <summary>Zera tudo (morte, troca de mapa).</summary>
        public void ResetAll()
        {
            ResetCombo();
            EndFrenzy();
        }

        void Announce(string msg, Color c, float ring)
        {
            GameState.Notify(msg);
            if (Sfx.Has("combo")) Sfx.Play("combo", transform.position, 0.6f);
            FX.Ring(transform.position, ring + 0.6f, c, 0.35f, 0.3f);
        }

        void StartFrenzy()
        {
            FrenzyClass = GameState.ClassId;
            frenzyT = FrenzyDuration;
            Color c = ClassColor(FrenzyClass);
            if (aura != null) Destroy(aura);
            aura = SkillFX.AttachAura(transform, c, FrenzyDuration, 55f, 0.6f, true);
            FX.Pillar(transform.position, c, 1.1f);
            SkillFX.Shockwave(transform.position, 2.5f, c, 0.4f);
            HUD.Popup(transform.position + Vector3.up * 2.8f, "FRENESI!", c, true);
            Sfx.Play(Sfx.Has("ult_ready") ? "ult_ready" : "buff", transform.position, 0.8f);
            GameState.Notify("FRENESI! " + PassiveName(FrenzyClass) + " por " + Mathf.RoundToInt(FrenzyDuration) + "s");
            if (Game.I != null) Game.I.Shake(0.25f);
        }

        void EndFrenzy()
        {
            frenzyT = 0f;
            FrenzyClass = "";
            if (aura != null)
            {
                var a = aura.GetComponent<FxAutoEnd>();
                if (a != null) a.EndNow(); else Destroy(aura);
                aura = null;
            }
        }

        void Heal(float amount)
        {
            healAcc += amount;
            if (healAcc < 1f) return;
            int h = Mathf.FloorToInt(healAcc);
            healAcc -= h;
            if (GameState.P.hp >= GameState.maxHp) return;
            GameState.P.hp = Mathf.Min(GameState.maxHp, GameState.P.hp + h);
            if (healPopT <= 0f)
            {
                healPopT = 0.35f;
                HUD.Popup(transform.position + Vector3.up * 2.3f, "+" + h, U.Hex("ff6a6a"));
            }
            GameState.Emit();
        }

        /// <summary>Tank em Frenesi: devolve parte do dano ao atacante mais próximo de fromPos.</summary>
        public void Reflect(float damageTaken, Vector3 fromPos)
        {
            if (!ReflectDamage || damageTaken <= 0f || Game.I == null) return;
            Enemy best = null;
            float bd = 4.5f;
            foreach (var e in Game.I.enemies)
            {
                if (e == null || e.dead) continue;
                float d = U.Flat(e.transform.position - fromPos).magnitude - e.radius;
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) return;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(damageTaken * ReflectPct));
            best.TakeHit(dmg, transform.position, false);
            SkillFX.Beam(transform.position + Vector3.up * 1.1f, best.transform.position + Vector3.up * 1.1f, SkillFX.Gold, 0.18f, 0.2f);
            FX.Burst(best.transform.position + Vector3.up, SkillFX.Gold, 0.6f, 12);
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            float dt = Time.deltaTime;
            if (healPopT > 0f) healPopT -= dt;
            if (timer > 0f)
            {
                timer -= dt;
                if (timer <= 0f) ResetCombo();
            }
            if (frenzyT > 0f)
            {
                frenzyT -= dt;
                if (frenzyT <= 0f)
                {
                    EndFrenzy();
                    GameState.Notify("O Frenesi acabou.");
                }
            }
        }

        void OnDisable() { EndFrenzy(); }
    }
}
