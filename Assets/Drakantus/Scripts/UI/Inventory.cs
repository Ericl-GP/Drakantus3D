using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Drakantus
{
    // ==========================================================================================
    // BOLSA (tecla I) — decisões de design
    // ------------------------------------------------------------------------------------------
    // * 12 slots de equipamento (GameData.EquipSlots): arma, capacete, peitoral, calca, bota, capa, asa,
    //   colar, brinco1, brinco2, anel1, anel2. O ItemDef.slot diz o TIPO ("brinco"/"anel" servem nos dois
    //   slots do par). Save: Profile.equipUids[12] (uid por slot, 0 = vazio). Save v1 (weaponUid/armorUid/
    //   ringUid, "armadura"/"joia") é migrado em GameState.Load -> Migrate().
    // * Itens equipados SAEM da mochila (OwnedItem.pos = -1). Itens na mochila têm posição fixa (OwnedItem.pos),
    //   com buracos permitidos; grade 6x6 (cresce em linhas se passar de 36). "Organizar" = GameState.SortBag().
    // * Arrastar e soltar (InvDragItem / InvDropTarget): mochila<->mochila troca de lugar; mochila->slot equipa
    //   (valida o tipo; o item antigo vai para o lugar de onde o novo saiu); slot->mochila desequipa (se soltar
    //   sobre um item compatível, troca); anel1<->anel2 troca; soltar na lixeira = descartar (sem moedas,
    //   com confirmação). VENDER só nas lojas da cidade (NPCs de armas/armaduras/joias) — ver ShopWindow.
    // * Clique direito: equipar / usar / desequipar. Dica (tooltip) com comparação +/- contra o equipado.
    // * Durante um arrasto a janela NÃO é reconstruída (invBusy adia o windowDirty), senão o arrasto quebraria.
    // * Boneco: PreviewRig = cópia do visual do herói em (0,-500,0) + câmera própria renderizando numa
    //   RenderTexture mostrada num RawImage; gira sozinho e com o arrasto do mouse. Fica desligado fora da
    //   bolsa/aparência. Usa tempo não escalado (funciona com o jogo pausado).
    // * O equipamento visível (elmo/capa/asas/tintas) e as cores de aparência ficam em Actors/Equipment.cs.
    // ==========================================================================================
    public partial class HUD
    {
        bool invBusy;                 // arrastando item / girando o boneco: adia reconstruções
        int invSellUid = -1;          // confirmação de venda pendente
        readonly List<KeyValuePair<string, Image>> invSlotGlows = new();

        const float InvCell = 82f, InvGap = 6f, InvSlot = 76f;
        const int InvCols = 6;

        public void InventoryWindow()
        {
            if (invBusy && modalGo != null && modalKind == "inventory") { windowDirty = true; return; }
            PreviewRig.Get();
            var c = OpenModal("inventory", "Bolsa", new Vector2(1500f, 880f), true);
            invSlotGlows.Clear();

            InvBuildStats(c);
            InvBuildDoll(c);
            InvBuildBag(c);
            if (GameState.Owned(invSellUid) != null) InvBuildSellPrompt();
            else invSellUid = -1;

            modalRebuild = InventoryWindow;
        }

        // ------------------------------------------------------------------ atributos (esquerda)
        void InvBuildStats(RectTransform c)
        {
            var left = UIKit.R(c, "atributos");
            UIKit.Place(left, TL, TL, Vector2.zero, new Vector2(320f, 714f));
            var st = UIKit.Round(left, "painel", new Color(0f, 0f, 0f, 0.25f), 1.4f);
            var str = UIKit.Place(st.rectTransform, TL, TL, Vector2.zero, new Vector2(320f, 420f));
            Label(str, "ATRIBUTOS", 16, UIKit.Gold, TL, TL, new Vector2(18f, -12f), new Vector2(280f, 24f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var cd = GameState.Class;
            Label(str, GameState.P.playerName + "  <size=15><color=#9aa3b5>Nv " + GameState.P.level + " · " + (string.IsNullOrEmpty(GameState.P.classId) ? "Sem classe" : cd.name) + "</color></size>",
                20, Color.white, TL, TL, new Vector2(18f, -40f), new Vector2(290f, 28f), TextAnchor.MiddleLeft, FontStyle.Bold);
            int crit = GameState.SumStat(5);
            string[] names = { "Ataque", "Defesa", "Vida máxima", "Mana máxima", "Velocidade", "Crítico (itens)" };
            string[] vals =
            {
                GameState.AttackPower().ToString(), GameState.Defense().ToString(),
                Mathf.RoundToInt(GameState.maxHp).ToString(), Mathf.RoundToInt(GameState.maxMp).ToString(),
                GameState.MoveSpeed().ToString("0.0"), "+" + crit + "%"
            };
            int[] bonus = { GameState.SumStat(0), GameState.SumStat(1), GameState.SumStat(2), GameState.SumStat(3), GameState.SumStat(4), 0 };
            string[] bonusSuffix = { " atq", " def", " hp", " mp", "%", "" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = -82f - i * 52f;
                var row = UIKit.Round(str, "linha", new Color(1f, 1f, 1f, i % 2 == 0 ? 0.035f : 0f), 1.6f);
                UIKit.Place(row.rectTransform, TL, TL, new Vector2(10f, y + 4f), new Vector2(300f, 46f));
                Label(str, names[i], 18, UIKit.DimText, TL, TL, new Vector2(20f, y), new Vector2(170f, 26f));
                Label(str, vals[i], 21, Color.white, TL, TL, new Vector2(170f, y), new Vector2(130f, 26f), TextAnchor.MiddleRight, FontStyle.Bold);
                if (bonus[i] != 0)
                    Label(str, "itens +" + bonus[i] + bonusSuffix[i], 13, new Color(0.55f, 0.85f, 0.55f), TL, TL, new Vector2(20f, y - 25f), new Vector2(280f, 18f));
            }

            var coins = UIKit.R(left, "moedas");
            UIKit.Place(coins, TL, TL, new Vector2(0f, -436f), new Vector2(320f, 44f));
            var ci = UIKit.Img(coins, "icone", U.Icon(2, 8), Color.white);
            UIKit.Place(ci.rectTransform, ML, ML, new Vector2(8f, 0f), new Vector2(32f, 32f));
            Label(coins, GameState.P.coins + " moedas", 22, UIKit.Gold, ML, ML, new Vector2(50f, 0f), new Vector2(260f, 36f), TextAnchor.MiddleLeft, FontStyle.Bold);

            var bApp = UIKit.Btn(left, "Aparência", new Vector2(300f, 52f), AppearanceWindow, UIKit.BtnCol, 22);
            UIKit.Place(UIKit.RT(bApp), TL, TL, new Vector2(10f, -494f), new Vector2(300f, 52f));

            Label(left, "Botão direito: equipar / usar\nVenda itens nas lojas da cidade",
                14, UIKit.DimText, TL, TL, new Vector2(12f, -560f), new Vector2(300f, 60f), TextAnchor.UpperLeft, FontStyle.Italic);
        }

        // ------------------------------------------------------------------ boneco + slots (centro)
        static readonly string[] InvSlotOrder = { "capacete", "colar", "peitoral", "capa", "calca", "bota", "brinco1", "brinco2", "asa", "anel1", "anel2", "arma" };
        static readonly Vector2[] InvSlotPos =
        {
            new Vector2(0f, 290f),
            new Vector2(-215f, 190f), new Vector2(-215f, 95f), new Vector2(-215f, 0f), new Vector2(-215f, -95f), new Vector2(-215f, -190f),
            new Vector2(215f, 190f), new Vector2(215f, 95f), new Vector2(215f, 0f), new Vector2(215f, -95f), new Vector2(215f, -190f),
            new Vector2(0f, -290f)
        };

        void InvBuildDoll(RectTransform c)
        {
            var area = UIKit.R(c, "boneco");
            UIKit.Place(area, TL, TL, new Vector2(340f, 0f), new Vector2(540f, 714f));
            var bg = UIKit.Round(area, "fundo", new Color(0f, 0f, 0f, 0.22f), 1.4f);
            UIKit.Stretch(bg.rectTransform);
            var halo = UIKit.Img(area, "halo", UIKit.SoftSprite(), new Color(1f, 0.78f, 0.45f, 0.09f));
            UIKit.Place(halo.rectTransform, MID, MID, new Vector2(0f, 10f), new Vector2(460f, 560f));

            var rig = PreviewRig.Get();
            var raw = UIKit.R(area, "previa").gameObject.AddComponent<RawImage>();
            raw.texture = rig.Texture;
            raw.raycastTarget = true;
            UIKit.Place(raw.rectTransform, MID, MID, Vector2.zero, new Vector2(340f, 500f));
            raw.gameObject.AddComponent<DollRotate>();
            var floor = UIKit.Img(area, "sombra", UIKit.SoftSprite(), new Color(0f, 0f, 0f, 0.35f));
            UIKit.Place(floor.rectTransform, MID, MID, new Vector2(0f, -232f), new Vector2(220f, 40f));
            floor.transform.SetSiblingIndex(raw.transform.GetSiblingIndex());

            for (int i = 0; i < InvSlotOrder.Length; i++) InvEquipSlot(area, InvSlotOrder[i], InvSlotPos[i]);
        }

        void InvEquipSlot(RectTransform area, string slot, Vector2 pos)
        {
            int uid = GameState.EquippedUid(slot);
            var o = GameState.Owned(uid);
            var d = o != null ? GameData.Item(o.itemId) : null;

            var frame = UIKit.MetalFrame(area, "slot_" + slot, d != null ? UIKit.RarityBorder(d) : new Color(0.36f, 0.29f, 0.21f, 1f), true);
            var fr = UIKit.Place(frame.rectTransform, MID, MID, pos, new Vector2(InvSlot, InvSlot));
            var inner = UIKit.Round(fr, "fundo", UIKit.SlotBg, 1.6f);
            UIKit.Stretch(inner.rectTransform, 4f);
            var glow = UIKit.Round(fr, "destaque", new Color(0.4f, 1f, 0.5f, 0.28f), 1.6f);
            UIKit.Stretch(glow.rectTransform, 3f);
            glow.enabled = false;
            invSlotGlows.Add(new KeyValuePair<string, Image>(slot, glow));

            if (d != null)
            {
                var ic = UIKit.Img(fr, "icone", U.Icon(d.icon), Color.white);
                UIKit.Place(ic.rectTransform, MID, MID, Vector2.zero, new Vector2(58f, 58f));
                ic.preserveAspect = true;
                if (d.rarity == "lendario") AddLegendGlow(frame);
                var drag = frame.gameObject.AddComponent<InvDragItem>();
                drag.uid = uid; drag.fromSlot = slot; drag.icon = ic;
                var ck = frame.gameObject.AddComponent<UIClick>();
                string s = slot;
                ck.onRight = () => { GameState.Unequip(s); InvAfterEquip(null); };
                var dd = d;
                UITip.Add(frame.gameObject, () => dd.name, () => ItemTipCompare(dd, uid) + "\n<color=#9aa3b5>Botão direito: tirar</color>");
            }
            else
            {
                var t = UIKit.Txt(fr, "nome", SlotShort(slot), 13, new Color(1f, 1f, 1f, 0.32f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(t.rectTransform, 4f);
                string s = slot;
                UITip.Add(frame.gameObject, () => GameData.SlotLabel(s), () => "<color=#9aa3b5>Vazio — arraste um item para cá.</color>");
            }
            frame.gameObject.AddComponent<UIHover>().scale = 1.06f;
            var drop = frame.gameObject.AddComponent<InvDropTarget>();
            drop.kind = InvDropTarget.Kind.Slot; drop.slot = slot; drop.highlight = glow;
        }

        static string SlotShort(string slot)
        {
            switch (slot)
            {
                case "brinco1": case "brinco2": return "Brinco";
                case "anel1": case "anel2": return "Anel";
            }
            return GameData.SlotLabel(slot);
        }

        // ------------------------------------------------------------------ mochila (direita)
        void InvBuildBag(RectTransform c)
        {
            var right = UIKit.R(c, "mochila");
            UIKit.Place(right, TL, TL, new Vector2(892f, 0f), new Vector2(552f, 714f));
            int count = 0;
            foreach (var o in GameState.P.items) if (!GameState.IsEquipped(o.uid)) count++;
            Label(right, "MOCHILA  <color=#9aa3b5>(" + count + " itens)</color>", 16, UIKit.Gold, TL, TL, new Vector2(0f, -10f), new Vector2(330f, 30f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var bSort = UIKit.Btn(right, "Organizar", new Vector2(160f, 42f), () =>
            {
                GameState.SortBag();
                windowDirty = true;
            }, UIKit.BtnCol, 19);
            UIKit.Place(UIKit.RT(bSort), TR, TR, new Vector2(0f, -4f), new Vector2(160f, 42f));

            ScrollRect sr;
            var content = UIKit.ScrollArea(right, "grade", out sr);
            UIKit.Place(UIKit.RT(sr), TL, TL, new Vector2(0f, -54f), new Vector2(552f, 560f));
            int rows = Mathf.Max(BagRowsMin, Mathf.CeilToInt(Mathf.Max(GameState.BagSize, GameState.BagExtent()) / (float)InvCols));
            content.sizeDelta = new Vector2(0f, rows * (InvCell + InvGap) + 10f);
            var byPos = new Dictionary<int, OwnedItem>();
            foreach (var o in GameState.P.items)
                if (!GameState.IsEquipped(o.uid) && o.pos >= 0 && !byPos.ContainsKey(o.pos)) byPos[o.pos] = o;
            for (int p = 0; p < rows * InvCols; p++)
            {
                byPos.TryGetValue(p, out var o);
                InvBagCell(content, p, o);
            }

            // lixeira
            var trash = UIKit.Round(right, "lixeira", new Color(0.32f, 0.1f, 0.1f, 0.55f), 1.4f, true);
            UIKit.Place(trash.rectTransform, TL, TL, new Vector2(0f, -626f), new Vector2(552f, 84f));
            var to = trash.gameObject.AddComponent<Outline>();
            to.effectColor = new Color(1f, 0.4f, 0.35f, 0.35f); to.effectDistance = new Vector2(1.5f, -1.5f); to.useGraphicAlpha = false;
            var thl = UIKit.Round(trash.rectTransform, "destaque", new Color(1f, 0.35f, 0.3f, 0.35f), 1.4f);
            UIKit.Stretch(thl.rectTransform, 2f);
            thl.enabled = false;
            var tt = UIKit.Txt(trash.rectTransform, "texto", "DESCARTAR", 18, new Color(1f, 0.75f, 0.7f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(tt.rectTransform);
            var td = trash.gameObject.AddComponent<InvDropTarget>();
            td.kind = InvDropTarget.Kind.Trash; td.highlight = thl;
        }

        const int BagRowsMin = 6;

        void InvBagCell(RectTransform parent, int pos, OwnedItem o)
        {
            int col = pos % InvCols, row = pos / InvCols;
            var d = o != null ? GameData.Item(o.itemId) : null;
            Vector2 at = new Vector2(8f + col * (InvCell + InvGap), -6f - row * (InvCell + InvGap));
            Image cell;
            if (d == null)
            {
                cell = UIKit.Round(parent, "vazio_" + pos, new Color(0.06f, 0.045f, 0.034f, 1f), 1.6f, true);
                var eo = cell.gameObject.AddComponent<Outline>();
                eo.effectColor = new Color(UIKit.Bronze.r, UIKit.Bronze.g, UIKit.Bronze.b, 0.12f); eo.effectDistance = new Vector2(1f, -1f); eo.useGraphicAlpha = false;
            }
            else cell = UIKit.MetalFrame(parent, "item_" + o.uid, UIKit.RarityBorder(d), true);
            var cr = UIKit.Place(cell.rectTransform, TL, TL, at, new Vector2(InvCell, InvCell));
            var hl = UIKit.Round(cr, "destaque", new Color(1f, 0.84f, 0.42f, 0.3f), 1.6f);
            UIKit.Stretch(hl.rectTransform, 2f);
            hl.enabled = false;

            if (d != null)
            {
                var inner = UIKit.Round(cr, "fundo", UIKit.SlotBg, 1.6f);
                UIKit.Stretch(inner.rectTransform, 4f);
                inner.transform.SetSiblingIndex(0);
                var ic = UIKit.Img(cr, "icone", U.Icon(d.icon), Color.white);
                UIKit.Place(ic.rectTransform, MID, MID, Vector2.zero, new Vector2(60f, 60f));
                ic.preserveAspect = true; ic.enabled = ic.sprite != null;
                if (o.qty > 1)
                {
                    var q = UIKit.Txt(cr, "qtd", o.qty.ToString(), 17, Color.white, TextAnchor.LowerRight, FontStyle.Bold, true);
                    UIKit.Stretch(q.rectTransform, 0f, 7f, 0f, 3f);
                }
                if (d.rarity == "lendario") AddLegendGlow(cell);
                cell.gameObject.AddComponent<UIHover>().scale = 1.06f;
                int uid = o.uid;
                var drag = cell.gameObject.AddComponent<InvDragItem>();
                drag.uid = uid; drag.fromSlot = ""; drag.icon = ic;
                var click = cell.gameObject.AddComponent<UIClick>();
                click.onRight = () => InvQuick(uid);
                string hint = d.slot == "consumivel" ? "usar" : Equippable(d) ? "equipar" : "";
                UITip.Add(cell.gameObject, () => d.name, () => ItemTipCompare(d, uid) + (hint != "" ? "\n<color=#9aa3b5>Botão direito: " + hint + "</color>" : ""));
            }
            var drop = cell.gameObject.AddComponent<InvDropTarget>();
            drop.kind = InvDropTarget.Kind.Bag; drop.pos = pos; drop.highlight = hl;
        }

        // ------------------------------------------------------------------ ações
        void InvQuick(int uid)
        {
            var o = GameState.Owned(uid);
            var d = o != null ? GameData.Item(o.itemId) : null;
            if (d == null) return;
            if (Equippable(d))
            {
                GameState.Equip(uid);
                InvAfterEquip(d);
            }
            else if (d.slot == "consumivel")
            {
                if (d.id == "return_scroll" && Game.I != null && Hero != null) ForceClose();
                if (Game.I != null && Hero != null) Game.I.UsePotion(d.id);
                else if (string.IsNullOrEmpty(GameState.Use(d.id))) Sfx.Play("ui_error");
                windowDirty = true;
            }
        }

        /// <summary>Depois de qualquer troca de equipamento: atualiza herói, boneco, som e janela.</summary>
        void InvAfterEquip(ItemDef d)
        {
            var p = Hero;
            if (p != null) p.RefreshEquipment();
            PreviewRig.MarkDirty();
            if (d != null) Sfx.Play(d.rarity == "lendario" ? "item_legendary" : d.rarity == "raro" ? "item_rare" : "ui_click");
            else Sfx.Play("ui_click");
            windowDirty = true;
        }

        internal RectTransform InvBeginDrag(InvDragItem src)
        {
            var o = GameState.Owned(src.uid);
            var d = o != null ? GameData.Item(o.itemId) : null;
            if (d == null) return null;
            invBusy = true;
            HideTip(null);
            foreach (var kv in invSlotGlows)
                if (kv.Value != null) kv.Value.enabled = GameState.CanEquipIn(d, kv.Key);
            var g = UIKit.MetalFrame(tipLayer, "arrastando_item", UIKit.RarityBorder(d));
            var rt = g.rectTransform;
            rt.anchorMin = rt.anchorMax = MID; rt.pivot = MID; rt.sizeDelta = new Vector2(InvCell, InvCell);
            var cg = g.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false; cg.alpha = 0.92f;
            var inner = UIKit.Round(rt, "fundo", UIKit.SlotBg, 1.6f);
            UIKit.Stretch(inner.rectTransform, 3f);
            var ic = UIKit.Img(rt, "icone", U.Icon(d.icon), Color.white);
            UIKit.Place(ic.rectTransform, MID, MID, Vector2.zero, new Vector2(62f, 62f));
            ic.preserveAspect = true;
            rt.localScale = Vector3.one * 1.1f;
            Sfx.Play("ui_click", null, 0.6f);
            return rt;
        }

        internal void InvEndDrag(InvDragItem src, Vector2 screenPos, bool handled)
        {
            invBusy = false;
            foreach (var kv in invSlotGlows) if (kv.Value != null) kv.Value.enabled = false;
            windowDirty = true;
        }

        internal void InvSetBusy(bool on)
        {
            invBusy = on;
            if (!on) windowDirty = true;
        }

        internal bool InvCanDrop(InvDragItem src, InvDropTarget dst)
        {
            if (src == null || dst == null) return false;
            if (dst.kind != InvDropTarget.Kind.Slot) return true;
            var o = GameState.Owned(src.uid);
            return o != null && GameState.CanEquipIn(GameData.Item(o.itemId), dst.slot);
        }

        internal void InvDrop(InvDragItem src, InvDropTarget dst)
        {
            var o = GameState.Owned(src.uid);
            var d = o != null ? GameData.Item(o.itemId) : null;
            if (d == null) return;
            switch (dst.kind)
            {
                case InvDropTarget.Kind.Bag:
                    if (string.IsNullOrEmpty(src.fromSlot))
                    {
                        GameState.MoveInBag(src.uid, dst.pos);
                        Sfx.Play("ui_click");
                    }
                    else
                    {
                        var occ = GameState.ItemAt(dst.pos);
                        var occD = occ != null ? GameData.Item(occ.itemId) : null;
                        if (occ != null && GameState.CanEquipIn(occD, src.fromSlot))
                        {
                            GameState.EquipTo(occ.uid, src.fromSlot);   // o equipado vai para a posição do outro
                            InvAfterEquip(occD);
                        }
                        else
                        {
                            GameState.Unequip(src.fromSlot, dst.pos);
                            InvAfterEquip(null);
                        }
                    }
                    break;
                case InvDropTarget.Kind.Slot:
                    if (!GameState.CanEquipIn(d, dst.slot))
                    {
                        Sfx.Play("ui_error");
                        Toast(Equippable(d) ? d.name + " vai no slot: " + GameData.SlotLabel(d.slot) + "." : d.name + " não pode ser equipado.");
                        break;
                    }
                    if (src.fromSlot == dst.slot) break;
                    GameState.EquipTo(src.uid, dst.slot);
                    InvAfterEquip(d);
                    break;
                case InvDropTarget.Kind.Trash:
                    InvAskSell(src.uid);
                    break;
            }
            windowDirty = true;
        }

        // ------------------------------------------------------------------ vender (confirmação)
        void InvAskSell(int uid)
        {
            if (GameState.Owned(uid) == null) return;
            invSellUid = uid;
            Sfx.Play("ui_open", null, 0.6f);
            windowDirty = true;
        }

        void InvBuildSellPrompt()
        {
            if (modalWin == null) return;
            var o = GameState.Owned(invSellUid);
            var d = o != null ? GameData.Item(o.itemId) : null;
            if (d == null) { invSellUid = -1; return; }
            var dim = UIKit.Round(modalWin, "venda_escuro", new Color(0f, 0f, 0f, 0.6f), 1f, true);
            UIKit.Stretch(dim.rectTransform);
            var card = UIKit.Panel(dim.rectTransform, "venda", UIKit.PanelSolid);
            var cr = UIKit.Place(card.rectTransform, MID, MID, Vector2.zero, new Vector2(560f, 300f));
            IconBox(cr, U.Icon(d.icon), UIKit.RarityBorder(d), TC, TC, new Vector2(0f, -22f), 84f, 64f, d.rarity == "lendario");
            bool equipped = GameState.IsEquipped(o.uid);
            int uid = o.uid;
            if (d.sell <= 0)
            {
                Label(cr, d.name + " não pode ser descartado.", 22, UIKit.TextCol, TC, TC, new Vector2(0f, -122f), new Vector2(520f, 34f), TextAnchor.MiddleCenter, FontStyle.Bold);
                var ok = UIKit.Btn(cr, "Ok", new Vector2(200f, 52f), () => { invSellUid = -1; windowDirty = true; }, UIKit.BtnCol, 22);
                UIKit.Place(UIKit.RT(ok), BC, BC, new Vector2(0f, 22f), new Vector2(200f, 52f));
                return;
            }
            string qty = o.qty > 1 ? " (x" + o.qty + ")" : "";
            Label(cr, "Descartar <color=#" + UIKit.ColorHex(GameData.RarityColor(d)) + ">" + d.name + "</color>" + qty + "?", 23, UIKit.TextCol, TC, TC, new Vector2(0f, -116f), new Vector2(520f, 34f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Label(cr, (equipped ? "Está equipado — será retirado.  " : "") + "O item some e você <b>não</b> recebe moedas. Para vender, use as lojas da cidade.",
                16, UIKit.DimText, TC, TC, new Vector2(0f, -152f), new Vector2(530f, 44f), TextAnchor.MiddleCenter);
            var b1 = UIKit.Btn(cr, "Descartar", new Vector2(170f, 52f), () =>
            {
                GameState.Discard(uid);
                Sfx.Play("ui_click");
                invSellUid = -1;
                InvAfterEquip(null);
            }, UIKit.DangerCol, 21);
            UIKit.Place(UIKit.RT(b1), BC, BC, new Vector2(-110f, 22f), new Vector2(170f, 52f));
            var bc = UIKit.Btn(cr, "Cancelar", new Vector2(170f, 52f), () => { invSellUid = -1; windowDirty = true; }, UIKit.BtnCol, 21);
            UIKit.Place(UIKit.RT(bc), BC, BC, new Vector2(110f, 22f), new Vector2(170f, 52f));
        }

        // ------------------------------------------------------------------ dica com comparação
        /// <summary>Texto da dica do item; compara com o que está equipado no slot correspondente. uid &lt;= 0 = item da loja.</summary>
        string ItemTipCompare(ItemDef d, int uid)
        {
            if (d == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#").Append(UIKit.ColorHex(GameData.RarityColor(d))).Append(">").Append(GameData.RarityName(d)).Append("</color> · ").Append(SlotName(d.slot));
            string st = ItemStats(d);
            if (!string.IsNullOrEmpty(st)) sb.Append("\n<color=#FFD76A>").Append(st).Append("</color>");
            if (!string.IsNullOrEmpty(d.description)) sb.Append("\n").Append(d.description);
            if (d.slot == "arma" && !string.IsNullOrEmpty(d.wtype))
            {
                bool ok = GameData.ClassCanUse(GameState.Class, d) || !GameState.HasChosenClass;
                sb.Append("\n<color=#").Append(ok ? "9aa3b5" : "ff7a6a").Append(">").Append(GameData.WeaponTypeName(d.wtype))
                  .Append(" · usada por: ").Append(GameData.ClassesForWeapon(d.wtype)).Append("</color>");
                if (!ok) sb.Append("\n<color=#ff7a6a>Sua classe não pode equipar. Venda na Armaria.</color>");
            }
            if (Equippable(d))
            {
                string where = uid > 0 ? GameState.SlotOf(uid) : "";
                if (where != "") sb.Append("\n<color=#8fd18a>Equipado (").Append(GameData.SlotLabel(where)).Append(")</color>");
                else
                {
                    string slot = GameState.DefaultSlotFor(d);
                    var cur = GameState.Equipped(slot);
                    sb.Append("\n\n<color=#9aa3b5>").Append(cur != null ? "Comparado com: " + cur.name : "Slot vazio").Append("</color>");
                    var lines = new List<string>();
                    InvDiff(lines, "ATQ", d.atk, cur?.atk ?? 0, "");
                    InvDiff(lines, "DEF", d.def, cur?.def ?? 0, "");
                    InvDiff(lines, "HP", d.hp, cur?.hp ?? 0, "");
                    InvDiff(lines, "MP", d.mp, cur?.mp ?? 0, "");
                    InvDiff(lines, "CRIT", d.crit, cur?.crit ?? 0, "%");
                    InvDiff(lines, "VEL", d.speed, cur?.speed ?? 0, "%");
                    if (lines.Count > 0) sb.Append("\n").Append(string.Join("   ", lines));
                }
            }
            if (uid <= 0 || GameState.Owned(uid) != null) sb.Append("\n<color=#9aa3b5>Venda: ").Append(d.sell).Append(" moedas</color>");
            return sb.ToString();
        }

        static void InvDiff(List<string> l, string name, int mine, int cur, string suf)
        {
            if (mine == 0 && cur == 0) return;
            int delta = mine - cur;
            string col = delta > 0 ? "7ee07e" : delta < 0 ? "ff7a6a" : "9aa3b5";
            l.Add("<color=#" + col + ">" + name + " " + (delta > 0 ? "+" : "") + delta + suf + "</color>");
        }
    }

    // ======================================================================== arrastar/soltar itens
    /// <summary>Item arrastável (célula da mochila ou slot de equipamento com item).</summary>
    public class InvDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int uid;
        public string fromSlot = "";   // "" = mochila
        public Image icon;
        public static InvDragItem current;
        public static bool handled;
        RectTransform ghost;

        public void OnBeginDrag(PointerEventData e)
        {
            if (HUD.I == null || e.button != PointerEventData.InputButton.Left) return;
            handled = false;
            ghost = HUD.I.InvBeginDrag(this);
            if (ghost == null) return;
            current = this;
            if (icon != null) icon.color = new Color(1f, 1f, 1f, 0.3f);
            HUD.I.MoveToPointer(ghost, e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            if (ghost != null && HUD.I != null) HUD.I.MoveToPointer(ghost, e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (ghost == null) return;
            Destroy(ghost.gameObject);
            ghost = null;
            if (icon != null) icon.color = Color.white;
            if (current == this) current = null;
            if (HUD.I != null) HUD.I.InvEndDrag(this, e.position, handled);
            handled = false;
        }

        void OnDisable()
        {
            if (ghost == null) return;
            Destroy(ghost.gameObject);
            ghost = null;
            if (current == this) current = null;
            if (HUD.I != null) HUD.I.InvSetBusy(false);
        }
    }

    /// <summary>Lugar que recebe um item arrastado: posição da mochila, slot de equipamento ou lixeira.</summary>
    public class InvDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public enum Kind { Bag, Slot, Trash }
        public Kind kind;
        public int pos = -1;
        public string slot = "";
        public Image highlight;
        Color baseCol; bool hasBase;

        public void OnDrop(PointerEventData e)
        {
            Restore();
            var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<InvDragItem>() : null;
            if (src == null || HUD.I == null) return;
            if (src.gameObject == gameObject) { InvDragItem.handled = true; return; }   // soltou no mesmo lugar
            InvDragItem.handled = true;
            HUD.I.InvDrop(src, this);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (highlight == null || !e.dragging || e.pointerDrag == null) return;
            var src = e.pointerDrag.GetComponent<InvDragItem>();
            if (src == null || HUD.I == null) return;
            if (!hasBase) { baseCol = highlight.color; hasBase = true; }
            bool ok = HUD.I.InvCanDrop(src, this);
            highlight.color = ok ? baseCol : new Color(1f, 0.25f, 0.2f, 0.35f);
            highlight.enabled = true;
        }

        public void OnPointerExit(PointerEventData e) { Restore(); }

        void Restore()
        {
            if (highlight == null) return;
            if (hasBase) highlight.color = baseCol;
            // os slots compatíveis continuam acesos durante o arrasto
            bool keep = kind == Kind.Slot && InvDragItem.current != null && HUD.I != null && HUD.I.InvCanDrop(InvDragItem.current, this);
            highlight.enabled = keep;
        }
    }

    /// <summary>Arrastar o boneco gira a prévia.</summary>
    public class DollRotate : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        bool dragging;
        public void OnBeginDrag(PointerEventData e) { dragging = true; if (HUD.I != null) HUD.I.InvSetBusy(true); }
        public void OnDrag(PointerEventData e) { if (PreviewRig.I != null) PreviewRig.I.Rotate(e.delta.x); }
        public void OnEndDrag(PointerEventData e) { dragging = false; if (HUD.I != null) HUD.I.InvSetBusy(false); }
        void OnDisable() { if (dragging && HUD.I != null) HUD.I.InvSetBusy(false); dragging = false; }
    }

    // ======================================================================== boneco de prévia
    /// <summary>
    /// Cópia do herói longe da cena (0,-500,0) com câmera própria -> RenderTexture.
    /// Ativo só com a bolsa ou a aparência abertas. Também aplica (com limite de frequência) as mudanças
    /// de aparência no herói e salva o jogo pouco depois.
    /// </summary>
    public class PreviewRig : MonoBehaviour
    {
        public static PreviewRig I;
        public RenderTexture Texture { get; private set; }

        Camera cam;
        Transform pivot;
        CharacterVisual vis;
        string builtModel = "";
        bool dirty = true, wasActive;
        float yaw = -18f, spinPause;
        static bool appearanceDirty;
        float nextApply, saveAt = -1f;

        public static PreviewRig Get()
        {
            if (I != null) return I;
            var g = new GameObject("PreviaBoneco");
            g.transform.position = new Vector3(0f, -500f, 0f);
            I = g.AddComponent<PreviewRig>();
            I.Init();
            return I;
        }

        public static void MarkDirty() { if (I != null) I.dirty = true; }

        /// <summary>Chamado pela janela de Aparência ao mudar cor/tom.</summary>
        public static void AppearanceChanged() { appearanceDirty = true; Get(); }

        public void Rotate(float dx)
        {
            yaw -= dx * 0.45f;
            spinPause = 3f;
        }

        void Init()
        {
            Texture = new RenderTexture(510, 750, 24, RenderTextureFormat.ARGB32) { name = "PreviaBoneco", antiAliasing = 2 };
            Texture.Create();

            pivot = new GameObject("pivo").transform;
            pivot.SetParent(transform, false);
            var vg = new GameObject("visual");
            vg.transform.SetParent(pivot, false);
            vis = vg.AddComponent<CharacterVisual>();

            var cg = new GameObject("camera");
            cg.transform.SetParent(transform, false);
            cam = cg.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.1f, 0f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
            cam.depth = -30f;
            cam.allowHDR = false;
            cam.useOcclusionCulling = false;
            cam.targetTexture = Texture;
            var urp = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            if (urp != null) { urp.renderShadows = false; urp.renderPostProcessing = false; }

            AddLight("luz_frente", new Vector3(1.6f, 2.6f, 3.2f), U.Hex("fff1dc"), 2.4f);
            AddLight("luz_contra", new Vector3(-2.2f, 2.2f, -2.4f), U.Hex("9fb8ff"), 1.6f);
            AddLight("luz_baixo", new Vector3(0f, 0.4f, 2.4f), U.Hex("ffd6a0"), 0.6f);

            GameState.InventoryChanged += OnInv;
            GameState.LoadoutChanged += OnInv;
        }

        void AddLight(string n, Vector3 p, Color c, float intensity)
        {
            var g = new GameObject(n);
            g.transform.SetParent(transform, false);
            g.transform.localPosition = p;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = 9f; l.shadows = LightShadows.None;
        }

        void OnInv() { dirty = true; }

        void OnDestroy()
        {
            GameState.InventoryChanged -= OnInv;
            GameState.LoadoutChanged -= OnInv;
            if (cam != null) cam.targetTexture = null;
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
            if (I == this) I = null;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            // aparência: aplica no herói no máx. 10x/s e salva 1 s depois da última mudança
            if (appearanceDirty && now >= nextApply)
            {
                appearanceDirty = false;
                nextApply = now + 0.1f;
                var p = Game.I != null ? Game.I.player : null;
                if (p != null) p.RefreshEquipment();
                dirty = true;
                saveAt = now + 1f;
            }
            if (saveAt > 0f && now >= saveAt) { saveAt = -1f; GameState.Save(); }

            string mk = HUD.I != null ? HUD.I.ModalKind : "";
            bool want = mk == "inventory" || mk == "appearance";
            if (cam.enabled != want) cam.enabled = want;
            if (pivot.gameObject.activeSelf != want) pivot.gameObject.SetActive(want);
            if (!want) { wasActive = false; return; }

            string model = "hero_" + GameState.Class.model;
            if (model != builtModel) Rebuild(model);
            else if (dirty) ApplyEquip();
            if (!wasActive)
            {
                wasActive = true;
                if (vis.animator != null) vis.animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                vis.Play("Idle", 0f, 0f, true);
            }

            if (spinPause > 0f) spinPause -= Time.unscaledDeltaTime;
            else yaw += Time.unscaledDeltaTime * 22f;
            pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void Rebuild(string model)
        {
            builtModel = model;
            vis.Build(model, U.Hex(GameState.Class.color));
            if (vis.animator != null) vis.animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            vis.Play("Idle", 0f, 0f, true);
            float h = Mathf.Max(1.2f, vis.height);
            // enquadra o boneco inteiro (com folga para asas e elmo)
            float half = h * 0.62f;
            float dist = half / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.localPosition = new Vector3(0f, h * 0.52f, dist);
            cam.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, h * 0.5f, 0f) - cam.transform.localPosition);
            cam.farClipPlane = dist + 10f;
            ApplyEquip();
        }

        void ApplyEquip()
        {
            dirty = false;
            Equipment.WeaponFor(out string w, out string off, out string glow);
            vis.SetWeapon(w, off, glow);
            vis.SetTint(Color.white);
            Equipment.Apply(vis);
        }
    }
}
