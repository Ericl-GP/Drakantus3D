using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    // ------------------------------------------------------------------
    // Estruturas lidas de Assets/Drakantus/Resources/Data/*.json
    // (edite os JSON para balancear o jogo sem mexer no código)
    // ------------------------------------------------------------------

    [Serializable]
    public class ClassDef
    {
        public string id, name, color, desc, ranged, model, weapon, offhand;
        public int[] icon;
        public float hp = 1, speed = 1, reach = 2, block = 0.2f;
        public int atk, def, mpBonus;
        // ---- evolução de classe
        public int tier = 1;              // 1 = classe inicial, 2 = evoluída
        public string parent = "";        // classe de origem (tier 2)
        public int evolveLevel = 10;      // nível para evoluir (tier 1) / nível em que se evolui (tier 2)
        public string[] weapons;          // tipos de arma permitidos (ItemDef.wtype)
        public float scale = 1f;          // tamanho do modelo (Berserker / The Guard são maiores)
        public string mechanic = "";      // stealth, heavy, ammo, longshot, support, arcane, frenzy, vengeance
        public float attackSpeed = 1f;    // multiplicador da velocidade do ataque básico
        public int ammo;                  // munição por pente (Pistoleiro)
    }

    [Serializable]
    public class VfxDef { public string style, color; }

    [Serializable]
    public class SkillDef
    {
        public string id, classId, name, desc, type, color, proj, text, telegraph;
        public int level = 1;
        public float mp, cd;
        public int[] icon;
        // números (cada tipo usa alguns)
        public float radius, range, area, hitRadius, spacing, trigger, distance, novaRadius, jumpRange, aoe;
        public float speed, speedMult = 1, mult = 1, stun, time, duration, tick, delay, interval, spread, life;
        public float percent, flat, invuln, execute, shield, arc = 0.3f, taunt, dmgTaken = 1, buffDuration, novaMult = 1, shake;
        public int drops, count = 1, jumps, atk;
        public bool pierce, falling;
        public VfxDef[] vfx;
        // ---- classes evoluídas (ClassKit*.cs). 0 / vazio = sem efeito.
        public string alt;                 // id da versão furiosa (Berserker em Fúria)
        public bool hidden;                // não aparece na lista de habilidades/loadout (versões "_f")
        public float hpCost;               // fração da vida ATUAL paga ao usar (nunca deixa com menos de 1)
        public float lifesteal;            // fração do dano causado que vira cura (máx. 8% da vida máx. por golpe)
        public float vengeanceMult;        // The Guard: dano += Vingança × fator (consome o medidor)
        public float root, freeze, blind;  // segundos de imobilizar / congelar / cegar
        public float slow, slowTime;       // slow = fração da velocidade perdida (0,4 = 40% mais lento)
        public float pull, push;           // puxar (velocidade m/s) / empurrar (metros)
        public string dot;                 // "poison", "burn", "bleed"
        public float dotMult, dotTime;     // dano por segundo (× ataque) e duração
        public int dotStacks;              // acúmulos máximos
        public float atkPct, defPct, regen, crit, spellPct, manaCut, atkSpeed;   // buffs (frações)
        public float reflect, reflectRanged, manaShield, stealth, shieldPct, critMult;
        public bool party, unstoppable, infiniteAmmo, cleanse;
        public int hits, bounces, splits, targets;
        public float markStore;            // Marca da Morte: fração do dano guardada
    }

    [Serializable]
    public class EnemyDef
    {
        public string id, name, model, ranged, weapon, tint;
        public float hp = 5, dmg = 5, speed = 2, windup = 0.6f, scale = 1, reach = 1.5f;
        public int xp = 5, chest;
        public int[] coins;
        public bool boss;
        // [Inimigos] campos opcionais (vazio/0 = comportamento antigo)
        public string ai = "";        // "" / melee, bomber, shield, summoner, charger, healer, sniper, swarm, goblin
        public string element = "";   // "" , fire (chão em chamas ao morrer), ice (golpe gelado), poison (poça ao morrer)
        public string offhand = "";   // modelo na mão esquerda (ex.: skel_shield)
        public string summon = "";    // id invocado pelo "summoner"
        public float cooldown;        // recarga da ação especial do arquétipo (s); 0 = padrão do arquétipo
    }

    [Serializable]
    public class ItemDef
    {
        public string id, name, category, slot, rarity, stats, description, tint, model;
        public int atk, def, hp, mp, price, sell;
        public int crit;     // % de chance de crítico extra
        public int speed;    // % de velocidade de movimento extra
        public int[] icon;
        public string wtype; // tipo de arma (espada, adaga, espadao, arco, arco_longo, pistola, cajado, varinha, martelo, lanca, machado)
    }

    [Serializable]
    public class RarityDef { public string id, name, color, glow; }

    [Serializable] class ClassList { public ClassDef[] classes; }
    [Serializable] class SkillList { public string[] slots; public SkillDef[] skills; }
    [Serializable] class EnemyList { public EnemyDef[] enemies; }
    [Serializable] class ItemList { public RarityDef[] rarity; public ItemDef[] items; }

    /// <summary>Catálogo estático com classes, habilidades, inimigos e itens.</summary>
    public static class GameData
    {
        public static readonly Dictionary<string, ClassDef> Classes = new();
        public static readonly List<ClassDef> ClassOrder = new();
        public static readonly Dictionary<string, SkillDef> Skills = new();
        public static readonly Dictionary<string, EnemyDef> Enemies = new();
        public static readonly Dictionary<string, ItemDef> Items = new();
        public static readonly List<ItemDef> ItemOrder = new();
        public static readonly Dictionary<string, RarityDef> Rarity = new();
        public static string[] SkillKeys = { "Z", "X", "C", "V" };
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var c = Read<ClassList>("classes");
            if (c?.classes != null) foreach (var x in c.classes) { Classes[x.id] = x; ClassOrder.Add(x); }
            var s = Read<SkillList>("skills");
            if (s != null)
            {
                if (s.slots != null && s.slots.Length > 0) SkillKeys = s.slots;
                if (s.skills != null) foreach (var x in s.skills) Skills[x.id] = x;
            }
            var e = Read<EnemyList>("enemies");
            if (e?.enemies != null) foreach (var x in e.enemies) Enemies[x.id] = x;
            foreach (var x in ClassOrder)
            {
                if (x.tier <= 0) x.tier = 1; if (x.scale <= 0) x.scale = 1; if (x.attackSpeed <= 0) x.attackSpeed = 1;
                if (x.evolveLevel <= 0) x.evolveLevel = 10;
                if (x.parent == null) x.parent = ""; if (x.mechanic == null) x.mechanic = "";
                if (x.hp <= 0) x.hp = 1; if (x.speed <= 0) x.speed = 1; if (x.reach <= 0) x.reach = 2; if (x.block <= 0) x.block = 0.2f;
            }
            foreach (var x in Skills.Values)
            {
                if (x.mult <= 0) x.mult = 1; if (x.speedMult <= 0) x.speedMult = 1; if (x.dmgTaken <= 0) x.dmgTaken = 1;
                if (x.count <= 0) x.count = 1; if (x.arc == 0) x.arc = 0.3f; if (x.novaMult <= 0) x.novaMult = 1;
                if (x.level <= 0) x.level = 1;
                if (x.alt == null) x.alt = ""; if (x.dot == null) x.dot = "";
                if (x.id != null && x.id.EndsWith("_f")) x.hidden = true;
            }
            foreach (var x in Enemies.Values)
            {
                if (x.scale <= 0) x.scale = 1; if (x.speed <= 0) x.speed = 2; if (x.reach <= 0) x.reach = 1.5f;
                if (x.windup <= 0) x.windup = 0.6f; if (x.hp <= 0) x.hp = 5;
                if (x.coins == null || x.coins.Length < 2) x.coins = new[] { 1, 3 };
            }
            var i = Read<ItemList>("items");
            if (i != null)
            {
                if (i.rarity != null) foreach (var r in i.rarity) Rarity[r.id] = r;
                if (i.items != null) foreach (var x in i.items)
                {
                    x.slot = NormalizeSlot(x.slot);
                    if (x.slot == "arma" && string.IsNullOrEmpty(x.wtype)) x.wtype = WeaponTypeOfModel(x.model);
                    Items[x.id] = x; ItemOrder.Add(x);
                }
            }
        }

        // ------------------------------------------------------------------ slots de equipamento
        /// <summary>Slots do herói (ordem fixa; índice usado no save em Profile.equipUids).</summary>
        public static readonly string[] EquipSlots = { "arma", "capacete", "peitoral", "calca", "bota", "capa", "asa", "colar", "brinco1", "brinco2", "anel1", "anel2" };

        /// <summary>Converte nomes antigos do JSON/save ("armadura" -> "peitoral", "joia" -> "anel").</summary>
        public static string NormalizeSlot(string slot)
        {
            if (slot == "armadura") return "peitoral";
            if (slot == "joia") return "anel";
            return slot ?? "";
        }

        /// <summary>Tipo do slot sem o número: "anel1" -> "anel", "brinco2" -> "brinco".</summary>
        public static string BaseSlot(string equipSlot)
        {
            if (string.IsNullOrEmpty(equipSlot)) return "";
            char c = equipSlot[equipSlot.Length - 1];
            if (c == '1' || c == '2') return equipSlot.Substring(0, equipSlot.Length - 1);
            return equipSlot;
        }

        public static bool IsEquipSlotType(string itemSlot)
        {
            switch (itemSlot)
            {
                case "arma": case "capacete": case "peitoral": case "calca": case "bota":
                case "capa": case "asa": case "colar": case "brinco": case "anel": return true;
            }
            return false;
        }

        /// <summary>Nome do slot em PT-BR (aceita tipo do item ou slot do herói).</summary>
        public static string SlotLabel(string slot)
        {
            switch (slot)
            {
                case "arma": return "Arma";
                case "capacete": return "Capacete";
                case "peitoral": case "armadura": return "Peitoral";
                case "calca": return "Calça";
                case "bota": return "Botas";
                case "capa": return "Capa";
                case "asa": return "Asas";
                case "colar": return "Colar";
                case "brinco": return "Brinco";
                case "brinco1": return "Brinco (esq.)";
                case "brinco2": return "Brinco (dir.)";
                case "anel": case "joia": return "Anel";
                case "anel1": return "Anel (esq.)";
                case "anel2": return "Anel (dir.)";
                case "consumivel": return "Consumível";
            }
            return slot ?? "";
        }

        static T Read<T>(string name) where T : class
        {
            var ta = Resources.Load<TextAsset>("Data/" + name);
            if (ta == null) { Debug.LogError("[Drakantus] Faltando Resources/Data/" + name + ".json"); return null; }
            try { return JsonUtility.FromJson<T>(ta.text); }
            catch (Exception ex) { Debug.LogError("[Drakantus] Erro lendo " + name + ".json: " + ex.Message); return null; }
        }

        public static ClassDef Class(string id) => Classes.TryGetValue(id ?? "", out var c) ? c : (ClassOrder.Count > 0 ? ClassOrder[0] : new ClassDef());
        public static SkillDef Skill(string id) => id != null && Skills.TryGetValue(id, out var s) ? s : null;
        public static ItemDef Item(string id) => id != null && Items.TryGetValue(id, out var s) ? s : null;
        public static EnemyDef Enemy(string id) => id != null && Enemies.TryGetValue(id, out var s) ? s : null;

        // ------------------------------------------------------------------ classes e armas
        /// <summary>Tipo de arma pelo modelo (itens antigos sem "wtype").</summary>
        public static string WeaponTypeOfModel(string model)
        {
            switch (model)
            {
                case "sword": return "espada";
                case "sword2h": return "espadao";
                case "axe": case "axe2h": return "machado";
                case "bow": case "crossbow": return "arco";
                case "longbow": return "arco_longo";
                case "staff": return "cajado";
                case "wand": return "varinha";
                case "dagger": return "adaga";
                case "pistol": return "pistola";
                case "hammer": return "martelo";
                case "spear": return "lanca";
                default: return model ?? "";
            }
        }

        public static string WeaponTypeName(string wtype)
        {
            switch (wtype)
            {
                case "espada": return "Espada";
                case "espadao": return "Espadão";
                case "machado": return "Machados";
                case "arco": return "Arco";
                case "arco_longo": return "Arco longo";
                case "cajado": return "Cajado";
                case "varinha": return "Varinha";
                case "adaga": return "Adagas";
                case "pistola": return "Pistolas";
                case "martelo": return "Martelo";
                case "lanca": return "Lança";
                default: return wtype ?? "";
            }
        }

        /// <summary>A classe pode usar esta arma? Itens que não são arma: sempre.</summary>
        public static bool ClassCanUse(ClassDef cd, ItemDef d)
        {
            if (d == null) return false;
            if (d.slot != "arma" || cd == null || cd.weapons == null || cd.weapons.Length == 0) return true;
            return Array.IndexOf(cd.weapons, d.wtype) >= 0;
        }

        /// <summary>Nomes das classes que usam este tipo de arma (para a dica do item).</summary>
        public static string ClassesForWeapon(string wtype)
        {
            var names = new List<string>();
            foreach (var c in ClassOrder) if (c.weapons != null && Array.IndexOf(c.weapons, wtype) >= 0) names.Add(c.name);
            return string.Join(", ", names.ToArray());
        }

        public static List<ClassDef> BaseClasses()
        {
            var l = new List<ClassDef>();
            foreach (var c in ClassOrder) if (c.tier <= 1) l.Add(c);
            return l;
        }

        public static List<ClassDef> EvolutionsOf(string classId)
        {
            var l = new List<ClassDef>();
            foreach (var c in ClassOrder) if (c.tier >= 2 && c.parent == classId) l.Add(c);
            return l;
        }

        /// <summary>Arma inicial dada ao escolher/evoluir a classe.</summary>
        public static string StarterWeapon(string classId)
        {
            switch (classId)
            {
                case "guerreiro": return "practice_sword";
                case "arqueiro": return "practice_bow";
                case "mago": return "practice_staff";
                case "tank": return "practice_hammer";
                case "assassino": return "twin_daggers";
                case "cavaleiro": return "great_blade";
                case "pistoleiro": return "flint_pistols";
                case "arqueiro_superior": return "yew_longbow";
                case "sacerdote": return "holy_staff";
                case "arquimago": return "arcane_staff";
                case "berserker": return "twin_axes";
                case "the_guard": return "guard_blade";
                default: return "";
            }
        }

        /// <summary>Habilidades da classe. Classe evoluída herda as da classe de origem.</summary>
        public static List<SkillDef> SkillsOf(string classId)
        {
            var l = new List<SkillDef>();
            var cd = Class(classId);
            string parent = cd != null ? cd.parent : "";
            foreach (var s in Skills.Values) if (!s.hidden) if (s.classId == classId || (!string.IsNullOrEmpty(parent) && s.classId == parent)) l.Add(s);
            l.Sort((a, b) => a.level != b.level ? a.level.CompareTo(b.level) : string.CompareOrdinal(a.id, b.id));
            return l;
        }

        public static Color RarityColor(ItemDef d)
        {
            if (d == null || !Rarity.TryGetValue(d.rarity ?? "comum", out var r)) return Color.white;
            return U.Hex(r.color);
        }
        public static string RarityGlow(ItemDef d)
        {
            if (d == null || !Rarity.TryGetValue(d.rarity ?? "comum", out var r)) return "";
            return r.glow ?? "";
        }
        public static string RarityName(ItemDef d)
        {
            if (d == null || !Rarity.TryGetValue(d.rarity ?? "comum", out var r)) return "";
            return r.name;
        }
    }
}
