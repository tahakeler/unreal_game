"""Headless Blender pipeline for the owner's rigged Tripo hero farmer (HARVEST PANIC player character).

Input : the Tripo FBX (UE-mannequin style rig: pelvis, spine_01..03, clavicle, upperarm, lowerarm, hand, thigh, calf,
        foot, ball, neck_01, head + fingers; one mesh, one material) and its basecolor texture.
Output (into <out_dir>, normally HarvestPanic/Assets/ThirdParty/Tripo/SK_Farmer_Hero/):
  SK_Farmer_Hero.fbx              scaled to <height> m, decimated to ~<tris> triangles (weights + UVs kept),
                                  UV0 = Tripo atlas, UV1 "PatternUV" = box projection in metres (outfit patterns)
  SK_Farmer_Hero_BaseColor.png    2048 copy of the 8192 basecolor
  SK_Farmer_Hero_Mask.png         RGBA recolour mask: R overalls, G shirt, B boots + gloves, A skin (0 = fixed colour)
  SK_Farmer_Hero_Regions.png      R straw hat, G hat band, B hair (baked-hat hiding, band/hair tints)
  SK_Farmer_Hero_stats.json       per zone: coverage %, mean linear RGB + luminance (the shader's identity reference),
                                  face (eye centres, protected texels), skin-weight check, tris, height

Segmentation = body part (dominant bone of each triangle, rasterised into UV space) + the surface position under
each texel + HSV of the texel. Body part separates the straw hat from the gloves (same hue); position finds the face:
the eyes are located from the sclera texels and the whole face front is "skin only" - eyes, brows, mouth line,
nostrils and anything else that is not skin-coloured there is protected (no zone, never tinted). Hair is only taken
off the face, and speckles are removed so the hairline does not turn into stubble when the hair colour changes.

Skin weights: the decimated mesh gets its weights re-transferred from the untouched source surface (interpolated,
4 influences, normalised), and the deformation of both meshes is compared in a stress pose (stats "deform").
The source files are only read; originals stay untouched.

Usage:
  Blender -b --factory-startup --python tools/tripo/farmer_hero.py -- <src.fbx> <src_basecolor> <out_dir> [height=1.75] [tris=10000] [debug_dir]
"""
import bpy, bmesh, json, math, os, sys
import numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, SRC_TEX, OUT = argv[0], argv[1], argv[2]
HEIGHT = float(argv[3]) if len(argv) > 3 else 1.75
TARGET_TRIS = int(argv[4]) if len(argv) > 4 else 10000
DEBUG = argv[5] if len(argv) > 5 else None
RES = 2048
NAME = "SK_Farmer_Hero"

PARTS = ["head", "hand", "forearm", "upperarm", "torso", "thigh", "calf", "foot"]


def part_of(bone):
    b = bone.lower()
    if b.startswith(("head", "neck")): return "head"
    if b.startswith(("hand", "index", "middle", "ring", "pinky", "thumb")): return "hand"
    if b.startswith("lowerarm"): return "forearm"
    if b.startswith(("upperarm", "clavicle")): return "upperarm"
    if b.startswith(("thigh",)): return "thigh"
    if b.startswith(("calf",)): return "calf"
    if b.startswith(("foot", "ball")): return "foot"
    return "torso"


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.data.objects if o.type == "MESH")
me = mesh.data
src_tris = sum(len(p.vertices) - 2 for p in me.polygons)

# ---- texture ---------------------------------------------------------------------------------------------------------
img = bpy.data.images.load(SRC_TEX)
img.scale(RES, RES)
px = np.empty(RES * RES * 4, dtype=np.float32)
img.pixels.foreach_get(px)
rgb = px.reshape(RES, RES, 4)[:, :, :3]  # Blender images: row 0 = bottom (v = 0), values are sRGB-encoded 0..1

# ---- per-vertex body part ------------------------------------------------------------------------------------------
group_part = {g.index: PARTS.index(part_of(g.name)) for g in mesh.vertex_groups}
vpart = np.zeros(len(me.vertices), dtype=np.int32)
for v in me.vertices:
    best, w = None, -1.0
    for ge in v.groups:
        if ge.weight > w and ge.group in group_part: best, w = group_part[ge.group], ge.weight
    vpart[v.index] = best if best is not None else PARTS.index("torso")

# ---- rasterise parts into UV space --------------------------------------------------------------------------------
label = np.full((RES, RES), -1, dtype=np.int32)
pos = np.zeros((RES, RES, 3), dtype=np.float32)             # source-space surface position under each texel
vpos = np.array([tuple(mesh.matrix_world @ vv.co) for vv in me.vertices], dtype=np.float64)
z_min, z_max = vpos[:, 2].min(), vpos[:, 2].max()
me.calc_loop_triangles()
uv = me.uv_layers.active.data
for tri in me.loop_triangles:
    pts = np.array([uv[l].uv for l in tri.loops], dtype=np.float64) * RES
    labs = [vpart[i] for i in tri.vertices]
    x0, y0 = np.floor(pts.min(0)).astype(int); x1, y1 = np.ceil(pts.max(0)).astype(int)
    x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, RES - 1), min(y1, RES - 1)
    if x1 < x0 or y1 < y0: continue
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = pts
    det = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(det) < 1e-12: continue
    l0 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / det
    l1 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / det
    l2 = 1.0 - l0 - l1
    inside = (l0 >= -0.02) & (l1 >= -0.02) & (l2 >= -0.02)
    if not inside.any(): continue
    nearest = np.argmax(np.stack([l0, l1, l2]), axis=0)
    lab = np.choose(nearest, labs)
    vp = vpos[list(tri.vertices)]
    yy, xx = np.nonzero(inside)
    label[yy + y0, xx + x0] = lab[yy, xx]
    pos[yy + y0, xx + x0] = (l0[yy, xx, None] * vp[0] + l1[yy, xx, None] * vp[1] + l2[yy, xx, None] * vp[2])

# fill gutters with the nearest label (so bilinear sampling at island borders stays in the right zone)
for _ in range(12):
    empty = label < 0
    if not empty.any(): break
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        shifted = np.roll(np.roll(label, dy, 0), dx, 1)
        take = empty & (shifted >= 0)
        label[take] = shifted[take]
        pos[take] = np.roll(np.roll(pos, dy, 0), dx, 1)[take]
        empty = label < 0
label[label < 0] = PARTS.index("torso")
zmap = (pos[:, :, 2] - z_min) / max(1e-6, z_max - z_min)    # 0 = soles, 1 = hat top

# ---- HSV classification -------------------------------------------------------------------------------------------
r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
v = mx
s = np.where(mx > 1e-6, (mx - mn) / np.maximum(mx, 1e-6), 0.0)
d = np.maximum(mx - mn, 1e-6)
h = np.where(mx == r, ((g - b) / d) % 6.0, np.where(mx == g, (b - r) / d + 2.0, (r - g) / d + 4.0)) * 60.0
h = np.where(mx - mn < 1e-6, 0.0, h)

blue = (h >= 185) & (h <= 250) & (s > 0.15)
navy = (h >= 180) & (h <= 260) & (v < 0.45)
white = (s < 0.3) & (v > 0.72)
red = ((h < 12) | (h > 340)) & (s > 0.45) & (v > 0.3)
straw = (h >= 29) & (h <= 60) & (s > 0.4) & (v > 0.4)
skin = ((h < 29) | (h > 345)) & (s > 0.28) & (v > 0.35) & ~red
dark = v < 0.32
hairish = (h >= 0) & (h <= 45) & (s > 0.25) & (v < 0.45)

P = {n: label == i for i, n in enumerate(PARTS)}
R = np.zeros((RES, RES), np.float32); G = np.zeros_like(R); B = np.zeros_like(R); A = np.zeros_like(R)
hat = np.zeros_like(R); band = np.zeros_like(R); hair = np.zeros_like(R)


def erode(m, n=1):
    for _ in range(n):
        m = m & np.roll(m, 1, 0) & np.roll(m, -1, 0) & np.roll(m, 1, 1) & np.roll(m, -1, 1)
    return m


def dilate(m, n=1):
    for _ in range(n):
        m = m | np.roll(m, 1, 0) | np.roll(m, -1, 0) | np.roll(m, 1, 1) | np.roll(m, -1, 1)
    return m


head = P["head"]
# ---- face: eyes from the sclera, then the face front is skin-only -------------------------------------------------
head_bone = next(b for b in arm.data.bones if b.name.lower().startswith("head"))
hb = arm.matrix_world @ head_bone.head_local                      # top of the neck
sclera = head & white & (pos[:, :, 1] < hb.y)                      # Blender: -Y = the character's front
face = {"found": False}
face_front = np.zeros_like(head)
eyes = []
for side in (1.0, -1.0):
    eye_m = sclera & ((pos[:, :, 0] - hb.x) * side > 0.0)
    if eye_m.sum() < 40: continue
    pts = pos[eye_m]
    eye_c = np.median(pts, axis=0)
    eyes.append((eye_c, float(np.percentile(np.linalg.norm(pts - eye_c, axis=1), 90))))
if len(eyes) == 2:
    (c1, r1), (c2, r2) = eyes
    er = max(r1, r2)
    ec = (c1 + c2) / 2.0
    half = abs(c1[0] - c2[0]) / 2.0
    rel = pos - ec
    # face front: in front of the eyes' depth (minus a margin), between the chin and the brows, inside the cheeks
    face_front = head & (rel[:, :, 1] < 1.6 * er) & (rel[:, :, 2] < 2.4 * er) & (rel[:, :, 2] > -4.6 * er) & (np.abs(rel[:, :, 0]) < half + 2.2 * er)
    near_eye = np.zeros_like(head)
    for eye_c, _ in eyes: near_eye |= head & (np.linalg.norm(pos - eye_c, axis=2) < 1.2 * er)
    face = {"found": True, "eye_radius": round(er, 4), "eye_centres": [[round(float(x), 4) for x in eye_c] for eye_c, _ in eyes]}
else:
    print("[farmer_hero] WARNING: eyes not found; face protection off")
    near_eye = np.zeros_like(head)

hat_px = head & straw & ~red
band_px = head & red & ((pos[:, :, 2] - (ec[2] if len(eyes) == 2 else z_min)) > (1.0 * er if len(eyes) == 2 else 0.0))  # above the brows: not the lips
band[band_px] = 1
hat[hat_px] = 1
skin_px = skin & ~hairish
# Off the face: hair is the dark brown paint (speckles removed); on the face only skin-coloured texels take the skin
# tint, everything else (eyes, brows, mouth line, nostrils) is protected.
hair_px = head & hairish & ~straw & ~red & (v < 0.42) & (v > 0.12) & ~face_front & ~near_eye
hair_px = dilate(erode(hair_px, 1), 1) & hair_px
hair[hair_px] = 1
# the shirt collar and strap tops are skinned to the neck: below the mouth, white is shirt and blue is overalls
below_mouth = head & ((pos[:, :, 2] - (ec[2] if len(eyes) == 2 else z_max)) < (-2.0 * er if len(eyes) == 2 else 0.0)) & ~near_eye
collar_px = below_mouth & white
straps_px = below_mouth & (blue | navy)
G[collar_px] = 1
R[straps_px] = 1
features = face_front & ~skin_px & ~hat_px & ~band_px & ~collar_px & ~straps_px  # no dilation: lid/socket skin takes the tint
A[head & skin_px & ~features & ~hat_px] = 1
face["protected_pct"] = round(float((head & features).mean()) * 100.0, 3)

B[P["hand"]] = 1                                           # big work gloves
fa = P["forearm"]
B[fa & straw] = 1; A[fa & skin] = 1; G[fa & white] = 1; R[fa & blue] = 1
ua = P["upperarm"]
G[ua & white] = 1; A[ua & skin] = 1; R[ua & (blue | navy)] = 1
tor = P["torso"]
R[tor & (blue | navy)] = 1; G[tor & white] = 1; A[tor & skin] = 1
legs = P["thigh"] | P["calf"] | P["foot"]
BOOT_TOP = 0.155                                           # rubber boots reach mid-shin (fraction of total height)
B[legs & (zmap < BOOT_TOP)] = 1
R[legs & (zmap >= BOOT_TOP) & ~white] = 1
chest_hair = tor & hairish & ~blue & ~navy & (v < 0.42)    # painted chest hair at the collar follows the hair colour
hair[dilate(erode(chest_hair, 1), 1) & chest_hair] = 1

for zone in (R, G, B, A): zone[hair > 0] = 0
# a texel belongs to one zone only
for lo, hi_list in ((R, (G, B, A)), (G, (B, A)), (B, (A,))):
    for hi in hi_list: hi[lo > 0] = 0


def lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


L = np.stack([lin(r), lin(g), lin(b)], axis=2)
lum = 0.299 * L[:, :, 0] + 0.587 * L[:, :, 1] + 0.114 * L[:, :, 2]
stats = {"source": os.path.basename(SRC), "source_tris": int(src_tris), "height_m": HEIGHT, "zones": {}, "face": face}
for name, m in (("overalls_R", R), ("shirt_G", G), ("bootsgloves_B", B), ("skin_A", A), ("hat", hat), ("band", band), ("hair", hair)):
    cov = float(m.mean())
    sel = m > 0
    # the shader's identity reference: tint == this colour reproduces the painted texture exactly
    mean_rgb = [round(float(x), 5) for x in (L[sel].mean(axis=0) if sel.any() else (0.0, 0.0, 0.0))]
    stats["zones"][name] = {"coverage_pct": round(cov * 100.0, 2), "mean_linear_lum": round(float(lum[sel].mean()) if sel.any() else 0.0, 4),
                            "mean_linear_rgb": mean_rgb}

# soften zone borders by one texel so the tint blends like the paint does
def blur(a):
    return (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.0


def save(name, chans):
    out = bpy.data.images.new(name, RES, RES, alpha=True, float_buffer=False)
    out.colorspace_settings.name = "Non-Color"
    arr = np.stack(chans, axis=2).astype(np.float32).reshape(-1)
    out.pixels.foreach_set(arr)
    out.filepath_raw = os.path.join(OUT, name + ".png")
    out.file_format = "PNG"
    out.save()


os.makedirs(OUT, exist_ok=True)
if DEBUG:
    os.makedirs(DEBUG, exist_ok=True)
    dbg = rgb * 0.25
    for m, col in ((R, (0.2, 0.4, 1)), (G, (0.2, 1, 0.3)), (B, (1, 0.3, 1)), (A, (1, 0.75, 0.5)), (hat, (1, 1, 0)), (band, (1, 0, 0)), (hair, (0.5, 0.25, 0))):
        dbg[m > 0] = col
    dbg[(head & features)] = rgb[(head & features)]                # protected face features keep their paint
    dbg[sclera] = (0, 1, 1)
    out_dbg = bpy.data.images.new("dbg", RES, RES, alpha=True, float_buffer=False)
    out_dbg.pixels.foreach_set(np.concatenate([dbg, np.ones((RES, RES, 1))], 2).astype(np.float32).reshape(-1))
    out_dbg.filepath_raw = os.path.join(DEBUG, "classes.png"); out_dbg.file_format = "PNG"; out_dbg.save()
save(NAME + "_Mask", [blur(R), blur(G), blur(B), blur(A)])
# hair: feathered and capped so light colours blend into the painted strokes instead of sitting on them as blobs
save(NAME + "_Regions", [blur(hat), blur(band), np.minimum(blur(blur(blur(hair))), 0.85), np.ones_like(R)])
img.filepath_raw = os.path.join(OUT, NAME + "_BaseColor.png")
img.file_format = "PNG"
img.save()

# ---- scale, decimate, pattern UVs ---------------------------------------------------------------------------------
ws = [mesh.matrix_world @ vv.co for vv in me.vertices]
h_src = max(w.z for w in ws) - min(w.z for w in ws)
k = HEIGHT / h_src
bpy.ops.object.select_all(action="DESELECT")
arm.scale = (arm.scale[0] * k, arm.scale[1] * k, arm.scale[2] * k)
arm.select_set(True); mesh.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

def stress_pose(on):
    """Arms forward and bent, legs stepping, spine and neck bent: the joints the clips bend hardest."""
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = (1, 0, 0, 0)
    if on:
        for name, axis, deg in (("upperarm_l", (1, 0, 0), 75), ("upperarm_r", (1, 0, 0), 75), ("lowerarm_l", (1, 0, 0), 85),
                                ("lowerarm_r", (1, 0, 0), 85), ("clavicle_l", (0, 0, 1), 15), ("clavicle_r", (0, 0, 1), -15),
                                ("thigh_l", (1, 0, 0), 55), ("calf_l", (1, 0, 0), -75), ("spine_02", (1, 0, 0), 20), ("neck_01", (1, 0, 0), 25),
                                ("hand_l", (0, 0, 1), 35), ("hand_r", (0, 0, 1), -35)):
            pb = arm.pose.bones.get(name)
            if pb is not None: pb.rotation_quaternion = Quaternion(axis, math.radians(deg))
    bpy.context.view_layer.update()


def posed(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    m = ev.to_mesh()
    co = [obj.matrix_world @ v.co for v in m.vertices]
    ev.to_mesh_clear()
    return co


def deform_error(dec, src):
    """mm between each decimated vertex and the source surface point it sits on, both skinned into the stress pose."""
    src.data.calc_loop_triangles()
    tris = [tuple(t.vertices) for t in src.data.loop_triangles]
    rest_src = [src.matrix_world @ v.co for v in src.data.vertices]
    bvh = BVHTree.FromPolygons(rest_src, tris)
    rest_dec = [dec.matrix_world @ v.co for v in dec.data.vertices]
    stress_pose(True)
    p_src, p_dec = posed(src), posed(dec)
    stress_pose(False)
    err = []
    for i, co in enumerate(rest_dec):
        loc, _, ti, _ = bvh.find_nearest(co)
        if loc is None: continue
        a, b, c = tris[ti]
        target = barycentric_transform(loc, rest_src[a], rest_src[b], rest_src[c], p_src[a], p_src[b], p_src[c])
        err.append((p_dec[i] - target).length)
    err = np.array(err) * 1000.0
    return {"mean_mm": round(float(err.mean()), 2), "p99_mm": round(float(np.percentile(err, 99)), 2), "max_mm": round(float(err.max()), 2)}


def influences(obj):
    n = [len([g for g in v.groups if g.weight > 1e-4]) for v in obj.data.vertices]
    return int(max(n))


deform = {}
if src_tris > TARGET_TRIS:
    src_obj = mesh.copy(); src_obj.data = mesh.data.copy(); bpy.context.collection.objects.link(src_obj)
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
    mod = mesh.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"; mod.ratio = TARGET_TRIS / src_tris; mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_move_to_index(modifier="dec", index=0)
    bpy.ops.object.modifier_apply(modifier="dec")
    deform["collapse_weights"] = deform_error(mesh, src_obj)
    deform["collapse_weights"]["max_influences"] = influences(mesh)
    # Re-transfer the weights from the untouched source surface (edge collapse averages weights across joints),
    # then the 4-influence limit Unity skins with, normalised.
    dt = mesh.modifiers.new("weights", "DATA_TRANSFER")
    dt.object = src_obj
    dt.use_vert_data = True
    dt.data_types_verts = {"VGROUP_WEIGHTS"}
    dt.vert_mapping = "POLYINTERP_NEAREST"
    dt.layers_vgroup_select_src = "ALL"
    dt.layers_vgroup_select_dst = "NAME"
    bpy.ops.object.modifier_move_to_index(modifier="weights", index=0)
    bpy.ops.object.modifier_apply(modifier="weights")
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    deform["transferred_weights"] = deform_error(mesh, src_obj)
    deform["transferred_weights"]["max_influences"] = influences(mesh)
    bpy.data.objects.remove(src_obj, do_unlink=True)
me = mesh.data
out_tris = sum(len(p.vertices) - 2 for p in me.polygons)

puv = me.uv_layers.new(name="PatternUV")
bm = bmesh.new(); bm.from_mesh(me)
layer = bm.loops.layers.uv["PatternUV"]
mw = mesh.matrix_world
for f in bm.faces:
    n = (mw.to_3x3() @ f.normal)
    ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
    for loop in f.loops:
        p = mw @ loop.vert.co
        # Blender: z up, -y = character front. U across the body, V up the body (metres).
        loop[layer].uv = (p.y, p.z) if ax >= ay and ax >= az else ((p.x, p.y) if az >= ay else (p.x, p.z))
bm.to_mesh(me); bm.free()
me.uv_layers.active_index = 0
for i, l in enumerate(me.uv_layers): l.active_render = (i == 0)

mesh.name = NAME
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True); mesh.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
    add_leaf_bones=False, bake_anim=False, path_mode="STRIP", mesh_smooth_type="OFF", use_armature_deform_only=False)

stats["output_tris"] = int(out_tris)
stats["deform"] = deform
stats["scale_applied"] = round(k, 4)
with open(os.path.join(OUT, NAME + "_stats.json"), "w") as f:
    json.dump(stats, f, indent=2)
print("[farmer_hero] " + json.dumps(stats))
