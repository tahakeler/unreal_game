"""Headless Blender pipeline for the owner's rigged Tripo hero farmer (HARVEST PANIC player character).

Input : the Tripo FBX (UE-mannequin style rig: pelvis, spine_01..03, clavicle, upperarm, lowerarm, hand, thigh, calf,
        foot, ball, neck_01, head + fingers; one mesh, one material) and its basecolor texture.
Output (into <out_dir>, normally HarvestPanic/Assets/ThirdParty/Tripo/SK_Farmer_Hero/):
  SK_Farmer_Hero.fbx              scaled to <height> m, decimated to ~<tris> triangles (weights + UVs kept),
                                  UV0 = Tripo atlas, UV1 "PatternUV" = box projection in metres (outfit patterns)
  SK_Farmer_Hero_BaseColor.png    2048 copy of the 8192 basecolor
  SK_Farmer_Hero_Mask.png         RGBA recolour mask: R overalls, G shirt, B boots + gloves, A skin (0 = fixed colour)
  SK_Farmer_Hero_Regions.png      R straw hat, G hat band, B hair + brows (baked-hat hiding, band/hair tints)
  SK_Farmer_Hero_stats.json       zone coverage %, mean linear luminance per zone (shader reference), tris, height

Segmentation = body part (dominant bone of each triangle, rasterised into UV space) + HSV of the texel, because the
straw hat and the gloves share the same hue and only differ by where they are on the body.
The source files are only read; originals stay untouched.

Usage:
  Blender -b --factory-startup --python tools/tripo/farmer_hero.py -- <src.fbx> <src_basecolor> <out_dir> [height=1.75] [tris=10000]
"""
import bpy, bmesh, colorsys, json, math, os, sys
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, SRC_TEX, OUT = argv[0], argv[1], argv[2]
HEIGHT = float(argv[3]) if len(argv) > 3 else 1.75
TARGET_TRIS = int(argv[4]) if len(argv) > 4 else 10000
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
zmap = np.full((RES, RES), 0.5, dtype=np.float32)           # source-space height of the surface under each texel
vz = np.array([(mesh.matrix_world @ vv.co).z for vv in me.vertices])
z_min, z_max = vz.min(), vz.max()
vz = (vz - z_min) / max(1e-6, z_max - z_min)                # 0 = soles, 1 = hat top
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
    zs = [vz[i] for i in tri.vertices]
    zi = l0 * zs[0] + l1 * zs[1] + l2 * zs[2]
    yy, xx = np.nonzero(inside)
    label[yy + y0, xx + x0] = lab[yy, xx]
    zmap[yy + y0, xx + x0] = zi[yy, xx]

# fill gutters with the nearest label (so bilinear sampling at island borders stays in the right zone)
for _ in range(12):
    empty = label < 0
    if not empty.any(): break
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        shifted = np.roll(np.roll(label, dy, 0), dx, 1)
        take = empty & (shifted >= 0)
        label[take] = shifted[take]
        zmap[take] = np.roll(np.roll(zmap, dy, 0), dx, 1)[take]
        empty = label < 0
label[label < 0] = PARTS.index("torso")

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

head = P["head"]
band[head & red] = 1
hat[head & straw & ~red] = 1
hair[head & hairish & ~straw & ~(v < 0.16)] = 1          # brows and sideburns; pupils (near black) stay fixed
A[head & skin & ~hairish] = 1                              # face, ears, neck

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
hair[tor & hairish & ~blue & ~navy] = 1                    # painted chest hair at the collar follows the hair colour

for zone in (R, G, A): zone[hair > 0] = 0
# a texel belongs to one zone only
for lo, hi_list in ((R, (G, B, A)), (G, (B, A)), (B, (A,))):
    for hi in hi_list: hi[lo > 0] = 0


def lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


lum = 0.299 * lin(r) + 0.587 * lin(g) + 0.114 * lin(b)
stats = {"source": os.path.basename(SRC), "source_tris": int(src_tris), "height_m": HEIGHT, "zones": {}}
for name, m in (("overalls_R", R), ("shirt_G", G), ("bootsgloves_B", B), ("skin_A", A), ("hat", hat), ("band", band), ("hair", hair)):
    cov = float(m.mean())
    stats["zones"][name] = {"coverage_pct": round(cov * 100.0, 2), "mean_linear_lum": round(float(lum[m > 0].mean()) if (m > 0).any() else 0.0, 4)}

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
save(NAME + "_Mask", [blur(R), blur(G), blur(B), blur(A)])
save(NAME + "_Regions", [blur(hat), blur(band), blur(hair), np.ones_like(R)])
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

if src_tris > TARGET_TRIS:
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
    mod = mesh.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"; mod.ratio = TARGET_TRIS / src_tris; mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_move_to_index(modifier="dec", index=0)
    bpy.ops.object.modifier_apply(modifier="dec")
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
stats["scale_applied"] = round(k, 4)
with open(os.path.join(OUT, NAME + "_stats.json"), "w") as f:
    json.dump(stats, f, indent=2)
print("[farmer_hero] " + json.dumps(stats))
