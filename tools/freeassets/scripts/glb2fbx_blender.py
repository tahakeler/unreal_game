# blender --background --factory-startup --python glb2fbx.py -- in.glb out.fbx [MeshName:Mat1,Mat2 ...]
import bpy, sys, bmesh
argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, strips = argv[0], argv[1], argv[2:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for spec in strips:
    mesh_name, mats = spec.split(":"); mats = set(mats.split(","))
    ob = bpy.data.objects[mesh_name]
    idx = {i for i, s in enumerate(ob.material_slots) if s.material and s.material.name in mats}
    bm = bmesh.new(); bm.from_mesh(ob.data)
    dead = [f for f in bm.faces if f.material_index in idx]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(ob.data); bm.free()
    print("stripped", len(dead), "faces from", mesh_name, mats)
for a in bpy.data.actions:
    a.name = a.name.split("|")[-1]
    print("action", a.name, a.frame_range[:])
bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, use_armature_deform_only=False, mesh_smooth_type='FACE')
print("WROTE", dst)
