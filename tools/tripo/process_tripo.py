"""Headless Blender pipeline for the owner's Tripo3D models (HARVEST PANIC).

Decimates each model to its art-bible budget (UVs preserved by edge-collapse), scales it to real size,
moves the pivot (base-centre; net = grip end; gate = hinge side), orients it so the model front is Blender -Y
(= Unity +Z after FBX export with forward -Z / up Y + baked space transform), and writes
<Name>.fbx + <Name>_BaseColor.jpg into HarvestPanic/Assets/ThirdParty/Tripo/<Name>/.

Usage:
  Blender -b --factory-startup --python tools/tripo/process_tripo.py -- <src_root> <unity_tripo_dir> [Name ...]
src_root defaults to ~/Desktop/HarvestPanic Tripo Models.
"""
import bpy, bmesh, glob, math, os, sys
import numpy as np
from mathutils import Matrix, Vector

# name, source folder, tri budget, fit axis ('length' = max horizontal, 'height', 'width' = X), metres, yaw (deg about Z
# applied after import so the front faces -Y), pivot mode, texture size
SPECS = [
    ("Pig_Hamlet",   "01_Pig_Hamlet",   5000, "length", 1.10, 0,   "base",  1024),
    ("Chicken",      "02_Chicken",      3000, "height", 0.40, 0,   "base",  1024),
    ("PrizePumpkin", "03_PrizePumpkin", 4000, "width",  1.60, 0,   "base",  2048),
    ("EggBasket",    "04_EggBasket",    3000, "width",  0.35, 0,   "base",  1024),
    ("FeedBucket",   "05_FeedBucket",   2000, "width",  0.32, 0,   "base",  1024),
    ("Net",          "06_Net",          3000, "length", 1.40, 0,   "grip",  1024),
    ("MarketStall",  "08_MarketStall",  6000, "width",  3.00, 0,   "base",  2048),
    ("PenGate",      "09_PenGate",      3000, "width",  2.00, 0,   "hinge", 1024),
]


# Models whose basecolor is re-baked onto a fresh UV unwrap of the decimated mesh (selected-to-active, Cycles).
# Needed where edge-collapse drags UVs across Tripo's many small islands and leaks a neighbour's colour
# (prize pumpkin: blue ribbon specks on the orange body).
REBAKE = {"PrizePumpkin"}


def tris(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def verts_np(me):
    a = np.empty(len(me.vertices) * 3, dtype=np.float64)
    me.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def decimate(obj, target):
    """Collapse decimation, bisecting the ratio until the triangle count is at or just under budget."""
    me0 = obj.data.copy()
    lo, hi, best = 0.0, 1.0, None
    for _ in range(14):
        ratio = (lo + hi) * 0.5 if best is not None or lo > 0 else min(1.0, target / max(1, tris(me0)))
        obj.data = me0.copy()
        mod = obj.modifiers.new("dec", "DECIMATE")
        mod.decimate_type = "COLLAPSE"; mod.ratio = ratio; mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
        t = tris(obj.data)
        if t <= target:
            best = (ratio, obj.data.copy()); lo = ratio
            if t > target * 0.97: break
        else:
            hi = ratio
    if best is not None:
        obj.data = best[1]
    return tris(obj.data)


def align_net(obj):
    """Shaft -> -Y with the grip tip at the origin, net pocket -> -Z.

    The source stands the net diagonally with the grip at the bottom (min Z), so the bare shaft is the low-Z slice:
    the centroids of two thin slices give the shaft direction; the head centroid offset from that line is the pocket side.
    """
    v = verts_np(obj.data)
    z0, z1 = v[:, 2].min(), v[:, 2].max(); h = z1 - z0
    def slice_c(a, b):
        sel = (v[:, 2] >= z0 + a * h) & (v[:, 2] <= z0 + b * h); return v[sel].mean(0)
    c0, c1 = slice_c(0.03, 0.08), slice_c(0.18, 0.23)
    s = c1 - c0; s /= np.linalg.norm(s)
    # refine: PCA on vertices close to the current shaft line, grip half only (the bare pole)
    for _ in range(6):
        d = v - c0; t = d @ s
        r = np.linalg.norm(d - np.outer(t, s), axis=1)
        sel = (r < 0.05 * h) & (t > -0.02 * h) & (t < 0.45 * t.max())
        if sel.sum() < 20: break
        c = v[sel].mean(0)
        _, _, vt = np.linalg.svd(v[sel] - c, full_matrices=False)
        s = vt[0] if vt[0] @ s > 0 else -vt[0]
        c0 = c - (c - c0) @ s * s if False else c - s * ((c - c0) @ s)
    head = v[v[:, 2] > z0 + 0.6 * h]
    off = head.mean(0) - c0; off = off - (off @ s) * s
    p = off / max(1e-9, np.linalg.norm(off))
    Y, Z = -s, -p
    R = np.array([np.cross(Y, Z), Y, Z])
    obj.data.transform(Matrix(((*R[0], 0), (*R[1], 0), (*R[2], 0), (0, 0, 0, 1))))
    v = verts_np(obj.data)
    ymax = v[:, 1].max()
    tip = v[v[:, 1] > ymax - 0.02 * (ymax - v[:, 1].min())].mean(0)
    obj.data.transform(Matrix.Translation(Vector((-tip[0], -ymax, -tip[2]))))


def rebake(high, low, size, out_path):
    """Smart-UV the low-poly mesh and bake the high-poly basecolor onto it."""
    sc = bpy.context.scene
    for o in bpy.data.objects: o.select_set(False)
    bpy.context.view_layer.objects.active = low; low.select_set(True)
    while low.data.uv_layers: low.data.uv_layers.remove(low.data.uv_layers[0])
    low.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    img = bpy.data.images.new("baked", size, size, alpha=False)
    mat = bpy.data.materials.new("bake_target"); mat.use_nodes = True
    node = mat.node_tree.nodes.new("ShaderNodeTexImage"); node.image = img
    mat.node_tree.nodes.active = node
    low.data.materials.clear(); low.data.materials.append(mat)
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 1
    sc.render.bake.use_pass_direct = False; sc.render.bake.use_pass_indirect = False; sc.render.bake.use_pass_color = True
    sc.render.bake.use_selected_to_active = True; sc.render.bake.cage_extrusion = 0.01; sc.render.bake.margin = 8
    high.hide_set(False); high.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
    img.filepath_raw = out_path; img.file_format = "JPEG"
    sc.render.image_settings.quality = 90
    img.save()
    return mat


def process(src_root, out_root, spec):
    name, folder, budget, fit, metres, yaw, pivot, texsize = spec
    fbx = glob.glob(os.path.join(src_root, folder, "*.fbx"))[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    obj = meshes[0]
    bpy.context.view_layer.objects.active = obj
    for o in bpy.data.objects: o.select_set(o == obj)
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    src_tris = tris(obj.data)
    if yaw: obj.data.transform(Matrix.Rotation(math.radians(yaw), 4, "Z"))
    if pivot == "grip": align_net(obj)

    high = None
    if name in REBAKE:
        high = obj.copy(); high.data = obj.data.copy(); bpy.context.scene.collection.objects.link(high)
    got = decimate(obj, budget)
    out_dir = os.path.join(out_root, name); os.makedirs(out_dir, exist_ok=True)
    tex_name = f"{name}_BaseColor.jpg"
    if high is not None:
        baked_mat = rebake(high, obj, texsize, os.path.join(out_dir, tex_name))
        bpy.data.objects.remove(high)

    v = verts_np(obj.data); size = v.max(0) - v.min(0)
    cur = {"length": max(size[0], size[1]) if pivot != "grip" else size[1], "height": size[2], "width": size[0]}[fit]
    obj.data.transform(Matrix.Scale(metres / cur, 4))
    v = verts_np(obj.data); mn, mx = v.min(0), v.max(0)
    if pivot == "base":
        off = Vector((-(mn[0] + mx[0]) / 2, -(mn[1] + mx[1]) / 2, -mn[2]))
    elif pivot == "hinge":
        off = Vector((-mn[0], -(mn[1] + mx[1]) / 2, -mn[2]))
    else:
        off = Vector((0, 0, 0))
    obj.data.transform(Matrix.Translation(off))
    obj.name = obj.data.name = "SM_Tripo_" + name
    obj.data.shade_smooth() if hasattr(obj.data, "shade_smooth") else None

    # texture: resize basecolor, save next to the FBX (unless re-baked above)
    img = [i for i in bpy.data.images if i.size[0] > 0 and i.name != "baked"]
    if img and high is None:
        im = img[0]; im.scale(texsize, texsize)
        im.filepath_raw = os.path.join(out_dir, tex_name); im.file_format = "JPEG"
        bpy.context.scene.render.image_settings.quality = 90
        im.save()
    mat = obj.data.materials[0] if obj.data.materials else bpy.data.materials.new("M")
    mat.name = "M_Tripo_" + name
    for o in list(bpy.data.objects):
        if o != obj: bpy.data.objects.remove(o)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, name + ".fbx"), use_selection=False, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, path_mode="STRIP", embed_textures=False)
    v = verts_np(obj.data); mn, mx = v.min(0), v.max(0)
    print(f"TRIPO {name}: tris {src_tris} -> {got} (budget {budget}); size m x={mx[0]-mn[0]:.3f} y={mx[1]-mn[1]:.3f} z={mx[2]-mn[2]:.3f}; tex {texsize}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    src = argv[0] if len(argv) > 0 else os.path.expanduser("~/Desktop/HarvestPanic Tripo Models")
    out = argv[1] if len(argv) > 1 else os.path.join(os.path.dirname(__file__), "../../HarvestPanic/Assets/ThirdParty/Tripo")
    only = set(argv[2:])
    for s in SPECS:
        if not only or s[0] in only: process(src, os.path.abspath(out), s)


main()
