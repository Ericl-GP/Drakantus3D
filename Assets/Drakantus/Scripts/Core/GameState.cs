using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Drakantus
{
    [Serializable]
    public class OwnedItem
    {
        public int uid; public string itemId; public int qty = 1;
        public int pos = -1;   // posição na mochila (0..), -1 = sem posição / equipado
    }

    [Serializable]
    public class Loadout { public string classId; public string[] skills = new string[4]; }

    [Serializable]
    public class Profile
    {
        public const int CurrentVersion = 2;
        public int saveVersion;   // 0 = save antigo (campo ausente no JSON); NewGame grava CurrentVersion
        public string playerName = "Aventureiro";
        public string classId = "";
        public int level = 1, xp, coins = 650, nextUid = 1;
        public float hp = 100, mp = 55;
        public bool registered;
        public List<OwnedItem> items = new();
        // equipamento: um uid por slot, na ordem de GameData.EquipSlots (0 ou -1 = vazio)
        public int[] equipUids = new int[12];
        // LEGADO (save v1): migrados para equipUids no Load e depois zerados para -1
        public int weaponUid = -1, armorUid = -1, ringUid = -1;
        public List<Loadout> loadouts = new();
        public List<int> floorsCleared = new();
        // aparência ("" = cor original do modelo); tom 0..1 (0,5 = neutro)
        public string hairColor = "", clothColor1 = "", clothColor2 = "";
        public float hairTone = 0.5f, cloth1Tone = 0.5f, cloth2Tone = 0.5f;
    }

    /// <summary>
    /// Estado do jogador (perfil salvo, bolsa, equipamento, habilidades equipadas, XP).
    /// Salvo em JSON em Application.persistentDataPath/drakantus_save.json.
    /// </summary>
    public static class GameState
    {
        public static Profile P = new();
        public static float maxHp = 100, maxMp = 55, stamina = 100, maxStamina = 100;
        public static float atkBuff;          // elixir de força (segundos)
        public static int skillAtkBonus;      // Grito de Guerra
        public static float skillSpeed = 1f;  // buffs de velocidade

        public static event Action Changed;            // vida, mana, moedas, XP...
        public static event Action InventoryChanged;
        public static event Action LoadoutChanged;
        public static event Action<int> LeveledUp;
        public static event Action<List<string>> SkillsUnlocked;
        public static event Action<string> Notified;

        static string SavePath => Path.Combine(Application.persistentDataPath, "drakantus_save.json");

        public static void Emit() => Changed?.Invoke();
        public static void Notify(string msg) => Notified?.Invoke(msg);

        // ------------------------------------------------------------------ salvar / carregar
        public static bool HasSave() => File.Exists(SavePath);

        public static void NewGame(string name)
        {
            P = new Profile { playerName = string.IsNullOrWhiteSpace(name) ? "Aventureiro" : name.Trim(), saveVersion = Profile.CurrentVersion };
            // a arma inicial vem com a classe (SetClass)
            Grant("traveler_vest", 1, true);
            Grant("amber_ring", 1, true);
            Grant("health_potion", 3);
            Recalc();
            P.hp = maxHp; P.mp = maxMp;
        }

        public static void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(P, true)); }
            catch (Exception e) { Debug.LogWarning("[Drakantus] Não foi possível salvar: " + e.Message); }
        }

        public static bool Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return false;
                var p = JsonUtility.FromJson<Profile>(File.ReadAllText(SavePath));
                if (p == null) return false;
                P = p;
                if (P.items == null) P.items = new List<OwnedItem>();
                if (P.loadouts == null) P.loadouts = new List<Loadout>();
                if (P.floorsCleared == null) P.floorsCleared = new List<int>();
                P.items.RemoveAll(i => GameData.Item(i.itemId) == null);
                Migrate();
                FixWeaponForClass();
                Recalc();
                P.hp = Mathf.Clamp(P.hp, 1, maxHp);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Drakantus] Save inválido: " + e.Message); return false; }
        }

        // ------------------------------------------------------------------ classe
        public static ClassDef Class => GameData.Class(P.classId);
        public static string ClassId => GameData.Classes.ContainsKey(P.classId ?? "") ? P.classId : (GameData.ClassOrder.Count > 0 ? GameData.ClassOrder[0].id : "guerreiro");

        /// <summary>
        /// Escolha da classe inicial. Só vale UMA vez (personagem sem classe). Depois disso
        /// a única mudança possível é a evolução (Evolve) na Mestra de Classes da Guilda.
        /// </summary>
        public static bool SetClass(string id)
        {
            var cd = GameData.Classes.TryGetValue(id ?? "", out var c) ? c : null;
            if (cd == null || cd.tier > 1) return false;
            if (HasChosenClass) { Notify("Sua classe já foi escolhida. Evolua com a Mestra de Classes."); return false; }
            P.classId = id;
            OnClassChanged(id);
            Notify("Classe escolhida: " + cd.name);
            return true;
        }

        public static bool HasChosenClass => !string.IsNullOrEmpty(P.classId) && GameData.Classes.ContainsKey(P.classId);
        public static bool IsEvolved => HasChosenClass && Class.tier >= 2;

        /// <summary>Pode evoluir agora? Motivo em "why" quando não pode.</summary>
        public static bool CanEvolve(out string why)
        {
            why = "";
            if (!HasChosenClass) { why = "Escolha uma classe primeiro."; return false; }
            if (IsEvolved) { why = "Você já evoluiu para " + Class.name + "."; return false; }
            if (P.level < Class.evolveLevel) { why = "Chegue ao nível " + Class.evolveLevel + " para evoluir."; return false; }
            return true;
        }

        /// <summary>Evolui a classe base para uma das duas classes avançadas. Definitivo.</summary>
        public static bool Evolve(string id)
        {
            if (!CanEvolve(out string why)) { Notify(why); return false; }
            var cd = GameData.Classes.TryGetValue(id ?? "", out var c) ? c : null;
            if (cd == null || cd.tier < 2 || cd.parent != P.classId) return false;
            P.classId = id;
            OnClassChanged(id);
            Notify("Você evoluiu para " + cd.name + "!");
            return true;
        }

        /// <summary>Depois de escolher/evoluir: tira armas que a classe não usa e dá a arma inicial.</summary>
        static void OnClassChanged(string id)
        {
            var cur = Equipped("arma");
            if (cur != null && !GameData.ClassCanUse(Class, cur)) Unequip("arma");
            string starter = GameData.StarterWeapon(id);
            if (!string.IsNullOrEmpty(starter) && GameData.Item(starter) != null)
            {
                bool hasUsable = Equipped("arma") != null;
                Grant(starter, 1, !hasUsable);
            }
            Recalc();
            P.hp = maxHp; P.mp = maxMp;
            Save();
            Emit();
            LoadoutChanged?.Invoke();
            ClassChanged?.Invoke();
        }

        /// <summary>Disparado ao escolher ou evoluir a classe (o Player reconstrói o modelo).</summary>
        public static event Action ClassChanged;

        /// <summary>Save antigo: arma equipada que a classe atual não pode usar volta para a bolsa.</summary>
        static void FixWeaponForClass()
        {
            if (!HasChosenClass) return;
            var cur = Equipped("arma");
            if (cur != null && !GameData.ClassCanUse(Class, cur))
            {
                int si = SlotIndex("arma");
                var o = Owned(Eq[si]);
                Eq[si] = 0;
                if (o != null) o.pos = FreeBagPos();
                NormalizeBag();
            }
        }

        // ------------------------------------------------------------------ atributos
        public static int AttackPower()
        {
            return 1 + Class.atk + SumStat(0) / 2 + (P.level - 1) / 3 + (atkBuff > 0 ? 4 : 0) + skillAtkBonus;
        }

        public static int Defense()
        {
            int armor = Equipped("peitoral") != null ? 0 : 2;   // sem peitoral: 2 de defesa base (como antes)
            return armor + SumStat(1) + Class.def + (P.level - 1);
        }

        public static float MoveSpeed() => 5.4f * Class.speed * skillSpeed * (1f + SumStat(4) / 100f);

        /// <summary>Chance de crítico extra dos itens (0..1). O golpe básico tem 12% de base no Player.</summary>
        public static float CritBonus() => SumStat(5) / 100f;

        /// <summary>Soma de um atributo em todos os slots: 0 atk, 1 def, 2 hp, 3 mp, 4 speed(%), 5 crit(%).</summary>
        public static int SumStat(int which)
        {
            int n = 0;
            for (int i = 0; i < GameData.EquipSlots.Length; i++)
            {
                var d = EquippedAt(i);
                if (d == null) continue;
                switch (which)
                {
                    case 0: n += d.atk; break;
                    case 1: n += d.def; break;
                    case 2: n += d.hp; break;
                    case 3: n += d.mp; break;
                    case 4: n += d.speed; break;
                    case 5: n += d.crit; break;
                }
            }
            return n;
        }

        public static void Recalc()
        {
            maxHp = (100f + SumStat(2) + (P.level - 1) * 8f) * Class.hp;
            maxMp = 50f + Class.mpBonus + SumStat(3) + (P.level - 1) * 4f;
            P.hp = Mathf.Min(P.hp, maxHp); P.mp = Mathf.Min(P.mp, maxMp);
            Emit();
        }

        // ------------------------------------------------------------------ XP e moedas
        public static int XpToNext() => 40 + (P.level - 1) * 35;

        public static void AddXp(int amount)
        {
            var before = UnlockedIds();
            P.xp += amount;
            bool leveled = false;
            while (P.xp >= XpToNext())
            {
                P.xp -= XpToNext();
                P.level++;
                leveled = true;
                Recalc();
                P.hp = maxHp; P.mp = maxMp;
                LeveledUp?.Invoke(P.level);
            }
            if (leveled)
            {
                var fresh = new List<string>();
                foreach (var id in UnlockedIds()) if (!before.Contains(id)) fresh.Add(id);
                if (fresh.Count > 0)
                {
                    var lo = CurrentLoadout();
                    foreach (var id in fresh)
                        for (int i = 0; i < lo.Length; i++) if (string.IsNullOrEmpty(lo[i])) { lo[i] = id; break; }
                    LoadoutChanged?.Invoke();
                    SkillsUnlocked?.Invoke(fresh);
                }
                Save();
            }
            Emit();
        }

        public static void AddCoins(int amount) { P.coins += amount; Emit(); }

        // ------------------------------------------------------------------ habilidades (4 slots por classe)
        public static bool SkillUnlocked(string id)
        {
            var d = GameData.Skill(id);
            return d != null && P.level >= d.level;
        }

        static List<string> UnlockedIds()
        {
            var l = new List<string>();
            foreach (var s in GameData.SkillsOf(ClassId)) if (SkillUnlocked(s.id)) l.Add(s.id);
            return l;
        }

        public static string[] CurrentLoadout()
        {
            string cid = ClassId;
            Loadout lo = P.loadouts.Find(x => x.classId == cid);
            int n = GameData.SkillKeys.Length;
            if (lo == null || lo.skills == null || lo.skills.Length != n)
            {
                lo = new Loadout { classId = cid, skills = new string[n] };
                int k = 0;
                foreach (var s in GameData.SkillsOf(cid))
                    if (k < n && SkillUnlocked(s.id)) lo.skills[k++] = s.id;
                P.loadouts.RemoveAll(x => x.classId == cid);
                P.loadouts.Add(lo);
            }
            for (int i = 0; i < lo.skills.Length; i++)
            {
                var d = GameData.Skill(lo.skills[i]);
                if (d == null || (d.classId != cid && d.classId != Class.parent) || !SkillUnlocked(d.id)) lo.skills[i] = "";
            }
            return lo.skills;
        }

        public static SkillDef SlotSkill(int i)
        {
            var lo = CurrentLoadout();
            return i >= 0 && i < lo.Length ? GameData.Skill(lo[i]) : null;
        }

        /// <summary>Equipa a habilidade no slot (troca de lugar se já estiver em outro).</summary>
        public static void SetSlot(int i, string id)
        {
            var lo = CurrentLoadout();
            if (i < 0 || i >= lo.Length) return;
            if (!string.IsNullOrEmpty(id) && !SkillUnlocked(id)) return;
            string prev = lo[i];
            int at = Array.IndexOf(lo, id);
            lo[i] = id;
            if (at >= 0 && at != i) lo[at] = prev;
            Save();
            LoadoutChanged?.Invoke();
        }

        // ------------------------------------------------------------------ itens / equipamento
        public const int BagSize = 36;   // mochila 6x6 (posições extras aparecem se passar disso)

        public static OwnedItem Owned(int uid) => uid > 0 ? P.items.Find(x => x.uid == uid) : null;

        static int[] Eq
        {
            get
            {
                int n = GameData.EquipSlots.Length;
                if (P.equipUids == null || P.equipUids.Length != n)
                {
                    var a = new int[n];
                    if (P.equipUids != null) for (int i = 0; i < Mathf.Min(n, P.equipUids.Length); i++) a[i] = P.equipUids[i];
                    P.equipUids = a;
                }
                return P.equipUids;
            }
        }

        /// <summary>Índice do slot do herói (aceita nomes antigos: "armadura", "joia", "anel", "brinco").</summary>
        public static int SlotIndex(string slot)
        {
            switch (slot)
            {
                case "armadura": slot = "peitoral"; break;
                case "joia": case "anel": slot = "anel1"; break;
                case "brinco": slot = "brinco1"; break;
            }
            return Array.IndexOf(GameData.EquipSlots, slot);
        }

        public static int EquippedUid(string slot)
        {
            int i = SlotIndex(slot);
            if (i < 0) return -1;
            int uid = Eq[i];
            return Owned(uid) != null ? uid : -1;
        }

        static ItemDef EquippedAt(int i)
        {
            var o = Owned(Eq[i]);
            return o != null ? GameData.Item(o.itemId) : null;
        }

        /// <summary>Item equipado no slot ("arma", "capacete", "peitoral", "calca", "bota", "capa", "asa", "colar", "brinco1/2", "anel1/2"; "armadura"/"joia" = legado).</summary>
        public static ItemDef Equipped(string slot)
        {
            int i = SlotIndex(slot);
            return i >= 0 ? EquippedAt(i) : null;
        }

        public static bool IsEquipped(int uid) => SlotOf(uid) != "";

        /// <summary>Slot onde o item está equipado ("" se não está).</summary>
        public static string SlotOf(int uid)
        {
            if (uid <= 0) return "";
            var e = Eq;
            for (int i = 0; i < e.Length; i++) if (e[i] == uid) return GameData.EquipSlots[i];
            return "";
        }

        /// <summary>O item pode ir neste slot do herói? (brincos/anéis aceitam qualquer um dos dois)</summary>
        public static bool CanEquipIn(ItemDef d, string slot)
        {
            return d != null && GameData.IsEquipSlotType(d.slot) && GameData.BaseSlot(slot) == d.slot
                && (!HasChosenClass || GameData.ClassCanUse(Class, d));
        }

        /// <summary>Mensagem quando a classe não pode usar a arma ("" se pode).</summary>
        public static string WhyCantUse(ItemDef d)
        {
            if (d == null || d.slot != "arma" || !HasChosenClass || GameData.ClassCanUse(Class, d)) return "";
            string who = GameData.ClassesForWeapon(d.wtype);
            return Class.name + " não usa " + GameData.WeaponTypeName(d.wtype).ToLowerInvariant() + (who != "" ? ". Usada por: " + who + "." : ".");
        }

        /// <summary>Melhor slot para o item: o primeiro vazio do par (anel/brinco) ou o primeiro.</summary>
        public static string DefaultSlotFor(ItemDef d)
        {
            if (d == null || !GameData.IsEquipSlotType(d.slot)) return "";
            if (d.slot == "anel" || d.slot == "brinco")
            {
                string a = d.slot + "1", b = d.slot + "2";
                if (EquippedUid(a) < 0) return a;
                if (EquippedUid(b) < 0) return b;
                return a;
            }
            return d.slot;
        }

        // ---- mochila
        public static OwnedItem ItemAt(int pos)
        {
            if (pos < 0) return null;
            foreach (var o in P.items) if (o.pos == pos && !IsEquipped(o.uid)) return o;
            return null;
        }

        public static int FreeBagPos()
        {
            var used = new HashSet<int>();
            foreach (var o in P.items) if (o.pos >= 0 && !IsEquipped(o.uid)) used.Add(o.pos);
            int p = 0;
            while (used.Contains(p)) p++;
            return p;
        }

        /// <summary>Maior posição ocupada da mochila (+1). Útil para crescer a grade além de 36.</summary>
        public static int BagExtent()
        {
            int m = 0;
            foreach (var o in P.items) if (!IsEquipped(o.uid) && o.pos + 1 > m) m = o.pos + 1;
            return m;
        }

        /// <summary>Corrige posições inválidas/repetidas (itens equipados ficam com pos = -1).</summary>
        public static void NormalizeBag()
        {
            var used = new HashSet<int>();
            var fix = new List<OwnedItem>();
            foreach (var o in P.items)
            {
                if (IsEquipped(o.uid)) { o.pos = -1; continue; }
                if (o.pos < 0 || used.Contains(o.pos)) fix.Add(o);
                else used.Add(o.pos);
            }
            foreach (var o in fix)
            {
                int p = 0;
                while (used.Contains(p)) p++;
                o.pos = p; used.Add(p);
            }
        }

        /// <summary>Move um item da mochila para a posição (troca de lugar se ocupada).</summary>
        public static void MoveInBag(int uid, int pos)
        {
            var o = Owned(uid);
            if (o == null || pos < 0 || IsEquipped(uid) || o.pos == pos) return;
            var other = ItemAt(pos);
            if (other != null) other.pos = o.pos;
            o.pos = pos;
            NormalizeBag();
            InventoryChanged?.Invoke();
            Save();
        }

        /// <summary>Ordena a mochila por tipo de slot, raridade (lendário primeiro) e nome.</summary>
        public static void SortBag()
        {
            var bag = new List<OwnedItem>();
            foreach (var o in P.items) if (!IsEquipped(o.uid)) bag.Add(o);
            bag.Sort((a, b) =>
            {
                var da = GameData.Item(a.itemId); var db = GameData.Item(b.itemId);
                int sa = SlotOrder(da), sb = SlotOrder(db);
                if (sa != sb) return sa.CompareTo(sb);
                int ra = RarityOrder(da), rb = RarityOrder(db);
                if (ra != rb) return rb.CompareTo(ra);
                return string.CompareOrdinal(da?.name ?? "", db?.name ?? "");
            });
            for (int i = 0; i < bag.Count; i++) bag[i].pos = i;
            InventoryChanged?.Invoke();
            Save();
        }

        static int SlotOrder(ItemDef d)
        {
            if (d == null) return 99;
            string[] order = { "arma", "capacete", "peitoral", "calca", "bota", "capa", "asa", "colar", "brinco", "anel", "consumivel" };
            int i = Array.IndexOf(order, d.slot);
            return i < 0 ? 50 : i;
        }

        public static int RarityOrder(ItemDef d) => d == null ? 0 : d.rarity == "lendario" ? 2 : d.rarity == "raro" ? 1 : 0;

        public static int Grant(string id, int qty = 1, bool equip = false)
        {
            var d = GameData.Item(id);
            if (d == null) return -1;
            if (d.slot == "consumivel")
            {
                var ex = P.items.Find(x => x.itemId == id);
                if (ex != null) { ex.qty += qty; InventoryChanged?.Invoke(); return ex.uid; }
            }
            var o = new OwnedItem { uid = P.nextUid++, itemId = id, qty = qty, pos = FreeBagPos() };
            P.items.Add(o);
            if (equip) Equip(o.uid);
            InventoryChanged?.Invoke();
            return o.uid;
        }

        public static int Count(string id)
        {
            int n = 0;
            foreach (var o in P.items) if (o.itemId == id) n += o.qty;
            return n;
        }

        /// <summary>Equipa no slot padrão do item (o que estava lá volta para a mochila no lugar dele).</summary>
        public static void Equip(int uid)
        {
            var o = Owned(uid); if (o == null) return;
            var d = GameData.Item(o.itemId); if (d == null) return;
            string slot = DefaultSlotFor(d);
            if (slot != "") EquipTo(uid, slot);
        }

        /// <summary>Equipa o item no slot indicado. Troca com o item que estava lá (vai para a posição de onde o novo saiu).</summary>
        public static bool EquipTo(int uid, string slot)
        {
            var o = Owned(uid); if (o == null) return false;
            var d = GameData.Item(o.itemId);
            int si = SlotIndex(slot);
            if (si < 0 || !CanEquipIn(d, GameData.EquipSlots[si]))
            {
                string why = WhyCantUse(d);
                if (why != "") Notify(why);
                return false;
            }
            var e = Eq;
            if (e[si] == uid) return true;
            string from = SlotOf(uid);
            int prev = Owned(e[si]) != null ? e[si] : 0;
            if (from != "")
            {
                // de um slot para outro (ex.: anel1 -> anel2): troca os dois
                int fi = SlotIndex(from);
                e[fi] = prev;
                e[si] = uid;
            }
            else
            {
                int bagPos = o.pos;
                e[si] = uid;
                o.pos = -1;
                var po = Owned(prev);
                if (po != null) po.pos = bagPos >= 0 && ItemAt(bagPos) == null ? bagPos : FreeBagPos();
            }
            NormalizeBag();
            Recalc();
            InventoryChanged?.Invoke();
            Save();
            return true;
        }

        /// <summary>Tira o item do slot e coloca na mochila (na posição pedida ou na primeira livre). O ocupante da posição vai para outra livre.</summary>
        public static void Unequip(string slot, int bagPos = -1)
        {
            int si = SlotIndex(slot);
            if (si < 0) return;
            var e = Eq;
            var o = Owned(e[si]);
            e[si] = 0;
            if (o != null)
            {
                if (bagPos >= 0)
                {
                    var occ = ItemAt(bagPos);
                    o.pos = bagPos;
                    if (occ != null) { occ.pos = -1; occ.pos = FreeBagPos(); }
                }
                else o.pos = FreeBagPos();
            }
            NormalizeBag();
            Recalc();
            InventoryChanged?.Invoke();
            Save();
        }

        /// <summary>Converte saves antigos (v1): weaponUid/armorUid/ringUid -> equipUids, posições da mochila, aparência.</summary>
        static void Migrate()
        {
            var e = Eq;
            if (P.saveVersion < 2)
            {
                if (P.weaponUid > 0 && Owned(P.weaponUid) != null) e[SlotIndex("arma")] = P.weaponUid;
                if (P.armorUid > 0 && Owned(P.armorUid) != null)
                {
                    // "armadura" antiga vira "peitoral" (ou o slot novo do item, ex.: manto -> capa)
                    var d = GameData.Item(Owned(P.armorUid).itemId);
                    string s = d != null && GameData.IsEquipSlotType(d.slot) ? DefaultSlotFor(d) : "peitoral";
                    e[SlotIndex(s)] = P.armorUid;
                }
                if (P.ringUid > 0 && Owned(P.ringUid) != null)
                {
                    var d = GameData.Item(Owned(P.ringUid).itemId);
                    string s = d != null && GameData.IsEquipSlotType(d.slot) ? DefaultSlotFor(d) : "anel1";
                    int si = SlotIndex(s);
                    if (si >= 0 && e[si] <= 0) e[si] = P.ringUid;
                }
                P.hairTone = 0.5f; P.cloth1Tone = 0.5f; P.cloth2Tone = 0.5f;
                if (P.hairColor == null) P.hairColor = "";
                if (P.clothColor1 == null) P.clothColor1 = "";
                if (P.clothColor2 == null) P.clothColor2 = "";
                foreach (var o in P.items) o.pos = -1;
                P.saveVersion = Profile.CurrentVersion;
            }
            P.weaponUid = -1; P.armorUid = -1; P.ringUid = -1;
            // remove uids inválidos ou itens que não cabem mais no slot
            for (int i = 0; i < e.Length; i++)
            {
                var o = Owned(e[i]);
                if (o == null || !CanEquipIn(GameData.Item(o.itemId), GameData.EquipSlots[i])) e[i] = 0;
            }
            NormalizeBag();
        }

        public static bool Buy(string id)
        {
            var d = GameData.Item(id);
            if (d == null || d.price <= 0) return false;
            if (P.coins < d.price) { Notify("Moedas insuficientes."); return false; }
            P.coins -= d.price;
            Grant(id);
            Notify("Comprou: " + d.name);
            Save(); Emit();
            return true;
        }

        public static void Sell(int uid)
        {
            var o = Owned(uid); if (o == null || IsEquipped(uid)) return;
            var d = GameData.Item(o.itemId);
            P.coins += d.sell;
            o.qty--;
            if (o.qty <= 0) P.items.Remove(o);
            InventoryChanged?.Invoke(); Emit(); Save();
        }

        /// <summary>Vende a pilha inteira (ou o item). Itens equipados são desequipados antes.</summary>
        public static void SellAll(int uid)
        {
            var o = Owned(uid); if (o == null) return;
            string slot = SlotOf(uid);
            if (slot != "") { Eq[SlotIndex(slot)] = 0; Recalc(); }
            var d = GameData.Item(o.itemId);
            P.coins += (d != null ? d.sell : 0) * Mathf.Max(1, o.qty);
            P.items.Remove(o);
            NormalizeBag();
            InventoryChanged?.Invoke(); Emit(); Save();
        }

        /// <summary>Joga fora a pilha inteira (sem moedas). Itens equipados são desequipados antes.</summary>
        public static void Discard(int uid)
        {
            var o = Owned(uid); if (o == null) return;
            string slot = SlotOf(uid);
            if (slot != "") { Eq[SlotIndex(slot)] = 0; Recalc(); }
            P.items.Remove(o);
            NormalizeBag();
            InventoryChanged?.Invoke(); Emit(); Save();
        }

        /// <summary>Esta loja (categoria do NPC) compra este item?</summary>
        public static bool ShopBuys(string shopCategory, ItemDef d)
        {
            if (d == null || d.sell <= 0) return false;
            switch (shopCategory)
            {
                case "armas": return d.slot == "arma";
                case "armaduras": return d.slot == "capacete" || d.slot == "peitoral" || d.slot == "calca" || d.slot == "bota" || d.slot == "capa" || d.slot == "asa";
                case "joias": return d.slot == "colar" || d.slot == "brinco" || d.slot == "anel";
                case "pocoes": return d.slot == "consumivel";
                default: return false;
            }
        }

        /// <summary>Usa um consumível. Retorna o efeito para o mundo tocar ("heal", "mana", "return"...).</summary>
        public static string Use(string id)
        {
            var o = P.items.Find(x => x.itemId == id);
            if (o == null) { Notify("Você não tem " + (GameData.Item(id)?.name ?? id) + "."); return ""; }
            string fx = "";
            switch (id)
            {
                case "health_potion": P.hp = Mathf.Min(maxHp, P.hp + 40); fx = "heal"; break;
                case "mana_potion": P.mp = Mathf.Min(maxMp, P.mp + 35); fx = "mana"; break;
                case "great_potion": P.hp = maxHp; P.mp = maxMp; fx = "heal"; break;
                case "strength_elixir": atkBuff = 60f; fx = "buff"; break;
                case "return_scroll": fx = "return"; break;
                default: return "";
            }
            o.qty--;
            if (o.qty <= 0) P.items.Remove(o);
            InventoryChanged?.Invoke(); Emit();
            return fx;
        }

        /// <summary>Sorteia o conteúdo de um baú (tier 1 = chefe do andar 1, 2 = chefe final).</summary>
        public static List<string> RollChest(int tier)
        {
            var l = new List<string> { "health_potion" };
            if (UnityEngine.Random.value < 0.6f) l.Add("mana_potion");
            if (UnityEngine.Random.value < 0.5f) l.Add(UnityEngine.Random.value < 0.5f ? "strength_elixir" : "return_scroll");
            if (tier >= 2) l.Add("great_potion");
            var rares = new List<string>(); var legends = new List<string>(); var wings = new List<string>();
            foreach (var d in GameData.ItemOrder)
            {
                if (!GameData.IsEquipSlotType(d.slot)) continue;
                if (d.slot == "asa") wings.Add(d.id);
                if (d.rarity == "raro") rares.Add(d.id);
                if (d.rarity == "lendario") legends.Add(d.id);
            }
            float pRare = tier >= 2 ? 0.9f : 0.6f, pLeg = tier >= 2 ? 0.45f : 0.12f;
            if (rares.Count > 0 && UnityEngine.Random.value < pRare) l.Add(rares[UnityEngine.Random.Range(0, rares.Count)]);
            if (legends.Count > 0 && UnityEngine.Random.value < pLeg) l.Add(legends[UnityEngine.Random.Range(0, legends.Count)]);
            // asas só saem de baús de chefe
            if (wings.Count > 0 && UnityEngine.Random.value < (tier >= 2 ? 0.4f : 0.2f)) l.Add(wings[UnityEngine.Random.Range(0, wings.Count)]);
            return l;
        }
    }
}
