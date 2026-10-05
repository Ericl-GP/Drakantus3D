"""Gera os JSON de Assets/Drakantus/Resources/Data a partir dos dados da versão 2D.
Distâncias em metros (1 tile 2D de 16 px = 1 m). Pode editar os JSON direto."""
import json, os
SRC = "/home/claude/proj/data"
OUT = "/home/claude/d3d/Assets/Drakantus/Resources/Data"
os.makedirs(OUT, exist_ok=True)
M = 1 / 16.0

def dump(name, obj):
    json.dump(obj, open(os.path.join(OUT, name), "w", encoding="utf-8"), indent=1, ensure_ascii=False)

# ---------------- classes
classes = [
 {"id": "guerreiro", "name": "Guerreiro", "color": "e36a5a", "icon": [0, 45], "desc": "Espadachim ágil. Combos rápidos e investidas.",
  "hp": 1.0, "atk": 1, "def": 0, "speed": 1.0, "ranged": "", "mpBonus": 0, "reach": 2.2, "block": 0.2,
  "model": "Barbarian", "weapon": "axe", "offhand": ""},
 {"id": "arqueiro", "name": "Arqueiro", "color": "8fd18a", "icon": [11, 99], "desc": "Ataca à distância com flechas. Rápido e frágil.",
  "hp": 0.9, "atk": 0, "def": 0, "speed": 1.1, "ranged": "arrow", "mpBonus": 0, "reach": 2.0, "block": 0.25,
  "model": "Ranger", "weapon": "bow", "offhand": ""},
 {"id": "mago", "name": "Mago", "color": "b9a6ff", "icon": [7, 48], "desc": "Lança orbes arcanos, explosões e cura.",
  "hp": 0.85, "atk": 0, "def": 0, "speed": 1.0, "ranged": "orb", "mpBonus": 40, "reach": 2.0, "block": 0.25,
  "model": "Mage", "weapon": "staff", "offhand": ""},
 {"id": "tank", "name": "Tank", "color": "8fb4ff", "icon": [5, 53], "desc": "Cavaleiro de escudo: muita vida, defesa forte e controle.",
  "hp": 1.5, "atk": 0, "def": 4, "speed": 0.9, "ranged": "", "mpBonus": 0, "reach": 2.0, "block": 0.08,
  "model": "Knight", "weapon": "sword", "offhand": "shield"},
]
dump("classes.json", {"classes": classes})

# ---------------- skills (da versão 2D, convertendo px -> m e efeitos -> VFX 3D)
src = json.load(open(os.path.join(SRC, "skills.json")))["skills"]
COL = {"guerreiro": "ff8a4a", "arqueiro": "8fe08a", "mago": "b98aff", "tank": "8fc4ff"}
VFX = {  # estilo, cor
 "spin": [("ring", "ffffff"), ("burst", "ffd2a0")], "charge": [("dust", "c8b090")], "warcry": [("pillar", "ff5a3a"), ("burst", "ffb347")],
 "leap": [("ring", "c8a070"), ("dust", "c8b090"), ("burst", "ffe0a0")], "whirlwind": [("ring", "e8f0ff")], "execute": [("slash", "ff4a3a")],
 "volley": [], "pierce": [("burst", "ffe08a")], "frost_arrow": [], "trap": [], "roll": [("dust", "d8e0c8")], "arrow_rain": [],
 "fireball": [], "heal": [("pillar", "8fe08a"), ("sparkle", "c8ffc0")], "blast": [("burst", "b98aff"), ("ring", "d8b8ff")],
 "chain": [], "blink": [("burst", "c58bff")], "meteor": [("burst", "ff7a2a"), ("ring", "ffb347"), ("dust", "6a4a3a")],
 "slam": [("ring", "c8a070"), ("dust", "c8b090")], "fortress": [("shield", "8fc4ff")], "bash": [("dust", "c8b090")],
 "taunt": [("ring", "ff5a5a"), ("pillar", "ff8a6a")], "quake": [], "guardian": [("pillar", "ffe08a"), ("sparkle", "fff2c0")],
}
skills = []
for sid, s in src.items():
    d = {"id": sid, "classId": s["class"], "name": s["name"], "desc": s["desc"], "level": s["level"], "mp": s["mp"], "cd": s["cd"],
         "icon": s["icon"], "type": s["type"], "color": COL[s["class"]]}
    for k in ["radius", "range", "area", "hit_radius", "spacing", "trigger", "distance", "nova_radius", "jump_range", "aoe"]:
        if k in s: d[{"hit_radius": "hitRadius", "nova_radius": "novaRadius", "jump_range": "jumpRange"}.get(k, k)] = round(s[k] * M, 2)
    if "speed" in s and s["type"] == "dash_strike": d["speed"] = round(s["speed"] * M, 2)
    elif "speed" in s: d["speedMult"] = s["speed"]
    for k in ["mult", "stun", "time", "duration", "tick", "delay", "drops", "interval", "count", "spread", "jumps", "life", "percent",
              "flat", "invuln", "atk", "execute", "shield", "arc"]:
        if k in s: d[k] = s[k]
    if "taunt" in s: d["taunt"] = float(s["taunt"]) if not isinstance(s["taunt"], bool) else (s.get("duration", 5.0) if s["taunt"] else 0.0)
    if "dmg_taken" in s: d["dmgTaken"] = s["dmg_taken"]
    if "buff_duration" in s: d["buffDuration"] = s["buff_duration"]
    if "nova_mult" in s: d["novaMult"] = s["nova_mult"]
    if "proj" in s: d["proj"] = s["proj"]
    if "pierce" in s: d["pierce"] = bool(s["pierce"])
    if "shake" in s: d["shake"] = s["shake"]
    if "text" in s: d["text"] = s["text"]
    if "telegraph" in s: d["telegraph"] = s["telegraph"]
    if "falling" in s: d["falling"] = True
    d["vfx"] = [{"style": a, "color": b} for a, b in VFX.get(sid, [])]
    skills.append(d)
dump("skills.json", {"slots": ["Z", "X", "C", "V"], "skills": skills})

# ---------------- inimigos (esqueletos KayKit)
enemies = [
 {"id": "minion", "name": "Esqueleto lacaio", "model": "Skeleton_Minion", "hp": 6, "dmg": 7, "speed": 2.6, "xp": 8, "coins": [2, 5], "windup": 0.5, "scale": 1.0, "reach": 1.4, "weapon": "", "tint": ""},
 {"id": "warrior", "name": "Esqueleto guerreiro", "model": "Skeleton_Warrior", "hp": 10, "dmg": 11, "speed": 2.2, "xp": 14, "coins": [4, 10], "windup": 0.6, "scale": 1.0, "reach": 1.6, "weapon": "skel_sword", "tint": ""},
 {"id": "rogue", "name": "Esqueleto ladino", "model": "Skeleton_Rogue", "hp": 7, "dmg": 9, "speed": 3.0, "xp": 12, "coins": [4, 9], "windup": 0.4, "scale": 1.0, "reach": 1.4, "weapon": "skel_dagger", "tint": ""},
 {"id": "archer", "name": "Esqueleto arqueiro", "model": "Skeleton_Rogue", "hp": 6, "dmg": 8, "speed": 2.2, "xp": 13, "coins": [5, 10], "windup": 0.7, "scale": 1.0, "reach": 9.0, "ranged": "arrow", "weapon": "skel_crossbow", "tint": "c8d8ff"},
 {"id": "mage", "name": "Esqueleto mago", "model": "Skeleton_Mage", "hp": 8, "dmg": 10, "speed": 2.0, "xp": 16, "coins": [6, 12], "windup": 0.8, "scale": 1.0, "reach": 8.0, "ranged": "orb", "weapon": "skel_staff", "tint": ""},
 {"id": "brute", "name": "Esqueleto brutamontes", "model": "Skeleton_Warrior", "hp": 18, "dmg": 15, "speed": 1.8, "xp": 24, "coins": [8, 16], "windup": 0.8, "scale": 1.3, "reach": 2.0, "weapon": "skel_axe", "tint": "ffd8b0"},
 {"id": "king", "name": "Rei Esqueleto Grumak", "model": "Skeleton_Warrior", "hp": 40, "dmg": 14, "speed": 2.0, "xp": 45, "coins": [25, 40], "windup": 0.7, "scale": 1.7, "reach": 2.4, "weapon": "skel_axe", "tint": "ffc89a", "boss": True, "chest": 1},
 {"id": "necro", "name": "Necromante Ancião", "model": "Skeleton_Mage", "hp": 70, "dmg": 18, "speed": 1.8, "xp": 90, "coins": [60, 90], "windup": 0.75, "scale": 1.8, "reach": 9.0, "ranged": "orb", "weapon": "skel_staff", "tint": "d8a8ff", "boss": True, "chest": 2},
]
dump("enemies.json", {"enemies": enemies})

# ---------------- itens
items = json.load(open(os.path.join(SRC, "items.json")))
MODEL = {"practice_sword": "sword", "hunter_bow": "axe", "oak_bow": "bow", "apprentice_staff": "staff", "wildwood_blade": "sword",
         "frost_blade": "sword", "arcane_staff": "staff", "war_trident": "spear", "ember_saber": "sword", "sun_bow": "bow"}
out = []
for it in items["items"]:
    if it["slot"] == "visual": continue
    d = {k: it.get(k, 0) for k in ["atk", "def", "hp", "mp", "price", "sell"]}
    d.update({"id": it["id"], "name": it["name"], "category": it["category"], "slot": it["slot"], "rarity": it.get("rarity", "comum"),
              "stats": it["stats"], "description": it["description"], "icon": it["icon"], "tint": it.get("tint", it.get("color", "")),
              "model": MODEL.get(it["id"], "")})
    out.append(d)
out.append({"id": "rune_wand", "name": "Varinha rúnica", "category": "armas", "slot": "arma", "rarity": "raro", "price": 300, "sell": 150,
            "atk": 6, "def": 0, "hp": 0, "mp": 20, "stats": "ATQ +6 · MP +20", "description": "Runas que brilham ao conjurar.", "icon": [8, 98], "tint": "", "model": "wand"})
out.append({"id": "tower_shield", "name": "Escudo da torre", "category": "armaduras", "slot": "armadura", "rarity": "raro", "price": 340, "sell": 170,
            "atk": 0, "def": 10, "hp": 20, "mp": 0, "stats": "DEF +10 · HP +20", "description": "Pesado, mas nada passa.", "icon": [5, 53], "tint": "d8e6ff", "model": ""})
rar = [{"id": k, "name": v["name"], "color": v["color"], "glow": v["glow"]} for k, v in items["rarity"].items()]
dump("items.json", {"rarity": rar, "items": out})
print(len(classes), len(skills), len(enemies), len(out))
