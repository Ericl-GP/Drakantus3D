# Descobre a regra da Unity "arma no osso handslot com posição/rotação zero" no Blender.
# Esquerda: regra R1 (osso @ matriz de importação). Direita: regra R2 (osso @ identidade, malha crua).
import bpy, os, json, math
from mathutils import Matrix, Vector
ROOT = r"C:\Users\batis\Documents\Codex\2026-10-01\oi\outputs\Drakantus3D"
KK = os.path.join(ROOT, "Assets", "KayKit")
bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=os.path.join(KK, "Animations", "Rig_Medium_General.fbx"))
for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o, do_unlink=True)
info = {}
def imp(path):
    before = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]
arms = []
for k in range(2):
    new = imp(os.path.join(KK, "Characters", "Knight.fbx"))
    arm = next(o for o in new if o.type == "ARMATURE")
    for o in new:
        if any(s in o.name for s in ("Helmet", "Cape")): o.hide_render = True
    act = next(a for a in bpy.data.actions if a.name.endswith("|Idle_A"))
    arm.animation_data_create(); arm.animation_data.action = act
    try: arm.animation_data.action_slot = act.slots[0]
    except Exception: pass
    arm.location.x = -1.2 + 2.4 * k
    arms.append(arm)
bpy.context.scene.frame_set(10); bpy.context.view_layer.update()
info["arm_matrix"] = [list(r) for r in arms[0].matrix_world]
for b in ("handslot.r", "handslot.l"):
    info["bone_" + b] = [list(r) for r in (arms[0].matrix_world @ arms[0].pose.bones[b].matrix)]
for fname, bone in (("sword_1handed", "handslot.r"), ("shield_square", "handslot.l")):
    for k, arm in enumerate(arms):
        objs = [o for o in imp(os.path.join(KK, "Weapons", fname + ".fbx")) if o.type == "MESH"]
        ob = objs[0]
        if k == 0: info[fname] = {"W0": [list(r) for r in ob.matrix_world], "dims": list(ob.dimensions),
                                  "bbox": [list(v) for v in ob.bound_box], "parent": ob.parent.name if ob.parent else ""}
        W0 = ob.matrix_world.copy(); ob.parent = None
        B = arm.matrix_world @ arm.pose.bones[bone].matrix
        ob.matrix_world = (B @ W0) if k == 0 else B
with open(os.path.join(ROOT, "Tools", "Blender", "probe_grip.json"), "w") as f: json.dump(info, f, indent=1)
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
cam_d = bpy.data.cameras.new("cam"); cam_d.type = "ORTHO"; cam_d.ortho_scale = 6
cam = bpy.data.objects.new("cam", cam_d); sc.collection.objects.link(cam)
cam.location = (0, -10, 1.1); cam.rotation_euler = (math.radians(90), 0, 0); sc.camera = cam
l = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(l); l.rotation_euler = (math.radians(50), 0, math.radians(20)); l.data.energy = 4
sc.world = sc.world or bpy.data.worlds.new("w")
sc.render.resolution_x = 1200; sc.render.resolution_y = 800
sc.render.filepath = os.path.join(ROOT, "Tools", "Blender", "probe_grip.png")
bpy.ops.render.render(write_still=True)
print("[Drakantus] probe_grip ok")
