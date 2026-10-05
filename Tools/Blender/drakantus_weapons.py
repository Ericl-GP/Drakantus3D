# Drakantus 3D — gerador de ARMAS por código (Blender 5.x) — v2 detalhada.
# Rodar no Console Python do Blender:
#   exec(open(r"C:\Users\batis\Documents\Codex\2026-10-01\oi\outputs\Drakantus3D\Tools\Blender\drakantus_weapons.py", encoding="utf-8").read())
# Saída: Assets/Drakantus/Resources/Models/Weapons/<id>.fbx (pivô na empunhadura; lâmina sobe em +Z no Blender = +Y na Unity)
#        Tools/Blender/preview_weapons.png
import bpy, bmesh, math, os, traceback
from mathutils import Vector

ROOT = r"C:\Users\batis\Documents\Codex\2026-10-01\oi\outputs\Drakantus3D"
OUT = os.path.join(ROOT, "Assets", "Drakantus", "Resources", "Models", "Weapons")
PREVIEW = os.path.join(ROOT, "Tools", "Blender", "preview_weapons.png")
os.makedirs(OUT, exist_ok=True)
TAU = math.tau

# ------------------------------------------------------------------ paleta
PAL = {
    "edge":     (0.93, 0.95, 0.98), "steel": (0.62, 0.66, 0.73), "steel_dk": (0.30, 0.33, 0.40),
    "iron":     (0.16, 0.17, 0.20), "black": (0.07, 0.07, 0.09),
    "wood":     (0.50, 0.30, 0.15), "wood_dk": (0.28, 0.16, 0.09), "wood_lt": (0.70, 0.48, 0.26),
    "leather":  (0.40, 0.22, 0.12), "leather_dk": (0.22, 0.12, 0.07), "cloth_red": (0.70, 0.12, 0.10),
    "gold":     (0.98, 0.74, 0.28), "gold_dk": (0.65, 0.42, 0.12), "brass": (0.85, 0.60, 0.28), "silver": (0.85, 0.87, 0.92),
    "ivory":    (0.94, 0.90, 0.80), "bone": (0.88, 0.82, 0.68),
    "violet":   (0.55, 0.25, 0.95), "violet_glow": (0.75, 0.45, 1.00),
    "arcane":   (0.55, 0.45, 1.00), "arcane_glow": (0.70, 0.60, 1.00),
    "holy":     (1.00, 0.90, 0.55), "holy_glow": (1.00, 0.95, 0.70),
    "ruby":     (0.95, 0.10, 0.12), "emerald": (0.15, 0.85, 0.40), "sapphire": (0.20, 0.45, 1.00),
    "blood":    (0.60, 0.03, 0.03), "blood_glow": (1.00, 0.15, 0.08),
    "guard_blue": (0.18, 0.32, 0.70), "guard_glow": (0.45, 0.75, 1.00),
    "leaf":     (0.35, 0.70, 0.30), "feather": (0.90, 0.88, 0.80), "feather_red": (0.80, 0.25, 0.18),
}
GLOW = {"violet_glow": 6, "arcane_glow": 8, "holy_glow": 8, "blood_glow": 7, "guard_glow": 6,
        "ruby": 2.5, "emerald": 2.5, "sapphire": 2.5}
METAL = {"edge", "steel", "steel_dk", "iron", "gold", "gold_dk", "brass", "silver", "black"}
_mats = {}

def mat(name):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new("DK_" + name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    c = PAL[name] + (1.0,)
    b.inputs["Base Color"].default_value = c
    b.inputs["Metallic"].default_value = 0.85 if name in METAL else 0.0
    b.inputs["Roughness"].default_value = 0.28 if name in METAL else (0.15 if name in GLOW else 0.7)
    if name in GLOW:
        for k in ("Emission Color", "Emission"):
            if k in b.inputs: b.inputs[k].default_value = c; break
        if "Emission Strength" in b.inputs: b.inputs["Emission Strength"].default_value = GLOW[name]
    m.diffuse_color = c
    _mats[name] = m
    return m

# ------------------------------------------------------------------ geometria
def clear_scene():
    bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete()
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for b in list(coll):
            if b.users == 0: coll.remove(b)
    _mats.clear()

def link(bm, name, material, smooth=False):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    if smooth:
        for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    ob.data.materials.append(mat(material))
    return ob

def bevel(ob, w=0.008, segs=2, ang=35):
    m = ob.modifiers.new("bv", "BEVEL"); m.width = w; m.segments = segs
    m.limit_method = "ANGLE"; m.angle_limit = math.radians(ang)
    return ob

def place(ob, loc=(0, 0, 0), rot=(0, 0, 0)):
    ob.location = loc; ob.rotation_euler = rot; return ob

def catmull(pts, n=6, closed=True):
    """Suaviza um contorno 2D (curvas no lugar de pontas retas)."""
    out = []; m = len(pts)
    rng = range(m) if closed else range(m - 1)
    for i in rng:
        p0 = Vector(pts[(i - 1) % m] if closed else pts[max(i - 1, 0)])
        p1 = Vector(pts[i]); p2 = Vector(pts[(i + 1) % m]); p3 = Vector(pts[(i + 2) % m] if closed else pts[min(i + 2, m - 1)])
        for k in range(n):
            t = k / n; t2 = t * t; t3 = t2 * t
            v = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            out.append((v.x, v.y))
    if not closed: out.append(pts[-1])
    return out

def slab(name, pts, thick, material, edge_thin=1.0, bev=0.004):
    """Contorno (x,z) extrudado em y. edge_thin<1 afina as bordas (fio)."""
    bm = bmesh.new(); n = len(pts)
    cx = sum(p[0] for p in pts) / n
    maxd = max(abs(p[0] - cx) for p in pts) or 1
    def th(x):
        d = abs(x - cx) / maxd
        return thick / 2 * (1 - (1 - edge_thin) * d)
    f = [bm.verts.new((x, -th(x), z)) for x, z in pts]
    b = [bm.verts.new((x, th(x), z)) for x, z in pts]
    bm.faces.new(f[::-1]); bm.faces.new(b)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((f[i], f[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = link(bm, name, material)
    if bev: bevel(ob, bev, 1, 50)
    return ob

def box(name, s, loc, material, bev=0.006, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * s[0], v.co.y * s[1], v.co.z * s[2]))
    ob = place(link(bm, name, material), loc, rot)
    if bev: bevel(ob, bev, 2)
    return ob

def cyl(name, r, h, loc, material, sides=12, r2=None, rot=(0, 0, 0), bev=0.003, smooth=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=sides, radius1=r, radius2=r if r2 is None else r2, depth=h)
    ob = place(link(bm, name, material, smooth), loc, rot)
    if bev: bevel(ob, bev, 1)
    return ob

def ball(name, r, loc, material, subd=2, scale=(1, 1, 1), smooth=True):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=subd, radius=r)
    for v in bm.verts: v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
    return place(link(bm, name, material, smooth), loc)

def gem(name, r, loc, material, scale=(1, 0.7, 1.3)):
    """Gema lapidada (facetada, brilhante)."""
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)
    for v in bm.verts: v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
    return place(link(bm, name, material, False), loc)

def tube(name, path, r, material, sides=8, r_end=None, closed=False, smooth=True):
    bm = bmesh.new(); rings = []; n = len(path)
    for i, p in enumerate(path):
        p = Vector(p)
        a_ = Vector(path[(i - 1) % n] if closed else path[max(i - 1, 0)]); b_ = Vector(path[(i + 1) % n] if closed else path[min(i + 1, n - 1)])
        t = (b_ - a_).normalized()
        ref = Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0))
        u = t.cross(ref).normalized(); w = t.cross(u).normalized()
        rr = r if r_end is None else r + (r_end - r) * i / max(1, n - 1)
        rings.append([bm.verts.new(p + (u * math.cos(k / sides * TAU) + w * math.sin(k / sides * TAU)) * rr) for k in range(sides)])
    for i in range(n if closed else n - 1):
        j = (i + 1) % n
        for k in range(sides):
            k2 = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][k2], rings[j][k2], rings[j][k]))
    if not closed: bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return link(bm, name, material, smooth)

def helix(z0, z1, r, turns, n_per=10):
    n = max(2, int(turns * n_per) + 1)
    return [(r * math.cos(i / n_per * TAU), r * math.sin(i / n_per * TAU), z0 + (z1 - z0) * i / (n - 1)) for i in range(n)]

def ring(name, r, thick, loc, material, axis="y", sides=24):
    pts = [(r * math.cos(i / sides * TAU), 0, r * math.sin(i / sides * TAU)) for i in range(sides)]
    if axis == "z": pts = [(x, z, 0) for x, _, z in pts]
    return place(tube(name, pts, thick, material, 6, closed=True), loc)

def rivets(xs_zs, y, r, material):
    return [ball("rebite", r, (x, y, z), material, 1) for x, z in xs_zs] + [ball("rebite", r, (x, -y, z), material, 1) for x, z in xs_zs]

def wrapped_grip(z0, z1, r, base="wood_dk", wrap="leather"):
    """Cabo com couro enrolado em espiral."""
    p = [cyl("cabo", r, z1 - z0, (0, 0, (z0 + z1) / 2), base, 10)]
    p.append(tube("couro", helix(z0 + 0.01, z1 - 0.01, r * 1.02, (z1 - z0) / 0.045), r * 0.32, wrap, 5))
    return p

def finish(name, parts):
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        bpy.context.view_layer.objects.active = p; p.select_set(True)
        for m in list(p.modifiers):
            try: bpy.ops.object.modifier_apply(modifier=m.name)
            except Exception: p.modifiers.remove(m)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object; ob.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    try: bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    except Exception:
        try: bpy.ops.object.shade_auto_smooth(angle=math.radians(35))
        except Exception: pass
    return ob

def export(ob, fid):
    bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    old = ob.location.copy(); oldr = ob.rotation_euler.copy(); olds = ob.scale.copy()
    ob.location = (0, 0, 0); ob.rotation_euler = (0, 0, 0); ob.scale = (1, 1, 1)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, fid + ".fbx"), use_selection=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        object_types={"MESH"}, mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="AUTO")
    ob.location = old; ob.rotation_euler = oldr; ob.scale = olds

# ------------------------------------------------------------------ lâmina em duas cores
def blade(name, pts, thick, smooth=4, core_scale=0.72, fuller=None, metal="steel", edge="edge"):
    """Fio claro + miolo mais escuro e grosso + sulco (fuller) opcional."""
    sp = catmull(pts, smooth) if smooth else pts
    parts = [slab(name + "_fio", sp, thick, edge, edge_thin=0.25, bev=0)]
    cx = sum(p[0] for p in sp) / len(sp)
    zmin = min(p[1] for p in sp)
    core = [(cx + (x - cx) * core_scale, zmin + (z - zmin) * 0.96) for x, z in sp]
    parts.append(slab(name + "_miolo", core, thick * 1.35, metal, edge_thin=0.55, bev=0))
    if fuller:
        z0, z1, w = fuller
        parts.append(slab(name + "_sulco", [(cx - w, z0), (cx + w, z0), (cx + w * 0.7, z1), (cx, z1 + w), (cx - w * 0.7, z1)], thick * 1.6, "steel_dk", bev=0))
    return parts

# ------------------------------------------------------------------ ARMAS
def make_sword():          # Guerreiro — espada de folha com guarda curva
    p = wrapped_grip(-0.10, 0.08, 0.022)
    p.append(ball("pomo", 0.038, (0, 0, -0.13), "gold", 2, (1, 0.8, 1)))
    p.append(gem("pomo_gema", 0.02, (0, 0.03, -0.13), "ruby", (1, 0.6, 1)))
    guard = [(-0.20, 0.13), (-0.16, 0.10), (-0.05, 0.085), (0.05, 0.085), (0.16, 0.10), (0.20, 0.13), (0.17, 0.125), (0.06, 0.115), (0, 0.13), (-0.06, 0.115), (-0.17, 0.125)]
    p.append(slab("guarda", catmull(guard, 3), 0.045, "gold_dk", bev=0.004))
    for s in (-1, 1): p.append(ball("ponta_guarda", 0.022, (0.205 * s, 0, 0.135), "gold", 2))
    p.append(gem("gema", 0.024, (0, 0.025, 0.105), "ruby"))
    p += blade("lamina", [(-0.045, 0.12), (0.045, 0.12), (0.058, 0.45), (0.04, 0.72), (0.0, 0.86), (-0.04, 0.72), (-0.058, 0.45)], 0.03, fuller=(0.16, 0.58, 0.011))
    return finish("sword", p)

def make_greatsword():     # Cavaleiro — espadão flamejante: lâmina larga com chamas na base, asas grandes e runas
    p = wrapped_grip(-0.26, 0.14, 0.028, "black", "cloth_red")
    p.append(cyl("anel_cabo", 0.034, 0.025, (0, 0, -0.06), "gold", 12))
    p.append(slab("pomo", catmull([(0, -0.25), (0.05, -0.28), (0.04, -0.33), (0, -0.37), (-0.04, -0.33), (-0.05, -0.28)], 3), 0.05, "gold", bev=0.004))
    p.append(gem("pomo_gema", 0.024, (0, 0.03, -0.31), "ruby", (1, 0.6, 1.2)))
    wing = [(0.04, 0.15), (0.14, 0.13), (0.26, 0.15), (0.38, 0.24), (0.42, 0.33), (0.34, 0.27), (0.30, 0.30), (0.25, 0.24), (0.20, 0.27), (0.14, 0.21), (0.04, 0.23)]
    for sg in (-1, 1):
        p.append(slab("asa", [(x * sg, z) for x, z in catmull(wing, 3)], 0.055, "gold_dk", bev=0.005))
        p.append(slab("asa_borda", [(x * sg * 0.92, z + 0.012) for x, z in catmull(wing, 3)], 0.07, "gold", bev=0.003))
    p.append(slab("centro", catmull([(-0.07, 0.13), (0.07, 0.13), (0.08, 0.22), (0, 0.29), (-0.08, 0.22)], 2), 0.08, "gold", bev=0.006))
    p.append(gem("gema", 0.04, (0, 0.045, 0.20), "ruby"))
    # lâmina larga com "chamas" perto da guarda
    pts = [(-0.10, 0.26), (-0.13, 0.33), (-0.09, 0.38), (-0.12, 0.46), (-0.095, 0.52), (-0.10, 1.10), (-0.07, 1.42), (0.0, 1.66),
           (0.07, 1.42), (0.10, 1.10), (0.095, 0.52), (0.12, 0.46), (0.09, 0.38), (0.13, 0.33), (0.10, 0.26)]
    p += blade("lamina", pts, 0.045, smooth=3, core_scale=0.70)
    for k, z in enumerate((0.60, 0.78, 0.96, 1.14)):
        p.append(slab("runa", [(-0.018, z), (0.018, z), (0.012, z + 0.09), (0, z + 0.11), (-0.012, z + 0.09)], 0.07, "blood_glow" if k % 2 else "holy_glow", bev=0))
    return finish("greatsword", p)

def make_dagger():         # Assassino — adaga kris negra com fio violeta e runa brilhando
    p = wrapped_grip(-0.08, 0.05, 0.017, "black", "violet")
    p.append(ball("pomo", 0.026, (0, 0, -0.10), "silver", 2, (1, 0.8, 1.3)))
    guard = [(-0.09, 0.07), (-0.06, 0.06), (0.05, 0.065), (0.10, 0.09), (0.07, 0.085), (0.02, 0.085), (-0.05, 0.085)]
    p.append(slab("guarda", catmull(guard, 3), 0.032, "silver", bev=0.003))
    kris = [(-0.03, 0.08), (0.032, 0.08), (0.045, 0.14), (0.02, 0.20), (0.045, 0.26), (0.02, 0.33), (-0.02, 0.43), (-0.02, 0.32), (-0.04, 0.25), (-0.015, 0.19), (-0.04, 0.13)]
    p += blade("lamina", kris, 0.02, smooth=4, metal="black", edge="violet")
    p.append(slab("runa", [(-0.005, 0.11), (0.006, 0.11), (0.003, 0.30), (-0.004, 0.30)], 0.03, "violet_glow", bev=0))
    return finish("dagger", p)

def make_pistol():         # Pistoleiro — pistola de pederneira de pirata (cano sobe em +Z; cabo inclinado para trás)
    p = []
    # coronha curva com "pera" na ponta (silhueta clássica de pistola pirata)
    stock = [(0.02, 0.10), (0.05, 0.06), (0.03, -0.02), (-0.04, -0.12), (-0.11, -0.17), (-0.15, -0.15), (-0.14, -0.10), (-0.08, -0.05), (-0.04, 0.03), (-0.04, 0.10)]
    p.append(slab("coronha", catmull(stock, 4), 0.055, "wood", bev=0.006))
    p.append(ball("pera", 0.045, (-0.135, 0, -0.15), "brass", 2, (1.2, 0.9, 1.0)))
    p.append(slab("placa_lado", catmull([(-0.035, 0.04), (0.03, 0.04), (0.035, 0.14), (-0.03, 0.14)], 2), 0.064, "brass", bev=0.004))
    p.append(slab("gravura", catmull([(-0.02, 0.07), (0.015, 0.06), (0.02, 0.11), (-0.015, 0.12)], 2), 0.068, "gold_dk", bev=0))
    p.append(cyl("cano", 0.024, 0.40, (0.0, 0, 0.33), "iron", 12))
    p.append(cyl("vareta", 0.007, 0.32, (0.032, 0, 0.30), "wood_dk", 6))
    p.append(cyl("boca", 0.025, 0.09, (0.0, 0, 0.56), "brass", 14, r2=0.042))
    p.append(ring("borda_boca", 0.042, 0.006, (0, 0, 0.605), "gold", "z"))
    for z in (0.18, 0.30, 0.42): p.append(cyl("anel", 0.029, 0.02, (0, 0, z), "brass", 12))
    hammer = [(-0.03, 0.11), (-0.055, 0.12), (-0.075, 0.15), (-0.085, 0.19), (-0.065, 0.18), (-0.055, 0.15), (-0.035, 0.13)]
    p.append(slab("cao", hammer, 0.018, "iron", bev=0))
    p.append(box("pederneira", (0.022, 0.024, 0.03), (-0.072, 0, 0.18), "black", 0.003))
    p.append(slab("caçoleta", [(-0.03, 0.15), (-0.005, 0.15), (-0.005, 0.20), (-0.03, 0.19)], 0.03, "iron", bev=0.002))
    p.append(tube("guarda_gatilho", [(0.035, 0, 0.06), (0.075, 0, 0.02), (0.07, 0, -0.02), (0.03, 0, -0.03)], 0.007, "brass", 6))
    p.append(box("gatilho", (0.009, 0.009, 0.04), (0.045, 0, 0.02), "iron", 0, (0, math.radians(25), 0)))
    p.append(ball("mira", 0.009, (-0.025, 0, 0.58), "brass", 1))
    return finish("pistol", p)

def bow_limb(h, depth, curl, n=21):
    """Arco recurvo: curva principal + pontas viradas para fora."""
    pts = []
    for i in range(n):
        t = i / (n - 1) * 2 - 1
        x = -depth * (1 - t * t) ** 0.8
        if abs(t) > 0.72: x += curl * ((abs(t) - 0.72) / 0.28) ** 1.6
        pts.append((x, 0, h * t))
    return pts

def make_bow(name="bow", h=0.58, depth=0.17, curl=0.10, r=0.021, wood="wood", trim="leather_dk", tip="gold", extra=False):
    arc = bow_limb(h, depth, curl)
    n = len(arc)
    # braço em camadas: madeira + tala escura nas costas (cara de arco composto)
    p = [tube("arco", arc, r, wood, 8)]
    back = [(x - r * 0.9, 0, z) for x, _, z in arc]
    p.append(tube("tala", back[2:-2], r * 0.55, trim, 6))
    for k in (4, 7, n - 8, n - 5): p.append(cyl("faixa", r * 1.35, 0.025, arc[k], "gold" if extra else "leather", 10))
    p.append(cyl("pegada", r * 1.6, 0.17, (-depth, 0, 0), "leather_dk", 10))
    wrap = tube("couro", helix(-0.08, 0.08, r * 1.65, 3.5), r * 0.35, "leather", 5); wrap.location = (-depth, 0, 0); p.append(wrap)
    p.append(slab("descanso", [(-depth - 0.005, 0.085), (-depth + 0.03, 0.085), (-depth + 0.03, 0.10), (-depth - 0.005, 0.11)], 0.03, "gold" if extra else "brass", bev=0.002))
    p.append(tube("corda", [arc[0], (arc[0][0] + 0.005, 0, 0), arc[-1]], 0.0045, "ivory", 4, smooth=False))
    for pt in (arc[0], arc[-1]):
        p.append(cyl("ponteira", r * 1.2, 0.05, pt, tip, 8, r2=r * 0.3))
    # flecha encaixada
    p.append(cyl("flecha", 0.007, 0.78, (arc[0][0] + 0.005 - 0.02, 0, 0), "wood_lt", 6, rot=(0, math.radians(90), 0)))
    p[-1].location = (-0.37 + 0.02, 0, 0.0)
    p.append(cyl("ponta_flecha", 0.016, 0.06, (-0.80, 0, 0), "steel", 4, r2=0.0, rot=(0, math.radians(-90), 0)))
    for k in range(3):
        fe = slab("pena", [(0, 0), (0.07, 0.0), (0.06, 0.03), (0.0, 0.015)], 0.004, "feather_red" if k == 1 else "feather", bev=0)
        fe.location = (0.0, 0, 0); fe.rotation_euler = (math.radians(120 * k), 0, 0); p.append(fe)
    if extra:   # arco longo: chapas de prata, gema e penas penduradas
        p.append(gem("gema", 0.032, (-depth - 0.035, 0, 0.13), "emerald"))
        for sg in (-1, 1):
            p.append(slab("chapa", catmull([(-depth - 0.035, 0.17 * sg), (-depth + 0.02, 0.17 * sg), (-depth + 0.0, 0.34 * sg), (-depth - 0.045, 0.31 * sg)], 2), 0.05, "silver", bev=0.003))
        for k in range(3):
            fe = slab("pena", catmull([(0, 0), (0.025, 0.03), (0.02, 0.10), (0, 0.13), (-0.02, 0.10), (-0.025, 0.03)], 3), 0.006, "feather" if k != 1 else "feather_red", bev=0)
            fe.location = (-depth - 0.02 + k * 0.025, 0, -0.24 - k * 0.03); fe.rotation_euler = (0, math.radians(170 + k * 8), 0)
            p.append(fe)
    return finish(name, p)

def make_longbow():
    return make_bow("longbow", h=0.9, depth=0.23, curl=0.14, r=0.025, wood="wood_dk", trim="black", tip="silver", extra=True)

def twisted_shaft(z0, z1, r, material):
    """Haste de madeira retorcida (duas tranças)."""
    parts = []
    for k in range(2):
        pts = [(r * 0.55 * math.cos(i / 8 * TAU + k * math.pi), r * 0.55 * math.sin(i / 8 * TAU + k * math.pi), z0 + (z1 - z0) * i / 40) for i in range(41)]
        parts.append(tube("torcao", pts, r * 0.75, material, 7))
    return parts

def make_staff_arcane():   # Mago / Arqui-mago — madeira retorcida com garras segurando cristais
    p = twisted_shaft(-0.40, 1.02, 0.03, "wood_dk")
    p.append(cyl("ponta", 0.03, 0.08, (0, 0, -0.44), "silver", 8, r2=0.008))
    for z in (0.15, 0.55, 0.95): p.append(cyl("anel", 0.036, 0.03, (0, 0, z), "silver", 12))
    for k in range(4):
        a = k / 4 * TAU
        claw = [(0.03 * math.cos(a), 0.03 * math.sin(a), 1.0), (0.10 * math.cos(a), 0.10 * math.sin(a), 1.10),
                (0.11 * math.cos(a), 0.11 * math.sin(a), 1.22), (0.05 * math.cos(a), 0.05 * math.sin(a), 1.33)]
        p.append(tube("garra", claw, 0.014, "wood_dk", 6, r_end=0.006))
    p.append(gem("cristal", 0.085, (0, 0, 1.22), "arcane_glow", (0.75, 0.75, 1.9)))
    for k in range(3):
        a = k / 3 * TAU
        p.append(gem("orbita", 0.022, (0.19 * math.cos(a), 0.19 * math.sin(a), 1.24 + 0.05 * math.sin(a * 2)), "arcane_glow", (0.7, 0.7, 1.4)))
    for k in range(3):
        a = k / 3 * TAU + 0.5
        p.append(gem("cristal_peq", 0.025, (0.05 * math.cos(a), 0.05 * math.sin(a), 1.11), "arcane", (0.6, 0.6, 1.6)))
    aro = ring("aro", 0.19, 0.007, (0, 0, 1.24), "arcane_glow", "z"); aro.rotation_euler = (math.radians(18), math.radians(10), 0); p.append(aro)
    return finish("staff_arcane", p)

def make_staff_holy():     # Sacerdote — cajado branco e ouro com sol alado
    p = [cyl("haste", 0.026, 1.42, (0, 0, 0.30), "ivory", 12, r2=0.022)]
    p.append(cyl("ponta", 0.032, 0.09, (0, 0, -0.44), "gold", 10, r2=0.01))
    for z in (0.0, 0.5, 0.9): p.append(cyl("anel", 0.032, 0.03, (0, 0, z), "gold", 12))
    p.append(tube("fita", helix(0.55, 0.95, 0.03, 2.5), 0.008, "sapphire", 5))
    p.append(cyl("colar", 0.045, 0.06, (0, 0, 1.03), "gold", 12, r2=0.03))
    p.append(ring("sol", 0.13, 0.018, (0, 0, 1.22), "gold", "y"))
    for i in range(12):
        a = i / 12 * TAU; L = 0.09 if i % 2 == 0 else 0.055
        ray = slab("raio", [(-0.014, 0), (0.014, 0), (0, L)], 0.02, "gold", bev=0)
        ray.location = (0.15 * math.cos(a), 0, 1.22 + 0.15 * math.sin(a)); ray.rotation_euler = (0, -a + math.pi / 2, 0)
        p.append(ray)
    for s in (-1, 1):
        wing = [(0.0, 0.0), (0.08, 0.05), (0.18, 0.12), (0.24, 0.20), (0.16, 0.15), (0.20, 0.10), (0.11, 0.06), (0.14, 0.02), (0.05, 0.0)]
        w = slab("asa", [(x * s, z) for x, z in catmull(wing, 3)], 0.02, "ivory", bev=0.003)
        w.location = (0.05 * s, -0.01, 1.08); p.append(w)
    p.append(ball("luz", 0.075, (0, 0, 1.22), "holy_glow", 2))
    return finish("staff_holy", p)

def make_hammer():         # Tank — martelo de guerra ornamentado
    p = wrapped_grip(-0.10, 0.62, 0.026, "wood_dk", "leather")
    p.append(ball("pomo", 0.04, (0, 0, -0.14), "iron", 2, (1, 1, 1.2)))
    head = [(-0.16, 0.62), (0.16, 0.62), (0.18, 0.66), (0.18, 0.80), (0.16, 0.84), (-0.16, 0.84), (-0.18, 0.80), (-0.18, 0.66)]
    p.append(slab("cabeca", head, 0.16, "steel_dk", bev=0.012))
    for x in (-0.12, 0.12): p.append(slab("faixa", [(x - 0.02, 0.61), (x + 0.02, 0.61), (x + 0.02, 0.85), (x - 0.02, 0.85)], 0.175, "gold", bev=0.003))
    p.append(cyl("face", 0.075, 0.04, (0.20, 0, 0.73), "steel", 8, rot=(0, math.radians(90), 0)))
    p.append(cyl("face2", 0.075, 0.04, (-0.20, 0, 0.73), "steel", 8, rot=(0, math.radians(90), 0)))
    p.append(cyl("espigao", 0.03, 0.12, (0, 0, 0.90), "steel", 8, r2=0.0))
    p += rivets([(-0.06, 0.69), (0.06, 0.69), (-0.06, 0.77), (0.06, 0.77)], 0.082, 0.012, "gold")
    p.append(gem("gema", 0.028, (0, 0.085, 0.73), "sapphire"))
    return finish("hammer", p)

def make_axe(name="axe", blood=False):   # Berserker — machado de guerra pesado
    p = wrapped_grip(-0.12, 0.56, 0.027, "wood_dk", "cloth_red" if not blood else "black")
    p.append(ball("pomo", 0.035, (0, 0, -0.15), "iron", 2))
    p.append(cyl("ponta", 0.028, 0.08, (0, 0, -0.21), "iron", 8, r2=0.0))
    bit = [(0.03, 0.42), (0.10, 0.40), (0.20, 0.33), (0.30, 0.30), (0.34, 0.40), (0.35, 0.55), (0.32, 0.68), (0.27, 0.74), (0.20, 0.66), (0.10, 0.60), (0.03, 0.60)]
    sp = catmull(bit, 3)
    p.append(slab("lamina_fio", sp, 0.035, "edge" if not blood else "blood", edge_thin=0.2, bev=0))
    core = [(0.03 + (x - 0.03) * 0.72, z) for x, z in sp]
    p.append(slab("lamina", core, 0.05, "steel_dk" if not blood else "black", edge_thin=0.6, bev=0))
    back = [(-0.03, 0.46), (-0.12, 0.44), (-0.18, 0.47), (-0.13, 0.51), (-0.18, 0.56), (-0.12, 0.58), (-0.03, 0.56)]
    p.append(slab("costas", catmull(back, 2), 0.045, "steel_dk" if not blood else "black", bev=0.004))
    p.append(cyl("olho", 0.042, 0.24, (0, 0, 0.51), "iron", 10))
    for z in (0.40, 0.62): p.append(cyl("aro", 0.047, 0.025, (0, 0, z), "gold_dk" if not blood else "blood", 10))
    p += rivets([(0.08, 0.47), (0.08, 0.55)], 0.026, 0.011, "gold" if not blood else "blood_glow")
    p.append(cyl("espigao", 0.03, 0.14, (0, 0, 0.70), "steel_dk" if not blood else "black", 8, r2=0.0))
    for sg in (-1, 1): p.append(cyl("chifre", 0.014, 0.07, (-0.06, 0.03 * sg, 0.63), "bone", 6, r2=0.0, rot=(math.radians(-30 * sg), math.radians(-40), 0)))
    if blood:
        rune = [(0.12, 0.44), (0.15, 0.43), (0.22, 0.49), (0.19, 0.53), (0.24, 0.60), (0.21, 0.62), (0.16, 0.55), (0.18, 0.51)]
        p.append(slab("runa", catmull(rune, 2), 0.06, "blood_glow", bev=0))
        for z, x in ((0.36, 0.25), (0.70, 0.28)): p.append(ball("gota", 0.012, (x, 0.0, z), "blood_glow", 1, (1, 1.6, 1.4)))
    else:
        p.append(slab("fita", [(0.0, 0.30), (0.03, 0.30), (0.05, 0.14), (0.02, 0.15)], 0.01, "cloth_red", bev=0))
    return finish(name, p)

def make_guard_sword():    # The Guard — espada larga com guarda de asas e runas azuis
    p = wrapped_grip(-0.11, 0.08, 0.023, "black", "guard_blue")
    p.append(ball("pomo", 0.04, (0, 0, -0.14), "gold", 2, (1, 0.8, 1.1)))
    p.append(gem("pomo_gema", 0.02, (0, 0.03, -0.14), "sapphire", (1, 0.6, 1)))
    wing = [(0.02, 0.10), (0.10, 0.09), (0.19, 0.11), (0.24, 0.17), (0.18, 0.14), (0.15, 0.17), (0.11, 0.13), (0.02, 0.14)]
    for s in (-1, 1): p.append(slab("asa", [(x * s, z) for x, z in catmull(wing, 3)], 0.045, "gold", bev=0.004))
    p.append(slab("escudete", catmull([(-0.045, 0.09), (0.045, 0.09), (0.04, 0.15), (0, 0.18), (-0.04, 0.15)], 2), 0.06, "guard_blue", bev=0.004))
    p += blade("lamina", [(-0.06, 0.16), (0.06, 0.16), (0.065, 0.60), (0.05, 0.82), (0.0, 0.96), (-0.05, 0.82), (-0.065, 0.60)], 0.034)
    p.append(slab("runa", [(-0.008, 0.22), (0.008, 0.22), (0.008, 0.74), (0, 0.78), (-0.008, 0.74)], 0.05, "guard_glow", bev=0))
    return finish("guard_sword", p)

def make_tower_shield():   # The Guard — escudo-torre com moldura, umbo e asas
    outline = [(-0.30, -0.48), (0.30, -0.48), (0.34, 0.20), (0.24, 0.42), (0.0, 0.54), (-0.24, 0.42), (-0.34, 0.20)]
    o = catmull(outline, 3)
    p = [place(slab("placa", o, 0.05, "gold_dk", bev=0.006), (0, 0.06, 0))]
    inner = [(x * 0.86, z * 0.88 + 0.01) for x, z in o]
    p.append(place(slab("campo", inner, 0.05, "guard_blue", bev=0.003), (0, 0.02, 0)))
    p.append(place(slab("faixa_v", [(-0.035, -0.40), (0.035, -0.40), (0.035, 0.44), (-0.035, 0.44)], 0.02, "silver", bev=0.002), (0, -0.01, 0)))
    p.append(place(slab("faixa_h", [(-0.27, 0.05), (0.27, 0.05), (0.27, 0.12), (-0.27, 0.12)], 0.02, "silver", bev=0.002), (0, -0.01, 0)))
    p.append(ball("umbo", 0.085, (0, -0.02, 0.085), "gold", 2, (1, 0.55, 1)))
    p.append(gem("gema", 0.035, (0, -0.065, 0.085), "guard_glow"))
    for x, z in [(-0.24, -0.40), (0.24, -0.40), (-0.29, 0.18), (0.29, 0.18), (0, 0.48), (-0.18, 0.40), (0.18, 0.40)]:
        p.append(ball("rebite", 0.016, (x, 0.03, z), "gold", 1))
    for s in (-1, 1):
        wing = [(0.06, 0.20), (0.14, 0.24), (0.20, 0.32), (0.13, 0.30), (0.16, 0.36), (0.08, 0.30)]
        p.append(place(slab("asa", [(x * s, z) for x, z in catmull(wing, 2)], 0.02, "silver", bev=0.002), (0, -0.01, 0)))
    p.append(box("punho", (0.04, 0.05, 0.16), (0, 0.11, 0.0), "leather", 0.006))
    return finish("tower_shield", p)

def make_spear():          # Tank — lança de guarda: ponta larga com lâminas laterais e borla
    p = [cyl("haste", 0.023, 1.60, (0, 0, 0.42), "wood_dk", 10)]
    p.append(tube("couro", helix(-0.15, 0.25, 0.024, 9), 0.008, "leather", 5))
    p += [cyl("anel", 0.029, 0.03, (0, 0, z), "brass", 10) for z in (-0.30, 0.0, 0.3, 0.9)]
    p.append(cyl("ponta_baixo", 0.028, 0.09, (0, 0, -0.43), "iron", 8, r2=0.005))
    p.append(cyl("colar", 0.034, 0.12, (0, 0, 1.27), "gold", 12, r2=0.028))
    p += blade("ponta", [(-0.05, 1.33), (0.05, 1.33), (0.07, 1.45), (0.04, 1.58), (0.0, 1.76), (-0.04, 1.58), (-0.07, 1.45)], 0.03, smooth=3, fuller=(1.38, 1.62, 0.009))
    for sg in (-1, 1):
        p.append(slab("lamina_lado", catmull([(0.02 * sg, 1.30), (0.10 * sg, 1.28), (0.17 * sg, 1.34), (0.12 * sg, 1.36), (0.06 * sg, 1.35)], 2), 0.025, "steel", bev=0.002))
    p.append(cyl("borla_topo", 0.03, 0.03, (0, 0, 1.20), "cloth_red", 10))
    for k in range(6):
        a = k / 6 * TAU
        p.append(tube("borla", [(0.02 * math.cos(a), 0.02 * math.sin(a), 1.19), (0.05 * math.cos(a), 0.05 * math.sin(a), 1.10), (0.06 * math.cos(a), 0.06 * math.sin(a), 1.02)], 0.008, "cloth_red", 5))
    return finish("spear", p)

# ------------------------------------------------------------------ prévia: grade, cada arma ampliada no seu quadro, vista 3/4
def preview(objs):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try: sc.render.engine = eng; break
        except Exception: pass
    world = sc.world or bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg: bg.inputs[0].default_value = (0.16, 0.14, 0.17, 1); bg.inputs[1].default_value = 0.9
    def sun(name, e, rot, col=(1, 1, 1)):
        l = bpy.data.lights.new(name, "SUN"); l.energy = e; l.color = col
        o = bpy.data.objects.new(name, l); sc.collection.objects.link(o); o.rotation_euler = rot
    sun("key", 4.0, (math.radians(45), math.radians(15), math.radians(35)), (1, 0.95, 0.88))
    sun("rim", 3.0, (math.radians(-60), 0, math.radians(200)), (0.7, 0.8, 1.0))
    sun("fill", 0.8, (math.radians(80), 0, math.radians(-120)))
    cols, cell = 7, 1.0
    for i, o in enumerate(objs):
        r, c = divmod(i, cols)
        size = max(o.dimensions.x, o.dimensions.z, 0.2)
        s = 0.88 / size
        o.scale = (s, s, s); o.location = (0, 0, 0)
        o.rotation_euler = (0, math.radians(-90), math.radians(-14)) if o.name.startswith("pistol") else (0, 0, math.radians(-14))
        bpy.context.view_layer.update()
        bb = [o.matrix_world @ Vector(v) for v in o.bound_box]
        center = sum(bb, Vector()) / 8
        o.location = ((c * cell - (cols - 1) * cell / 2) - center.x, -center.y, (-r * 1.05 * cell) - center.z)
    cam_d = bpy.data.cameras.new("cam"); cam_d.type = "ORTHO"; cam_d.ortho_scale = 7.4
    cam = bpy.data.objects.new("cam", cam_d); sc.collection.objects.link(cam)
    rows = (len(objs) + cols - 1) // cols
    cam.location = (0, -8, -(rows - 1) * 1.05 * cell / 2); cam.rotation_euler = (math.radians(90), 0, 0); sc.camera = cam
    sc.render.resolution_x = 2100; sc.render.resolution_y = int(2100 * (rows * 1.05 + 0.1) / 7.4)
    sc.render.filepath = PREVIEW
    bpy.ops.render.render(write_still=True)

# ------------------------------------------------------------------ main
if not globals().get("DK_NO_MAIN"):
  clear_scene()
  made = []
  for fid, fn in [
      ("gen_sword", make_sword), ("gen_greatsword", make_greatsword), ("gen_dagger", make_dagger),
      ("gen_pistol", make_pistol), ("gen_bow", make_bow), ("gen_longbow", make_longbow),
      ("gen_staff_arcane", make_staff_arcane), ("gen_staff_holy", make_staff_holy),
      ("gen_hammer", make_hammer), ("gen_axe", make_axe), ("gen_axe_blood", lambda: make_axe("axe_blood", True)),
      ("gen_guard_sword", make_guard_sword), ("gen_tower_shield", make_tower_shield), ("gen_spear", make_spear),
  ]:
      try:
          ob = fn(); export(ob, fid); made.append(ob)
          print("[Drakantus] exportado", fid, len(ob.data.polygons), "faces")
      except Exception as e:
          traceback.print_exc(); print("[Drakantus] ERRO em", fid, e)
  try:
      preview(made); print("[Drakantus] prévia:", PREVIEW)
  except Exception:
      traceback.print_exc()
  print("[Drakantus] FIM:", len(made), "armas")
