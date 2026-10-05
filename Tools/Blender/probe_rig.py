# Lista ossos, malhas e animações dos personagens KayKit (para o gerador de personagens).
import bpy, os, json
ROOT = r"C:\Users\batis\Documents\Codex\2026-10-01\oi\outputs\Drakantus3D"
KK = os.path.join(ROOT, "Assets", "KayKit")
out = {}
bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete()
for name in ["Barbarian", "Knight", "Mage", "Ranger", "Rogue", "Rogue_Hooded"]:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(KK, "Characters", name + ".fbx"))
    new = [o for o in bpy.data.objects if o not in before]
    arm = next((o for o in new if o.type == "ARMATURE"), None)
    out[name] = {"objects": [(o.name, o.type, o.parent.name if o.parent else "", o.parent_bone, [round(v, 3) for v in o.dimensions]) for o in new],
                 "bones": [(b.name, [round(v, 3) for v in b.head_local], [round(v, 3) for v in b.tail_local]) for b in arm.data.bones] if arm else [],
                 "arm_scale": [round(v, 3) for v in arm.scale] if arm else None}
    for o in new: o.location.x += len(out) * 3
before = set(bpy.data.actions)
bpy.ops.import_scene.fbx(filepath=os.path.join(KK, "Animations", "Rig_Medium_General.fbx"))
out["actions"] = [a.name for a in bpy.data.actions]
with open(os.path.join(ROOT, "Tools", "Blender", "probe_rig.json"), "w") as f: json.dump(out, f, indent=1)
print("[Drakantus] probe ok")
