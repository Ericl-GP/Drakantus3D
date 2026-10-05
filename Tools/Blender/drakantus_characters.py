# Drakantus 3D — visual das 12 CLASSES (Blender 5.x).
# Usa os personagens KayKit (mesmo esqueleto Rig_Medium -> as animações continuam funcionando) e
# adiciona equipamento próprio de cada classe, preso aos ossos (capacetes, ombreiras, barbas, capas...).
# Rodar no Console Python do Blender:
#   exec(open(r"C:\Users\batis\Documents\Codex\2026-10-01\oi\outputs\Drakantus3D\Tools\Blender\drakantus_characters.py", encoding="utf-8").read())
# Saída: Assets/Drakantus/Resources/Models/Heroes/hero_<classe>.fbx  +  Tools/Blender/preview_classes.png
import bpy, os, math, traceback
from mathutils import Vector, Matrix

ROOT = r"C:\Users\batis\Documents\Codex\2026-10-01\oi\outputs\Drakantus3D"
TOOLS = os.path.join(ROOT, "Tools", "Blender")
KK = os.path.join(ROOT, "Assets", "KayKit")
OUT_HEROES = os.path.join(ROOT, "Assets", "Drakantus", "Resources", "Models", "Heroes")
os.makedirs(OUT_HEROES, exist_ok=True)

DK_NO_MAIN = True
exec(open(os.path.join(TOOLS, "drakantus_weapons.py"), encoding="utf-8").read())   # helpers + make_* das armas
PREVIEW = os.path.join(TOOLS, "preview_classes.png")
PAL.update({
    "fur": (0.38, 0.27, 0.18), "fur_dk": (0.22, 0.15, 0.09), "beard": (0.48, 0.16, 0.06), "beard_dk": (0.32, 0.09, 0.04),
    "white": (0.95, 0.94, 0.90), "cloth_blue": (0.16, 0.26, 0.60), "cloth_purple": (0.30, 0.12, 0.45), "cloth_green": (0.18, 0.40, 0.20),
    "cloth_black": (0.08, 0.08, 0.10), "cloth_brown": (0.42, 0.26, 0.14), "plume_red": (0.85, 0.12, 0.10), "plume_blue": (0.25, 0.50, 0.95),
    "horn": (0.92, 0.86, 0.70), "skin": (0.90, 0.70, 0.55),
    "cloth_teal": (0.08, 0.36, 0.40), "cloth_olive": (0.30, 0.40, 0.16), "fire_glow": (1.0, 0.42, 0.08),
    "ice_glow": (0.45, 0.88, 1.0), "volt_glow": (1.0, 0.92, 0.35), "emerald_glow": (0.30, 1.0, 0.55),
})
PAL["page_glow"] = (0.88, 0.95, 1.0)
GLOW.update({"page_glow": 1.2, "fire_glow": 9, "ice_glow": 7, "volt_glow": 8, "emerald_glow": 6})

# ------------------------------------------------------------------ utilidades de personagem
def import_char(model):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(KK, "Characters", model + ".fbx"))
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    meshes = [o for o in new if o.type == "MESH"]
    return arm, meshes

def bbox(obs):
    pts = [o.matrix_world @ Vector(c) for o in obs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi

def bone_w(arm, name):
    return arm.matrix_world @ arm.data.bones[name].head_local

def attach(ob, arm, bone):
    """Prende a peça ao osso mantendo a posição no mundo (segue as animações)."""
    bpy.context.view_layer.update()
    mw = ob.matrix_world.copy()
    ob.parent = arm; ob.parent_type = "BONE"; ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    bpy.context.view_layer.update()
    ob.matrix_world = mw
    return ob

ORGANIC = {"capa", "ponta", "barba", "barba_queixo", "tranca", "bigode", "chifre", "pluma", "cone", "gola_pele",
           "lenco", "lenco2", "cachecol", "topete", "rabo", "mascara_pano"}
def group(name, parts, arm, bone):
    """Junta as peças; peças orgânicas (capas, barbas, chifres, plumas) ganham subdivisão para ficarem lisas."""
    for o in parts:
        if o.name.split(".")[0] in ORGANIC and o.type == "MESH":
            m = o.modifiers.new("sub", "SUBSURF"); m.levels = 1; m.render_levels = 1
    ob = finish(name, parts)
    return attach(ob, arm, bone)

def hide(meshes, *suffixes):
    for o in list(meshes):
        if any(o.name.endswith(s) or ("_" + s) in o.name for s in suffixes):
            bpy.data.objects.remove(o, do_unlink=True); meshes.remove(o)

def dome(name, r, loc, material, scale=(1, 1, 1), cut=0.0, subd=3):
    """Meia esfera (capacetes, ombreiras): corta abaixo de cut (em fração do raio)."""
    import bmesh
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=subd, radius=r)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, cut * r), plane_no=(0, 0, 1), clear_inner=True)
    for v in bm.verts: v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
    bmesh.ops.contextual_create(bm, geom=[e for e in bm.edges if e.is_boundary])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return place(link(bm, name, material, True), loc)

class Ref:
    """Pontos de referência do corpo, medidos nas malhas (servem para qualquer modelo KayKit)."""
    def __init__(self, arm, meshes):
        head = [o for o in meshes if o.name.split(".")[0].endswith("_Head")]
        body = [o for o in meshes if o.name.split(".")[0].endswith("_Body")]
        self.hlo, self.hhi = bbox(head); self.blo, self.bhi = bbox(body)
        self.hc = (self.hlo + self.hhi) / 2; self.hr = (self.hhi.x - self.hlo.x) / 2
        self.bc = (self.blo + self.bhi) / 2
        self.front = self.blo.y          # o herói olha para -Y
        self.back = self.bhi.y
        self.sl = bone_w(arm, "upperarm.l"); self.sr = bone_w(arm, "upperarm.r")
        self.chest = bone_w(arm, "chest"); self.hips = bone_w(arm, "hips")


# ------------------------------------------------------------------ recolorir a roupa (textura atlas do KayKit)
import numpy as np
def _rgb2hsv(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = np.max(a[..., :3], -1); mn = np.min(a[..., :3], -1); d = mx - mn + 1e-6
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6.0
    s = np.where(mx > 0, (mx - mn) / (mx + 1e-6), 0); return h % 1.0, s, mx
def _hsv2rgb(h, s, v):
    i = np.floor(h * 6) % 6; f = h * 6 - np.floor(h * 6)
    p = v * (1 - s); q = v * (1 - f * s); t = v * (1 - (1 - f) * s)
    r = np.choose(i.astype(int), [v, q, p, p, t, v]); g = np.choose(i.astype(int), [t, v, v, q, p, p]); b = np.choose(i.astype(int), [p, p, t, v, v, q])
    return r, g, b

def recolor(meshes, cid, cloth_hue=None, cloth_sat=1.0, cloth_val=1.0, metal=None, metal_mix=0.0, leather_val=1.0):
    """Gera uma textura nova para a classe: tecidos ganham outra cor, metal pode ganhar tom (ouro/azul/escuro), pele fica igual."""
    img = None
    for o in meshes:
        for slot in o.material_slots:
            m = slot.material
            if m and m.use_nodes:
                for n in m.node_tree.nodes:
                    if n.type == "TEX_IMAGE" and n.image: img = n.image; break
            if img: break
        if img: break
    if img is None: return
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    H, S, V = _rgb2hsv(a)
    skin = (H > 0.02) & (H < 0.11) & (S > 0.18) & (S < 0.62) & (V > 0.55)
    grey = S < 0.16
    leather = (H > 0.03) & (H < 0.12) & (S >= 0.35) & (V < 0.6)
    cloth = ~skin & ~grey & ~leather
    H2, S2, V2 = H.copy(), S.copy(), V.copy()
    if cloth_hue is not None:
        H2 = np.where(cloth, cloth_hue + (H - np.median(H[cloth])) * 0.25, H2) % 1.0
    S2 = np.where(cloth, np.clip(S * cloth_sat, 0, 1), S2)
    V2 = np.where(cloth, np.clip(V * cloth_val, 0, 1), V2)
    V2 = np.where(leather, np.clip(V * leather_val, 0, 1), V2)
    r, g, b = _hsv2rgb(H2, S2, V2)
    out = np.stack([r, g, b, a[..., 3]], -1)
    if metal is not None:
        mc = np.array(metal, dtype=np.float32)
        lum = a[..., :3].mean(-1, keepdims=True)
        tinted = np.clip(lum * mc * 1.25, 0, 1)
        m = (grey & (V > 0.25))[..., None]
        out[..., :3] = np.where(m, out[..., :3] * (1 - metal_mix) + tinted * metal_mix, out[..., :3])
    name = "tex_" + cid
    new = bpy.data.images.new(name, w, h, alpha=True)
    new.pixels[:] = out.ravel().tolist()
    new.filepath_raw = os.path.join(OUT_HEROES, name + ".png"); new.file_format = "PNG"; new.save()
    for o in meshes:
        for slot in o.material_slots:
            m = slot.material
            if not m or not m.use_nodes: continue
            mm = m.copy(); mm.name = "M_" + cid
            for n in mm.node_tree.nodes:
                if n.type == "TEX_IMAGE": n.image = new
            slot.material = mm

# cores de cada classe: (matiz do tecido 0..1, saturação, brilho, tom do metal, mistura do metal, brilho do couro)
COLORS = {
    "guerreiro":         (0.02, 1.15, 0.95, None, 0.0, 1.0),
    "arqueiro":          (0.24, 0.85, 0.85, None, 0.0, 1.05),
    "mago":              (0.52, 1.15, 0.85, (1.0, 0.80, 0.40), 0.35, 1.0),
    "tank":              (0.60, 1.0, 0.9, (0.75, 0.80, 0.95), 0.25, 1.0),
    "assassino":         (0.78, 0.9, 0.55, (0.35, 0.30, 0.45), 0.5, 0.6),
    "cavaleiro":         (0.00, 1.2, 0.85, (1.0, 0.78, 0.35), 0.6, 1.0),
    "pistoleiro":        (0.98, 1.1, 0.70, None, 0.0, 0.8),
    "arqueiro_superior": (0.47, 1.0, 0.45, (0.80, 0.90, 0.95), 0.55, 0.6),
    "sacerdote":         (0.12, 0.10, 1.45, (1.0, 0.85, 0.45), 0.45, 1.1),
    "arquimago":         (0.80, 1.2, 0.55, None, 0.0, 0.7),
    "berserker":         (0.00, 1.25, 0.60, (0.35, 0.33, 0.33), 0.6, 0.65),
    "the_guard":         (0.62, 1.2, 0.75, (0.60, 0.75, 1.0), 0.55, 0.9),
}

# ------------------------------------------------------------------ peças reutilizáveis
def pauldron(R, side, material, trim, size=0.24, spikes=0, horn=False):
    s = R.sl if side > 0 else R.sr
    c = Vector((s.x + 0.10 * side, s.y, s.z + 0.06))
    p = [dome("ombreira", size, c, material, (1.1, 1.0, 0.8), cut=-0.15)]
    p.append(dome("borda", size * 1.06, c - Vector((0, 0, 0.03)), trim, (1.12, 1.02, 0.42), cut=0.0))
    p += [ball("rebite", 0.022, c + Vector((0.12 * side, -0.14 + 0.14 * k, 0.10)), "gold", 1) for k in range(3)]
    for k in range(spikes):
        a = -0.6 + 0.6 * k
        p.append(cyl("espinho", 0.04, 0.16, c + Vector((0.05 * side, a * 0.25, size * 0.75)), "steel", 8, r2=0.0))
    if horn:
        p.append(tube("chifre", [c + Vector((0.12 * side, 0, 0.12)), c + Vector((0.26 * side, 0, 0.26)), c + Vector((0.30 * side, 0.02, 0.44))], 0.045, "horn", 8, r_end=0.008))
    return p

def cape(R, material, trim, length=0.75, width=0.55):
    y = R.back + 0.02; z0 = R.chest.z + 0.18
    pts = [(-width / 2, z0), (width / 2, z0), (width / 2 + 0.08, z0 - length), (0.0, z0 - length - 0.06), (-width / 2 - 0.08, z0 - length)]
    c = slab("capa", catmull(pts, 3), 0.03, material, bev=0); c.location = (0, y, 0)
    t = slab("capa_borda", catmull([(x * 1.02, z - 0.03) for x, z in pts], 3), 0.02, trim, bev=0); t.location = (0, y + 0.012, 0)
    return [c, t]

def belt(R, material, buckle):
    z = R.hips.z + 0.16
    rx = (R.bhi.x - R.blo.x) / 2 * 1.0; ry = (R.back - R.front) / 2 * 1.0
    cy = (R.back + R.front) / 2
    pts = [(rx * math.cos(i / 20 * TAU), cy + ry * math.sin(i / 20 * TAU), z) for i in range(20)]
    p = [tube("cinto", pts, 0.035, material, 6, closed=True)]
    p.append(box("fivela", (0.12, 0.03, 0.09), (0, R.front - 0.03, z), buckle, 0.008))
    return p

def tabard(R, material, trim, emblem=None, emblem_mat="gold"):
    z0 = R.hips.z + 0.22
    pts = [(-0.17, z0), (0.17, z0), (0.15, z0 - 0.36), (0.0, z0 - 0.44), (-0.15, z0 - 0.36)]
    t = slab("tabardo", pts, 0.025, material, bev=0); t.location = (0, R.front - 0.02, 0)
    b = slab("tabardo_borda", [(x * 1.12, z + 0.01 * (1 if z > z0 - 0.1 else -1)) for x, z in pts], 0.018, trim, bev=0); b.location = (0, R.front - 0.01, 0)
    p = [t, b]
    if emblem == "cross":
        p.append(place(slab("cruz_v", [(-0.025, z0 - 0.32), (0.025, z0 - 0.32), (0.025, z0 - 0.05), (-0.025, z0 - 0.05)], 0.035, emblem_mat, bev=0), (0, R.front - 0.03, 0)))
        p.append(place(slab("cruz_h", [(-0.09, z0 - 0.15), (0.09, z0 - 0.15), (0.09, z0 - 0.10), (-0.09, z0 - 0.10)], 0.035, emblem_mat, bev=0), (0, R.front - 0.03, 0)))
    elif emblem == "sun":
        p.append(place(ring("sol", 0.06, 0.012, (0, 0, z0 - 0.16), emblem_mat, "y"), (0, R.front - 0.035, z0 - 0.16)))
    return p

def chestplate(R, material, trim, gem_mat=None):
    z = R.chest.z
    pts = [(-0.26, z + 0.24), (0.26, z + 0.24), (0.24, z - 0.05), (0.12, z - 0.18), (0.0, z - 0.22), (-0.12, z - 0.18), (-0.24, z - 0.05)]
    p = [place(slab("peitoral", catmull(pts, 3), 0.05, material, bev=0.01), (0, R.front - 0.02, 0))]
    p.append(place(slab("peitoral_borda", catmull([(x * 1.08, zz) for x, zz in pts], 3), 0.03, trim, bev=0.005), (0, R.front, 0)))
    if gem_mat:
        p.append(place(slab("emblema", catmull([(-0.07, z + 0.10), (0.07, z + 0.10), (0.06, z - 0.02), (0, z - 0.08), (-0.06, z - 0.02)], 2), 0.06, trim, bev=0.004), (0, R.front - 0.04, 0)))
        p.append(gem("gema", 0.045, (0, R.front - 0.08, z + 0.03), gem_mat))
    return p

# ------------------------------------------------------------------ classes
# ------------------------------------------------------------------ peças grandes (silhueta)
def skirt(R, z_top, z_bot, r_top, r_bot, material, trim, slits=0):
    """Saia/robe: tronco de cone em volta do quadril (com barra dourada)."""
    cy = (R.front + R.back) / 2
    p = [cyl("robe", r_top, z_top - z_bot, (0, cy, (z_top + z_bot) / 2), material, 20, r2=r_bot, bev=0)]
    p[0].rotation_euler = (math.pi, 0, 0)
    p.append(cyl("barra", r_bot * 1.02, 0.05, (0, cy, z_bot + 0.03), trim, 20, bev=0))
    p[0].scale = (1.0, 0.85, 1.0); p[-1].scale = (1.0, 0.85, 1.0)
    return p

def hood_back(R, material, trim=None, size=1.12):
    """Capuz: cobre topo e costas da cabeça, aberto na frente."""
    d = dome("capuz", R.hr * size, (0, R.hc.y + 0.06, R.hc.z + 0.02), material, (1.0, 1.0, 1.05), cut=-0.2)
    d.rotation_euler = (math.radians(-100), 0, 0)
    p = [d]
    p.append(cyl("ponta", R.hr * 0.35, 0.35, (0, R.hc.y + R.hr * 0.95, R.hc.z + 0.25), material, 10, r2=0.02))
    p[-1].rotation_euler = (math.radians(-60), 0, 0)
    if trim:
        p.append(ring("borda", R.hr * 1.0, 0.03, (0, R.hlo.y + 0.12, R.hc.z + 0.02), trim, "y"))
    return p

def back_cloak(R, material, trim, length=1.1, width=0.95, fur=None, flare=0.25, rows=12, cols=10):
    """Capa nas costas: UMA malha lisa curvada (envolve o corpo, abre embaixo, ondula), com barra."""
    import bmesh
    z0 = R.chest.z + 0.30; y0 = R.back + 0.03; th = 0.025
    def pt(i, j, off):
        t = i / rows; u = -1 + 2 * j / cols
        hw = width / 2 * (0.72 + flare * t + 0.03)
        y = y0 + 0.24 * t ** 1.4 - 0.12 * u * u * (1 - 0.5 * t) + 0.025 * math.sin(u * 3 * math.pi) * t + off
        return (u * hw, y, z0 - length * t)
    bm = bmesh.new()
    F = [[bm.verts.new(pt(i, j, 0)) for j in range(cols + 1)] for i in range(rows + 1)]
    B = [[bm.verts.new(pt(i, j, th)) for j in range(cols + 1)] for i in range(rows + 1)]
    for i in range(rows):
        for j in range(cols):
            bm.faces.new((F[i][j], F[i][j + 1], F[i + 1][j + 1], F[i + 1][j]))
            bm.faces.new((B[i][j], B[i + 1][j], B[i + 1][j + 1], B[i][j + 1]))
    border = [F[0][j] for j in range(cols + 1)] + [F[i][cols] for i in range(1, rows + 1)] + [F[rows][j] for j in range(cols - 1, -1, -1)] + [F[i][0] for i in range(rows - 1, 0, -1)]
    borderB = [B[0][j] for j in range(cols + 1)] + [B[i][cols] for i in range(1, rows + 1)] + [B[rows][j] for j in range(cols - 1, -1, -1)] + [B[i][0] for i in range(rows - 1, 0, -1)]
    n = len(border)
    for k in range(n):
        bm.faces.new((border[k], borderB[k], borderB[(k + 1) % n], border[(k + 1) % n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts = [link(bm, "capa", material, smooth=True)]
    hem = [pt(rows, j, th / 2) for j in range(cols + 1)]
    parts.append(tube("barra", [(x, y, z + 0.02) for x, y, z in hem], 0.03, trim, 6))
    if fur:
        parts.append(tube("gola_pele", [(R.bhi.x * 1.1 * math.cos(i / 24 * TAU), R.bc.y + (R.back - R.front) / 2 * 1.15 * math.sin(i / 24 * TAU), z0 - 0.02) for i in range(24)], 0.13, fur, 8, closed=True))
    return parts

def quiver(R, body, rim, fletch, n=5, big=1.0, side=1):
    """Aljava nas costas, inclinada, com flechas."""
    c = Vector((0.12 * side, R.back + 0.14, R.chest.z + 0.05)); tilt = math.radians(-28 * side)
    up = Vector((math.sin(-tilt), 0, math.cos(tilt)))
    L = 0.62 * big
    q = [cyl("aljava", 0.12 * big, L, c, body, 14, r2=0.14 * big, rot=(0, tilt, 0))]
    q.append(cyl("aljava_aro", 0.15 * big, 0.05, c + up * (L / 2), rim, 14, rot=(0, tilt, 0)))
    q.append(cyl("aljava_aro2", 0.135 * big, 0.04, c - up * (L / 2 - 0.05), rim, 14, rot=(0, tilt, 0)))
    for k in range(n):
        a = k / n * TAU
        off = Vector((math.cos(a) * 0.06 * big, math.sin(a) * 0.06 * big, 0))
        base = c + off + up * (L / 2); tip = base + up * 0.22 * big
        q.append(tube("flecha", [base, tip], 0.012, "wood_lt", 5))
        f = slab("pena", [(-0.035, 0), (0.035, 0), (0.02, 0.11), (-0.02, 0.11)], 0.008, fletch, bev=0)
        f.location = tip - up * 0.12 * big; f.rotation_euler = (0, tilt, a); q.append(f)
    return q

def glow_eyes(R, material, spread=0.17, dz=0.0):
    z = R.hc.z - 0.02 + dz; y = R.hlo.y + 0.03
    return [ball("olho", 0.045, (sd * spread, y, z), material, 2, (1.3, 0.6, 0.8)) for sd in (-1, 1)]

def bracers(R, arm, material, trim):
    for side, bone in ((1, "lowerarm.l"), (-1, "lowerarm.r")):
        h = bone_w(arm, bone); w = bone_w(arm, "wrist." + ("l" if side > 0 else "r"))
        mid = (h + w) / 2
        b = cyl("bracadeira", 0.11, 0.16, mid, material, 12, r2=0.12, rot=(0, math.radians(90), 0))
        r_ = cyl("aro", 0.125, 0.03, mid + Vector((0.07 * side, 0, 0)), trim, 12, rot=(0, math.radians(90), 0))
        group("g_brac" + str(side), [b, r_], arm, bone)

# ------------------------------------------------------------------ classes
CLASSES = []
def cls(id_, model, weapon, offhand=None, scale=1.0, label="", color="ffffff"):
    def deco(fn):
        CLASSES.append(dict(id=id_, model=model, weapon=weapon, offhand=offhand, scale=scale, build=fn, label=label, color=color)); return fn
    return deco

@cls("guerreiro", "Barbarian", "sword", label="Guerreiro", color="e36a5a")
def b_guerreiro(arm, meshes, R):
    # referência: mercenário espadachim — rabo de cavalo, bandana vermelha, escudo redondo nas costas, ombreira de aço
    hide(meshes, "BearHat")
    zt = R.hhi.z - 0.12
    hz = R.hc.z + 0.06
    head = [dome("elmo", R.hr * 1.07, (0, R.hc.y + 0.02, hz), "steel", (1.0, 1.04, 1.12), cut=0.0)]
    head.append(tube("aro", [(R.hr * 1.08 * math.cos(i / 28 * TAU), R.hc.y + 0.02 + R.hr * 1.12 * math.sin(i / 28 * TAU), hz + 0.01) for i in range(28)], 0.045, "steel_dk", 6, closed=True))
    for k in range(4):   # nervuras douradas que sobem até o topo
        a_ = k / 4 * TAU + TAU / 8
        arc = [(R.hr * 1.09 * math.cos(t * math.pi / 2) * math.cos(a_), R.hc.y + 0.02 + R.hr * 1.13 * math.cos(t * math.pi / 2) * math.sin(a_), hz + R.hr * 1.2 * math.sin(t * math.pi / 2)) for t in [i / 8 for i in range(9)]]
        head.append(tube("nervura", arc, 0.028, "gold", 6))
    head.append(slab("nasal", [(-0.05, 0), (0.05, 0), (0.03, -0.26), (-0.03, -0.26)], 0.045, "steel_dk", bev=0.003)); head[-1].location = (0, R.hlo.y - 0.02, hz + 0.04)
    head.append(cyl("topo", 0.05, 0.08, (0, R.hc.y + 0.02, hz + R.hr * 1.22), "gold", 10))
    head.append(tube("pluma", [(0, R.hc.y + 0.02, hz + R.hr * 1.25), (0, R.hc.y + 0.18, hz + R.hr * 1.40), (0, R.hc.y + 0.42, hz + R.hr * 1.25), (0, R.hc.y + 0.55, hz + R.hr * 0.85), (0, R.hc.y + 0.58, hz + R.hr * 0.35)], 0.09, "plume_red", 10, r_end=0.025))
    for sd in (-1, 1):
        head.append(slab("bochecha", catmull([(-0.10, 0), (0.10, 0), (0.08, -0.22), (0, -0.28), (-0.06, -0.20)], 3), 0.04, "steel", bev=0.004))
        head[-1].location = (sd * R.hr * 0.98, R.hc.y + 0.02, hz + 0.02); head[-1].rotation_euler = (0, 0, math.radians(90 + 12 * sd))
    group("g_cabeca", head, arm, "head")
    group("g_ombro", pauldron(R, 1, "steel", "leather_dk", 0.26, spikes=1), arm, "upperarm.l")
    c = Vector((0, R.back + 0.12, R.chest.z - 0.02))
    sh = [cyl("escudo", 0.50, 0.06, c, "wood", 28, rot=(math.radians(90), 0, 0), bev=0.01)]
    sh.append(ring("aro", 0.50, 0.035, c, "steel", "y", 32))
    sh.append(dome("umbo", 0.15, c + Vector((0, 0.04, 0)), "steel", (1, 1, 0.6), cut=0.0)); sh[-1].rotation_euler = (math.radians(-90), 0, 0)
    for k in range(4):
        a_ = k / 4 * TAU + 0.785
        sh.append(box("tabua", (0.04, 0.02, 0.34), c + Vector((math.cos(a_) * 0.29, 0.035, math.sin(a_) * 0.29)), "cloth_red", 0.004, rot=(0, -a_ + math.pi / 2, 0)))
    for k in range(8):
        a_ = k / 8 * TAU
        sh.append(ball("rebite", 0.025, c + Vector((math.cos(a_) * 0.44, 0.04, math.sin(a_) * 0.44)), "brass", 1))
    sh.append(tube("alca", [(R.sl.x * 0.7, R.front - 0.03, R.chest.z + 0.28), (0, R.front - 0.06, R.chest.z), (R.sr.x * 0.8, R.front - 0.04, R.hips.z + 0.25)], 0.035, "leather_dk", 6))
    group("g_escudo", sh, arm, "chest")
    group("g_cinto", belt(R, "leather_dk", "brass"), arm, "hips")
    bracers(R, arm, "leather", "brass")

@cls("arqueiro", "Ranger", "bow", label="Arqueiro", color="a8d05a")
def b_arqueiro(arm, meshes, R):
    # referência: caçador da floresta (Robin Hood) — chapéu pontudo com pena longa, aljava nas costas, capa curta
    z = R.hc.z + R.hr * 0.30
    hat = [dome("chapeu", R.hr * 1.10, (0, R.hc.y + 0.03, z), "cloth_olive", (1.0, 1.2, 1.0), cut=0.0)]
    hat.append(tube("ponta", [(0, R.hc.y + R.hr * 0.2, z + R.hr * 0.85), (0, R.hc.y + R.hr * 0.9, z + R.hr * 0.75), (0, R.hc.y + R.hr * 1.6, z + R.hr * 0.45)], R.hr * 0.42, "cloth_olive", 12, r_end=0.02))
    aba = [(R.hr * 1.14 * math.cos(i / 28 * TAU), R.hc.y + 0.03 + R.hr * 1.3 * math.sin(i / 28 * TAU), z + 0.02 + 0.10 * max(0, -math.sin(i / 28 * TAU))) for i in range(28)]
    hat.append(tube("aba", aba, 0.05, "leather", 6, closed=True))
    pena = slab("pena", catmull([(0, 0), (0.06, 0.10), (0.05, 0.55), (0, 0.70), (-0.05, 0.55), (-0.06, 0.10)], 3), 0.012, "feather_red", bev=0)
    pena.location = (R.hr * 0.75, R.hc.y + 0.05, z + 0.02); pena.rotation_euler = (math.radians(-55), math.radians(-25), 0); hat.append(pena)
    group("g_chapeu", hat, arm, "head")
    group("g_aljava", quiver(R, "leather", "leather_dk", "feather_red", 5, 1.0, 1), arm, "chest")
    group("g_capa", back_cloak(R, "cloth_olive", "leather", 0.55, 0.80, flare=0.15), arm, "chest")
    group("g_cinto", belt(R, "leather", "brass"), arm, "hips")
    bracers(R, arm, "leather", "leather_dk")

@cls("mago", "Mage", "staff_arcane", label="Mago", color="5fd0e0")
def b_mago(arm, meshes, R):
    # referência: mago elemental — tiara com olho arcano, gola alta, 3 orbes (fogo, gelo, raio) girando, grimório aberto flutuando
    hide(meshes, "Hat")
    zc = R.hc.z + R.hr * 0.30
    tia = [tube("tiara", [(R.hr * 0.93 * math.cos(i / 26 * TAU), R.hc.y + R.hr * 0.95 * math.sin(i / 26 * TAU), zc) for i in range(26)], 0.03, "gold", 6, closed=True)]
    tia.append(slab("tiara_frente", catmull([(-0.10, 0), (0.10, 0), (0.06, 0.10), (0, 0.17), (-0.06, 0.10)], 3), 0.03, "gold", bev=0))
    tia[-1].location = (0, R.hlo.y + 0.06, zc - 0.03)
    tia.append(gem("olho_arcano", 0.05, (0, R.hlo.y + 0.03, zc + 0.04), "ice_glow", (1, 0.6, 1.2)))
    group("g_tiara", tia, arm, "head")
    gola = slab("gola_alta", catmull([(-0.42, 0), (0.42, 0), (0.50, 0.30), (0.30, 0.52), (0, 0.42), (-0.30, 0.52), (-0.50, 0.30)], 3), 0.03, "cloth_teal", bev=0)
    gola.location = (0, R.back + 0.02, R.chest.z + 0.22); gola.rotation_euler = (math.radians(-18), 0, 0)
    borda = tube("gola_borda", [(-0.42, R.back + 0.02, R.chest.z + 0.22), (-0.50, R.back + 0.12, R.chest.z + 0.50), (-0.30, R.back + 0.18, R.chest.z + 0.73), (0, R.back + 0.15, R.chest.z + 0.63), (0.30, R.back + 0.18, R.chest.z + 0.73), (0.50, R.back + 0.12, R.chest.z + 0.50), (0.42, R.back + 0.02, R.chest.z + 0.22)], 0.022, "gold", 6)
    orbs = []
    for k, (m, h, a) in enumerate((("fire_glow", 0.75, math.pi * 0.95), ("ice_glow", 0.55, math.pi * 0.5), ("volt_glow", 0.95, math.pi * 0.20))):
        c = Vector((math.cos(a) * 0.80, R.bc.y + math.sin(a) * 0.55, R.chest.z + h))
        orbs.append(ball("orbe", 0.12, c, m, 2))
        orbs.append(ring("orbe_anel", 0.18, 0.015, c, "gold", "z" if k % 2 else "y"))
    page = catmull([(0, 0), (0.24, 0.03), (0.25, 0.32), (0.01, 0.30)], 3)
    livro = []
    for sd in (-1, 1):
        cv = slab("capa_livro", [(x * sd, z) for x, z in catmull([(0, -0.02), (0.27, 0.01), (0.28, 0.35), (0, 0.33)], 3)], 0.03, "cloth_teal", bev=0.004)
        cv.rotation_euler = (0, 0, math.radians(-22 * sd)); livro.append(cv)
        pg = slab("pagina", [(x * sd, z) for x, z in page], 0.05, "page_glow", bev=0)
        pg.location = (0, -0.035, 0.01); pg.rotation_euler = (0, 0, math.radians(-16 * sd)); livro.append(pg)
        cn = box("canto", (0.05, 0.035, 0.05), (0.25 * sd, 0.01, 0.31), "gold", 0.004, rot=(0, 0, math.radians(-22 * sd))); livro.append(cn)
    livro.append(cyl("lombada", 0.03, 0.34, (0, 0.03, 0.16), "gold", 8))
    livro.append(ring("runa", 0.08, 0.012, (0.12, -0.07, 0.17), "ice_glow", "y")); livro.append(ring("runa2", 0.07, 0.012, (-0.12, -0.07, 0.17), "ice_glow", "y"))
    livro.append(ring("circulo", 0.30, 0.012, (0, 0, -0.12), "ice_glow", "z", 32))
    for o in livro:
        o.location = Vector(o.location) * 1.4 + Vector((R.sl.x + 0.55, R.bc.y - 0.20, R.chest.z + 0.05)); o.scale = Vector(o.scale) * 1.4
    group("g_magia", [gola, borda] + orbs + livro, arm, "chest")
    p = skirt(R, R.hips.z + 0.20, R.hips.z - 0.30, 0.42, 0.56, "cloth_teal", "gold")
    for k in range(3):
        sc_ = cyl("pergaminho", 0.035, 0.18, (-0.22 + k * 0.10, R.front - 0.08, R.hips.z + 0.05), "ivory", 8, rot=(math.radians(90), 0, math.radians(90)))
        p.append(sc_)
    group("g_robe", p, arm, "hips")
    bracers(R, arm, "cloth_teal", "gold")

@cls("tank", "Knight", "hammer", offhand="tower_shield", label="Tank", color="8fb4ff")
def b_tank(arm, meshes, R):
    for sd, b in ((1, "upperarm.l"), (-1, "upperarm.r")):
        group("g_ombro" + str(sd), pauldron(R, sd, "steel", "steel_dk", 0.28), arm, b)
    group("g_peito", chestplate(R, "steel", "steel_dk", "sapphire"), arm, "chest")
    group("g_cinto", belt(R, "leather_dk", "steel"), arm, "hips")

@cls("assassino", "Rogue_Hooded", "dagger", offhand="dagger", label="Assassino", color="9b6bff")
def b_assassino(arm, meshes, R):
    group("g_olhos", glow_eyes(R, "violet_glow", 0.16, 0.06), arm, "head")
    scarf = [tube("lenco", [(0.10, R.back - 0.05, R.chest.z + 0.32), (0.25, R.back + 0.20, R.chest.z + 0.25), (0.45, R.back + 0.45, R.chest.z + 0.10), (0.62, R.back + 0.62, R.chest.z + 0.18), (0.80, R.back + 0.75, R.chest.z + 0.05)], 0.07, "violet", 8, r_end=0.02),
             tube("lenco2", [(-0.05, R.back - 0.05, R.chest.z + 0.30), (-0.10, R.back + 0.25, R.chest.z + 0.12), (-0.05, R.back + 0.50, R.chest.z - 0.05), (0.05, R.back + 0.62, R.chest.z - 0.20)], 0.06, "cloth_black", 8, r_end=0.02)]
    group("g_lenco", scarf, arm, "chest")
    sash = tube("faixa", [(R.bhi.x * 0.95 * math.cos(i / 20 * TAU), R.bc.y + (R.back - R.front) / 2 * 0.98 * math.sin(i / 20 * TAU), R.hips.z + 0.2) for i in range(20)], 0.05, "violet", 6, closed=True)
    knives = []
    for k in range(4):
        kn = slab("faca", [(-0.022, 0), (0.022, 0), (0.0, 0.18)], 0.012, "steel", bev=0)
        kn.location = (-0.21 + k * 0.14, R.front - 0.05, R.hips.z + 0.24); kn.rotation_euler = (0, math.radians(180 + (k - 1.5) * 12), 0)
        knives.append(kn)
    group("g_faixa", [sash] + knives, arm, "hips")
    for sd, b in ((1, "upperarm.l"), (-1, "upperarm.r")):
        bl = slab("lamina_ombro", catmull([(0, 0), (0.06, 0.02), (0.24, 0.16), (0.04, 0.07)], 2), 0.02, "black", bev=0)
        bl.location = (R.sl.x + 0.05 if sd > 0 else R.sr.x - 0.05, R.sl.y, R.sl.z + 0.12)
        bl.rotation_euler = (0, 0, 0) if sd > 0 else (0, 0, math.pi)
        group("g_lamina" + str(sd), [bl], arm, b)

@cls("cavaleiro", "Knight", "greatsword", label="Cavaleiro", color="e0b050")
def b_cavaleiro(arm, meshes, R):
    hide(meshes, "HelmetVisor")
    crown = [tube("coroa", [(R.hr * 1.12 * math.cos(i / 24 * TAU), R.hc.y + R.hr * 1.12 * math.sin(i / 24 * TAU), R.hc.z + 0.28) for i in range(24)], 0.04, "gold", 6, closed=True)]
    for k in range(8):
        a = k / 8 * TAU
        crown.append(cyl("ponta", 0.04, 0.12, (R.hr * 1.12 * math.cos(a), R.hc.y + R.hr * 1.12 * math.sin(a), R.hc.z + 0.36), "gold", 6, r2=0.0))
    crown.append(tube("pluma", [(0, R.hc.y - 0.05, R.hhi.z + 0.02), (0, R.hc.y + 0.10, R.hhi.z + 0.32), (0, R.hc.y + 0.42, R.hhi.z + 0.40), (0, R.hc.y + 0.75, R.hhi.z + 0.12), (0, R.hc.y + 0.90, R.hhi.z - 0.30)], 0.13, "plume_red", 10, r_end=0.03))
    group("g_coroa", crown, arm, "head")
    for sd, b in ((1, "upperarm.l"), (-1, "upperarm.r")):
        p = pauldron(R, sd, "gold", "gold_dk", 0.30)
        p.append(dome("ombreira2", 0.25, Vector(((R.sl.x if sd > 0 else R.sr.x) + 0.15 * sd, R.sl.y, R.sl.z - 0.05)), "gold_dk", (1.0, 0.95, 0.6), cut=-0.1))
        group("g_ombro" + str(sd), p, arm, b)
    group("g_tabardo", tabard(R, "cloth_red", "gold", "cross", "gold"), arm, "hips")
    group("g_capa", back_cloak(R, "cloth_red", "gold", 1.15, 1.0), arm, "chest")

@cls("pistoleiro", "Rogue", "pistol", offhand="pistol", label="Pistoleiro", color="ffb347")
def b_pistoleiro(arm, meshes, R):
    top = R.hhi.z - 0.08
    hat = [cyl("copa", R.hr * 0.72, 0.32, (0, R.hc.y, top + 0.13), "cloth_black", 18, r2=R.hr * 0.62)]
    hat.append(cyl("aba", R.hr * 1.30, 0.04, (0, R.hc.y, top - 0.02), "cloth_black", 24))
    hat.append(tube("aba_borda", [(R.hr * 1.30 * math.cos(i / 28 * TAU), R.hc.y + R.hr * 1.30 * math.sin(i / 28 * TAU), top + 0.01 + 0.10 * abs(math.cos(i / 28 * TAU)) ** 2) for i in range(28)], 0.035, "cloth_black", 6, closed=True))
    hat.append(tube("fita", [(R.hr * 0.73 * math.cos(i / 20 * TAU), R.hc.y + R.hr * 0.73 * math.sin(i / 20 * TAU), top + 0.03) for i in range(20)], 0.035, "gold", 6, closed=True))
    hat.append(dome("caveira", 0.08, (0, R.hc.y - R.hr * 0.72, top + 0.16), "white", (1, 0.5, 1.1), cut=-0.6))
    hat.append(slab("pena", catmull([(0, 0), (0.07, 0.12), (0.06, 0.45), (0, 0.56), (-0.06, 0.45), (-0.07, 0.12)], 3), 0.012, "feather_red", bev=0))
    hat[-1].location = (R.hr * 0.55, R.hc.y + 0.12, top + 0.10); hat[-1].rotation_euler = (0, math.radians(-40), 0)
    group("g_chapeu", hat, arm, "head")
    a0 = Vector((R.sl.x, R.front - 0.03, R.chest.z + 0.28)); a1 = Vector((R.sr.x * 0.7, R.front - 0.04, R.hips.z + 0.18))
    band = [tube("bandoleira", [a0, (a0 + a1) / 2 + Vector((0, -0.04, 0)), a1], 0.045, "leather_dk", 6)]
    for k in range(6):
        pt = a0.lerp(a1, 0.12 + k * 0.15) + Vector((0, -0.05, 0)); band.append(cyl("bala", 0.024, 0.09, pt, "brass", 8))
    group("g_bandoleira", band, arm, "chest")
    for sd, b in ((1, "upperarm.l"), (-1, "upperarm.r")):
        s_ = R.sl if sd > 0 else R.sr
        ep = [cyl("dragona", 0.15, 0.05, (s_.x + 0.08 * sd, s_.y, s_.z + 0.12), "gold", 14)]
        for k in range(7):
            a = math.pi * (k / 6)
            ep.append(cyl("franja", 0.012, 0.10, (s_.x + 0.08 * sd + 0.14 * math.cos(a) * sd, s_.y - 0.14 * math.sin(a) + 0.07, s_.z + 0.06), "gold_dk", 5))
        group("g_dragona" + str(sd), ep, arm, b)
    tails = []
    for sd in (-1, 1):
        t = slab("aba_casaco", catmull([(0, 0), (0.22 * sd, 0), (0.26 * sd, -0.55), (0.10 * sd, -0.62), (0.0, -0.45)], 2), 0.03, "cloth_red", bev=0)
        t.location = (0, R.back + 0.02, R.hips.z + 0.22); t.rotation_euler = (math.radians(-10), 0, 0); tails.append(t)
    group("g_casaco", tails, arm, "hips")

@cls("arqueiro_superior", "Knight", "longbow", label="Arqueiro Superior", color="40e0b0")
def b_superior(arm, meshes, R):
    # referência: sentinela de elite / atirador blindado — capuz fundo, máscara de prata, coroa de folhas, olhos verdes, aljava grande com flechas brilhando
    hide(meshes, "Helmet", "HelmetVisor", "Cape")
    group("g_capuz", hood_back(R, "cloth_teal", "silver", 1.22), arm, "head")
    crown = []
    zc = R.hc.z + R.hr * 0.62
    crown.append(slab("tiara_frente", catmull([(-0.07, 0), (0.07, 0), (0.04, 0.08), (0, 0.15), (-0.04, 0.08)], 3), 0.025, "silver", bev=0))
    crown[-1].location = (0, R.hlo.y + 0.05, zc - 0.07)
    crown.append(gem("gema", 0.045, (0, R.hlo.y + 0.03, zc + 0.02), "emerald_glow"))
    group("g_coroa", crown, arm, "head")
    neck = R.chest.z + 0.30
    group("g_cachecol", [tube("cachecol", [(R.bhi.x * 0.80 * math.cos(i / 20 * TAU), R.bc.y + (R.back - R.front) / 2 * 0.95 * math.sin(i / 20 * TAU), neck) for i in range(20)], 0.08, "cloth_teal", 8, closed=True),
                         tube("ponta", [(-0.12, R.front - 0.04, neck - 0.02), (-0.16, R.front - 0.08, neck - 0.25), (-0.14, R.front - 0.06, neck - 0.45)], 0.06, "cloth_teal", 8, r_end=0.025)], arm, "chest")
    for sd, b in ((1, "upperarm.l"), (-1, "upperarm.r")):
        p = pauldron(R, sd, "silver", "cloth_teal", 0.26)
        s_ = R.sl if sd > 0 else R.sr
        for k in range(3):
            lf = slab("folha_ombro", catmull([(0, 0), (0.05, 0.06), (0.04, 0.22), (0, 0.28), (-0.04, 0.22), (-0.05, 0.06)], 3), 0.015, "silver", bev=0)
            lf.location = (s_.x + 0.10 * sd, s_.y + (k - 1) * 0.12, s_.z + 0.10); lf.rotation_euler = (0, math.radians(-70 * sd), 0); p.append(lf)
        group("g_ombro" + str(sd), p, arm, b)
    group("g_aljava", quiver(R, "cloth_teal", "silver", "emerald_glow", 7, 1.35, -1), arm, "chest")
    group("g_capa", back_cloak(R, "cloth_teal", "silver", 1.15, 0.95, flare=0.3), arm, "chest")
    bracers(R, arm, "silver", "emerald_glow")

@cls("sacerdote", "Mage", "staff_holy", label="Sacerdote", color="ffe08a")
def b_sacerdote(arm, meshes, R):
    hide(meshes, "Hat")
    mitre = [slab("mitra", catmull([(-0.30, 0), (0.30, 0), (0.24, 0.42), (0.0, 0.66), (-0.24, 0.42)], 3), 0.42, "white", edge_thin=0.5, bev=0.01)]
    mitre[0].location = (0, R.hc.y, R.hhi.z - 0.24)
    mitre.append(place(slab("faixa", [(-0.045, 0), (0.045, 0), (0.045, 0.52), (-0.045, 0.52)], 0.44, "gold", bev=0), (0, R.hc.y, R.hhi.z - 0.22)))
    mitre.append(place(slab("faixa_h", [(-0.30, 0), (0.30, 0), (0.29, 0.07), (-0.29, 0.07)], 0.44, "gold", bev=0), (0, R.hc.y, R.hhi.z - 0.24)))
    mitre.append(gem("gema", 0.055, (0, R.hc.y - 0.23, R.hhi.z + 0.05), "holy_glow"))
    mitre.append(ring("aureola", 0.32, 0.045, (0, R.hc.y + 0.30, R.hc.z + 0.35), "holy_glow", "y"))
    group("g_mitra", mitre, arm, "head")
    stole = [tube("gola", [(R.bhi.x * 0.95 * math.cos(i / 22 * TAU), R.bc.y + (R.back - R.front) / 2 * 1.0 * math.sin(i / 22 * TAU), R.chest.z + 0.26) for i in range(22)], 0.07, "gold", 8, closed=True)]
    for sd in (-1, 1):
        st = slab("estola", [(-0.07, 0), (0.07, 0), (0.08, -0.85), (-0.06, -0.85)], 0.025, "gold", bev=0)
        st.location = (0.13 * sd, R.front - 0.04, R.chest.z + 0.26); stole.append(st)
        crs = slab("cruz", catmull([(-0.02, -0.14), (0.02, -0.14), (0.02, 0.0), (-0.02, 0.0)], 1), 0.035, "white", bev=0)
        crs.location = (0.13 * sd, R.front - 0.06, R.chest.z - 0.35); stole.append(crs)
    group("g_estola", stole, arm, "chest")
    group("g_robe", skirt(R, R.hips.z + 0.20, R.hips.z - 0.32, 0.42, 0.56, "white", "gold"), arm, "hips")

@cls("arquimago", "Mage", "staff_arcane", label="Arqui-mago", color="c070ff")
def b_arquimago(arm, meshes, R):
    hide(meshes, "Hat")
    top = R.hhi.z - 0.12
    hat = [cyl("aba", R.hr * 1.65, 0.05, (0, R.hc.y, top), "cloth_purple", 28)]
    cone = [(0, R.hc.y, top), (0, R.hc.y + 0.02, top + 0.45), (0, R.hc.y + 0.12, top + 0.85), (0, R.hc.y + 0.38, top + 1.10), (0, R.hc.y + 0.62, top + 1.05)]
    hat.append(tube("cone", cone, R.hr * 1.0, "cloth_purple", 14, r_end=0.03))
    hat.append(cyl("faixa", R.hr * 1.03, 0.10, (0, R.hc.y, top + 0.08), "violet_glow", 18))
    for k, (dx, dz) in enumerate(((0.15, 0.40), (-0.12, 0.62), (0.05, 0.85))):
        hat.append(gem("estrela", 0.05, (dx, R.hc.y - R.hr * 0.55 + dz * 0.05, top + dz), "arcane_glow", (1, 0.5, 1)))
    group("g_chapeu", hat, arm, "head")
    collar = []
    for k in range(7):
        a = math.pi * (0.10 + 0.8 * k / 6)
        sp = slab("gola", [(-0.07, 0), (0.07, 0), (0.0, 0.40)], 0.02, "cloth_purple", bev=0)
        sp.location = (math.cos(a) * 0.32, R.bc.y + math.sin(a) * 0.22, R.chest.z + 0.24); sp.rotation_euler = (math.radians(-25), 0, -math.cos(a) * 0.8)
        collar.append(sp)
    rune = ring("anel_runico", 0.85, 0.025, (0, R.bc.y, R.hips.z + 0.05), "arcane_glow", "z")
    orbs = []
    for k in range(3):
        a = k / 3 * TAU + 0.3
        c = (math.cos(a) * 0.85, R.bc.y + math.sin(a) * 0.85, R.hips.z + 0.05)
        orbs.append(gem("orbe", 0.10, c, "arcane_glow", (1, 1, 1)))
    group("g_gola", collar + [rune] + orbs, arm, "chest")
    group("g_robe", skirt(R, R.hips.z + 0.20, R.hips.z - 0.30, 0.42, 0.55, "cloth_purple", "violet_glow"), arm, "hips")

@cls("berserker", "Barbarian", "axe_blood", offhand="axe_blood", scale=1.18, label="Berserker", color="ff4a3a")
def b_berserker(arm, meshes, R):
    hide(meshes, "BearHat")
    hz = R.hc.z + 0.10
    helm = [dome("elmo", R.hr * 1.08, (0, R.hc.y, hz), "iron", (1, 1, 0.95), cut=0.0)]
    helm.append(tube("aro", [(R.hr * 1.09 * math.cos(i / 28 * TAU), R.hc.y + R.hr * 1.09 * math.sin(i / 28 * TAU), hz) for i in range(28)], 0.05, "steel_dk", 6, closed=True))
    for k in range(10):
        a = k / 10 * TAU
        helm.append(ball("rebite", 0.03, (R.hr * 1.12 * math.cos(a), R.hc.y + R.hr * 1.12 * math.sin(a), hz), "gold_dk", 1))
    helm.append(tube("crista", [(0, R.hc.y - R.hr * 1.05, hz + 0.05), (0, R.hc.y - R.hr * 0.6, hz + R.hr * 0.85), (0, R.hc.y, hz + R.hr * 1.05), (0, R.hc.y + R.hr * 0.7, hz + R.hr * 0.8)], 0.04, "steel_dk", 6))
    helm.append(slab("nasal", [(-0.06, 0), (0.06, 0), (0.035, -0.34), (-0.035, -0.34)], 0.05, "steel_dk", bev=0)); helm[-1].location = (0, R.hlo.y - 0.03, hz + 0.06)
    for sd in (-1, 1):
        horn = [(sd * R.hr * 0.95, R.hc.y, hz + 0.18), (sd * (R.hr + 0.22), R.hc.y - 0.02, hz + 0.28), (sd * (R.hr + 0.42), R.hc.y - 0.06, hz + 0.55),
                (sd * (R.hr + 0.46), R.hc.y - 0.12, hz + 0.90), (sd * (R.hr + 0.34), R.hc.y - 0.18, hz + 1.12)]
        helm.append(tube("chifre", horn, 0.13, "horn", 12, r_end=0.012))
        for t, rr in ((1, 0.12), (2, 0.10)):
            helm.append(ring("anel_chifre", rr, 0.02, Vector(horn[t]), "iron", "z"))
    bz = R.hc.z - R.hr * 0.42; by = R.hlo.y + 0.16
    beard = [ball("barba", 0.38, (0, by, bz - 0.12), "beard", 2, (1.05, 0.55, 0.85)), ball("barba_queixo", 0.26, (0, by - 0.04, bz - 0.44), "beard", 2, (0.9, 0.6, 1.0))]
    for sd in (-1, 1):
        beard.append(tube("tranca", [(sd * 0.15, by - 0.10, bz - 0.40), (sd * 0.17, by - 0.14, bz - 0.66), (sd * 0.14, by - 0.14, bz - 0.92)], 0.075, "beard_dk", 8, r_end=0.04))
        beard.append(cyl("anel_tranca", 0.09, 0.07, (sd * 0.16, by - 0.14, bz - 0.74), "gold", 12))
        beard.append(tube("bigode", [(0, by - 0.18, bz + 0.10), (sd * 0.17, by - 0.20, bz + 0.08), (sd * 0.32, by - 0.16, bz - 0.08)], 0.06, "beard_dk", 8, r_end=0.02))
    group("g_elmo", helm + beard + glow_eyes(R, "blood_glow", 0.17, 0.05), arm, "head")
    for sd, b in ((1, "upperarm.l"), (-1, "upperarm.r")):
        group("g_ombro" + str(sd), pauldron(R, sd, "iron", "fur", 0.30, spikes=3, horn=True), arm, b)
    group("g_capa", back_cloak(R, "fur_dk", "fur", 0.95, 1.0, fur="fur"), arm, "chest")
    a0 = Vector((R.sl.x + 0.05, R.front - 0.04, R.chest.z + 0.25)); a1 = Vector((R.sr.x * 0.8, R.front - 0.05, R.hips.z + 0.15))
    chain = []
    for k in range(9):
        pt = a0.lerp(a1, k / 8) + Vector((0, -0.03, 0))
        chain.append(ring("elo", 0.04, 0.012, pt, "steel_dk", "y" if k % 2 else "z"))
    group("g_corrente", chain, arm, "chest")
    p = belt(R, "leather_dk", "iron")
    p.append(dome("caveira", 0.10, (0, R.front - 0.07, R.hips.z + 0.17), "bone", (1, 0.7, 1.1), cut=-0.5))
    group("g_cinto", p, arm, "hips")
    bracers(R, arm, "iron", "blood")

@cls("the_guard", "Knight", "guard_sword", offhand="tower_shield", scale=1.18, label="The Guard", color="7fb0ff")
def b_guard(arm, meshes, R):
    hide(meshes, "Helmet", "HelmetVisor")
    z0 = R.hlo.z + 0.12; z1 = R.hhi.z - 0.10
    helm = [cyl("elmo", R.hr * 1.08, z1 - z0, (0, R.hc.y, (z0 + z1) / 2), "silver", 20, r2=R.hr * 1.02, bev=0.01)]
    helm.append(dome("topo", R.hr * 1.02, (0, R.hc.y, z1), "silver", (1, 1, 0.55), cut=0.0))
    helm.append(box("fenda", (R.hr * 1.2, 0.08, 0.07), (0, R.hlo.y - 0.05, R.hc.z + 0.04), "black", 0.01))
    helm.append(box("fenda_v", (0.07, 0.08, 0.32), (0, R.hlo.y - 0.05, R.hc.z - 0.12), "black", 0.01))
    helm.append(slab("cruz", [(-0.04, R.hc.z - 0.35), (0.04, R.hc.z - 0.35), (0.04, z1 + 0.25), (-0.04, z1 + 0.25)], 0.06, "gold", bev=0.003)); helm[-1].location = (0, R.hlo.y - 0.07, 0)
    helm.append(tube("aro", [(R.hr * 1.10 * math.cos(i / 28 * TAU), R.hc.y + R.hr * 1.10 * math.sin(i / 28 * TAU), z0 + 0.03) for i in range(28)], 0.04, "gold", 6, closed=True))
    for sd in (-1, 1):
        wing = slab("asa_elmo", catmull([(0, 0), (0.10, 0.06), (0.22, 0.22), (0.26, 0.40), (0.16, 0.30), (0.12, 0.38), (0.06, 0.22)], 3), 0.03, "gold", bev=0.003)
        wing.location = (sd * R.hr * 1.0, R.hc.y, R.hc.z + 0.08); wing.scale = (sd, 1, 1); wing.rotation_euler = (0, 0, math.radians(90 * sd - 90 * sd))
        helm.append(wing)
    helm.append(tube("pluma", [(0, R.hc.y - 0.10, z1 + 0.35), (0, R.hc.y + 0.15, z1 + 0.55), (0, R.hc.y + 0.50, z1 + 0.45), (0, R.hc.y + 0.80, z1 + 0.10), (0, R.hc.y + 0.95, z1 - 0.35)], 0.13, "plume_blue", 10, r_end=0.03))
    group("g_elmo", helm, arm, "head")
    for sd, b in ((1, "upperarm.l"), (-1, "upperarm.r")):
        p = pauldron(R, sd, "guard_blue", "gold", 0.34)
        p.append(dome("ombreira2", 0.28, Vector(((R.sl.x if sd > 0 else R.sr.x) + 0.17 * sd, R.sl.y, R.sl.z - 0.07)), "silver", (1.0, 0.95, 0.6), cut=-0.1))
        p.append(gem("gema", 0.04, Vector(((R.sl.x if sd > 0 else R.sr.x) + 0.12 * sd, R.sl.y - 0.20, R.sl.z + 0.12)), "guard_glow"))
        group("g_ombro" + str(sd), p, arm, b)
    group("g_peito", chestplate(R, "silver", "gold", "guard_glow"), arm, "chest")
    group("g_tabardo", tabard(R, "guard_blue", "gold", "cross", "gold"), arm, "hips")
    group("g_capa", back_cloak(R, "guard_blue", "gold", 1.2, 1.05), arm, "chest")
    bracers(R, arm, "silver", "gold")

# ------------------------------------------------------------------ arma na mão (só para a foto; no jogo a arma vem do equipamento)
WEAPON_FN = {"sword": make_sword, "greatsword": make_greatsword, "dagger": make_dagger, "pistol": make_pistol, "bow": make_bow,
             "longbow": make_longbow, "staff_arcane": make_staff_arcane, "staff_holy": make_staff_holy, "hammer": make_hammer,
             "axe": make_axe, "axe_blood": lambda: make_axe("axe_blood", True), "guard_sword": make_guard_sword,
             "tower_shield": make_tower_shield, "spear": make_spear}

def hold(arm, kind, bone):
    w = WEAPON_FN[kind]()
    w.parent = arm; w.parent_type = "BONE"; w.parent_bone = bone
    w.matrix_parent_inverse = Matrix.Identity(4)
    w.location = (0, -arm.data.bones[bone].length, 0)      # cabeça do osso handslot
    w.rotation_euler = (math.radians(-90), 0, 0)              # +Z da arma -> eixo do osso
    w.scale = (1 / max(1e-4, arm.scale.x),) * 3
    return w

def set_pose(arm, action_name="Idle_A", frame=12):
    act = next((a for a in bpy.data.actions if a.name.endswith("|" + action_name)), None)
    if act is None: act = next((a for a in bpy.data.actions if a.name.endswith("|Idle_A")), None)
    if act is None: return
    if arm.animation_data is None: arm.animation_data_create()
    arm.animation_data.action = act
    try:
        if hasattr(arm.animation_data, "action_slot") and len(act.slots): arm.animation_data.action_slot = act.slots[0]
    except Exception: pass
    bpy.context.scene.frame_set(frame)

def export_hero(cid, arm, objs):
    bpy.ops.object.select_all(action="DESELECT")
    old = arm.name; arm.name = "Rig_Medium"
    for o in objs: o.select_set(True)
    arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_HEROES, "hero_" + cid + ".fbx"), use_selection=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", path_mode="COPY", embed_textures=False)
    arm.name = old

# ------------------------------------------------------------------ main
clear_scene()
for fbx in ("Rig_Medium_General.fbx", "Rig_Medium_CombatMelee.fbx", "Rig_Medium_CombatRanged.fbx"):
    try: bpy.ops.import_scene.fbx(filepath=os.path.join(KK, "Animations", fbx))
    except Exception: traceback.print_exc()
for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o, do_unlink=True)   # só queremos as ações
print("[Drakantus] ações:", [a.name.split("|")[-1] for a in bpy.data.actions][:80])
POSE = {"greatsword": "Melee_2H_Idle", "hammer": "Idle_A", "axe_blood": "Melee_Blocking", "guard_sword": "Idle_A",
        "bow": "Ranged_Bow_Idle", "longbow": "Ranged_Bow_Idle", "sword": "Melee_Blocking", "dagger": "Melee_Blocking", "pistol": "Melee_Blocking"}

lineup = []
for i, c in enumerate(CLASSES):
    try:
        arm, meshes = import_char(c["model"])
        R = Ref(arm, meshes)
        try: recolor(meshes, c["id"], *COLORS[c["id"]])
        except Exception: traceback.print_exc()
        before = set(bpy.data.objects)
        c["build"](arm, meshes, R)
        gear = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        export_hero(c["id"], arm, meshes + gear)
        set_pose(arm, POSE.get(c["weapon"], "Idle_A"), 10)
        bpy.context.view_layer.update()
        HR = arm.matrix_world @ arm.pose.bones["handslot.r"].head; HL = arm.matrix_world @ arm.pose.bones["handslot.l"].head
        for kind, bone, side in ((c["weapon"], "handslot.r", -1), (c["offhand"], "handslot.l", 1)):
            if not kind: continue
            w = hold(arm, kind, bone)
            hp = HR if side < 0 else HL
            bpy.context.view_layer.update()
            if kind == "greatsword":
                # duas mãos: cabo entre as mãos, lâmina para cima inclinada à frente
                mid = (HR + HL) / 2
                axis = Vector((-0.55, -0.35, 1.0)).normalized()     # diagonal para o lado direito: não cobre o rosto
                yv = Vector((0, 1, 0)); yv = (yv - axis * axis.dot(yv)).normalized(); xv = yv.cross(axis)
                rw = Matrix((xv, yv, axis)).transposed()          # lâmina de frente para a câmera
                w.matrix_world = Matrix.Translation(mid + Vector((-0.05, -0.10, -0.05))) @ rw.to_4x4() @ Matrix.Diagonal((1.3, 1.3, 1.15, 1))
                continue
            if kind == "tower_shield":
                # escudo preso no antebraço esquerdo, virado um pouco para fora, sem tocar o chão
                fa = arm.matrix_world @ arm.pose.bones["lowerarm.l"].head
                cen = (fa + HL) / 2 + Vector((0.20, -0.16, 0.10))
                rw = Matrix.Rotation(math.radians(28), 3, "Z") @ Matrix.Rotation(math.radians(-6), 3, "X")
                w.matrix_world = Matrix.Translation(cen) @ rw.to_4x4() @ Matrix.Scale(0.92, 4)
                continue
            rw = (Matrix.Rotation(math.radians(-15), 3, "X") @ Matrix.Rotation(math.radians(8 * side), 3, "Y"))
            if kind in ("bow", "longbow"): rw = Matrix.Rotation(math.radians(90), 3, "Z") @ rw
            if kind == "pistol": rw = Matrix.Rotation(math.radians(-70), 3, "X")
            off = Vector((0, -0.08, 0.0)); chunky = Matrix.Identity(4)
            if kind in ("staff_arcane", "staff_holy", "hammer"):
                # proporção chibi: haste mais grossa e cabeça maior, afastada do corpo para não sumir atrás do cabelo
                rw = Matrix.Rotation(math.radians(-8), 3, "X") @ Matrix.Rotation(math.radians(-10), 3, "Y")
                off = Vector((-0.16, -0.12, 0.0)); chunky = Matrix.Diagonal((1.7, 1.7, 1.05, 1))
            w.matrix_world = Matrix.Translation(hp + off) @ rw.to_4x4() @ chunky
        arm.scale = arm.scale * c["scale"]
        lineup.append(arm)
        print("[Drakantus] classe", c["id"], "ok")
    except Exception as e:
        traceback.print_exc(); print("[Drakantus] ERRO classe", c["id"], e)

# foto: 2 fileiras de 6, pedestais com a cor da classe, câmera inclinada como no jogo
sc = bpy.context.scene
PAL.update({"pedestal": (0.20, 0.19, 0.22), "pedestal_dk": (0.12, 0.11, 0.13)})
for i, arm in enumerate(lineup):
    r, k = divmod(i, 6)
    anchor = bpy.data.objects.new("ancora_%d" % i, None); sc.collection.objects.link(anchor)
    arm.parent = anchor
    anchor.location = ((k - 2.5) * 2.7, 0, -r * 4.0)
    anchor.rotation_euler = (0, 0, math.radians(-14))
    col = CLASSES[i]["color"]
    PAL["ring_" + col] = tuple(int(col[j:j + 2], 16) / 255 for j in (0, 2, 4)); GLOW["ring_" + col] = 4
    ped = [cyl("pedestal", 1.05, 0.18, (0, 0, -0.10), "pedestal", 32, r2=1.12, bev=0.02),
           cyl("pedestal_base", 1.15, 0.08, (0, 0, -0.22), "pedestal_dk", 32, bev=0.01),
           ring("anel", 1.06, 0.025, (0, 0, -0.005), "ring_" + col, "z", 48)]
    pd = finish("pedestal_%d" % i, ped); pd.parent = anchor; pd.location = (0, 0, 0)
for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try: sc.render.engine = eng; break
    except Exception: pass
try:
    sc.eevee.use_shadows = True; sc.eevee.use_raytracing = True
except Exception: pass
world = sc.world or bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
if bg: bg.inputs[0].default_value = (0.10, 0.09, 0.12, 1); bg.inputs[1].default_value = 1.3
for name, e, rot, col in (("key", 4.5, (50, 10, 30), (1, 0.93, 0.85)), ("rim", 3.5, (-55, 0, 195), (0.6, 0.75, 1.0)), ("fill", 1.2, (75, 0, -110), (0.9, 0.9, 1.0))):
    l = bpy.data.lights.new(name, "SUN"); l.energy = e; l.color = col
    try: l.angle = math.radians(6)
    except Exception: pass
    o = bpy.data.objects.new(name, l); sc.collection.objects.link(o); o.rotation_euler = tuple(math.radians(v) for v in rot)
cam_d = bpy.data.cameras.new("cam"); cam_d.type = "ORTHO"; cam_d.ortho_scale = 17.2
cam = bpy.data.objects.new("cam", cam_d); sc.collection.objects.link(cam)
cam.rotation_euler = (math.radians(78), 0, 0)
cam.location = (0, -20, -1.6 + 20 * math.tan(math.radians(12)))
sc.camera = cam
try: sc.view_settings.view_transform = "AgX"; sc.view_settings.look = "AgX - Punchy"
except Exception: pass
sc.render.resolution_x = 2400; sc.render.resolution_y = 1500
sc.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)
print("[Drakantus] FIM personagens:", len(lineup), "->", PREVIEW)
