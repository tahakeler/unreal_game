"""Headless Blender preview of the hero farmer recolour (the same maths as FarmerRecolour.shader), for checking the mask
before it goes into Unity.

Renders the exported SK_Farmer_Hero.fbx with: the painted texture, the default colourway (every zone tinted with its
own reference colour, which must reproduce the texture exactly), a mask view and the given colourways; full body +
face close-up per variant, tiled into <out.png>. Prints the default colourway's max texel difference.

Usage:
  Blender -b --factory-startup --python tools/tripo/farmer_hero_preview.py -- <hero_dir> <out.png> [prim,sec,acc,skin,hair,band ...]
  (each colourway = six #rrggbb values joined by commas)
"""
import bpy, json, os, sys
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
D, OUT = argv[0], argv[1]
WAYS = [w.split(",") for w in argv[2:]] or [
    "#F08A24,#F7F7F2,#3A3A40,#5A3725,#6B4A2E,#F08A24".split(","),
    "#3FAE5A,#F5E663,#24345E,#F6D7C3,#B5562B,#3FAE5A".split(","),
    "#A77BE0,#9FE0C0,#5B2A6E,#9E6A47,#F5E663,#A77BE0".split(","),
    "#3A3A40,#E04FA0,#F7F7F2,#EBC0A0,#F7F7F2,#F5D142".split(","),
    "#F7F7F2,#24345E,#9C4A3A,#3E261A,#3A3A40,#2EC4C4".split(","),
]
N = "SK_Farmer_Hero"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(D, N + ".fbx"))
mesh = next(o for o in bpy.data.objects if o.type == "MESH")
stats = json.load(open(os.path.join(D, N + "_stats.json")))


def load(n):
    im = bpy.data.images.load(os.path.join(D, f"{N}_{n}.png")); im.colorspace_settings.name = "Non-Color"
    p = np.empty(len(im.pixels), np.float32); im.pixels.foreach_get(p); return p.reshape(im.size[1], im.size[0], 4)


base = load("BaseColor")[:, :, :3]; mask = load("Mask"); reg = load("Regions")
lin = lambda c: np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
srgb = lambda c: np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1 / 2.4) - 0.055)
hx = lambda s: lin(np.array([int(s[i:i + 2], 16) / 255 for i in (1, 3, 5)]))
L = lin(base); lum = (L * [0.299, 0.587, 0.114]).sum(2, keepdims=True)
Z = stats["zones"]
ZONES = [("overalls_R", mask[:, :, 0:1]), ("shirt_G", mask[:, :, 1:2]), ("bootsgloves_B", mask[:, :, 2:3]), ("skin_A", mask[:, :, 3:4]),
         ("hair", reg[:, :, 2:3]), ("band", reg[:, :, 1:2])]


def recolour(tints):
    """FarmerRecolour.shader: c = a + m * s * (tint - ref), s = clamp(lum / refLum, 0, 1.6)."""
    c = L.copy()
    for (zone, m), t in zip(ZONES, tints):
        ref = np.array(Z[zone]["mean_linear_rgb"]); s = np.clip(lum / max(Z[zone]["mean_linear_lum"], 0.02), 0, 1.6)
        c = c + m * s * (t - ref)
    return srgb(np.clip(c, 0, 1))


def tex_image(name, rgb):
    im = bpy.data.images.new(name, rgb.shape[1], rgb.shape[0]); im.colorspace_settings.name = "sRGB"
    im.pixels.foreach_set(np.concatenate([rgb, np.ones(rgb.shape[:2] + (1,))], 2).astype(np.float32).reshape(-1)); return im


mat = bpy.data.materials.new("m"); mat.use_nodes = True
nt = mat.node_tree; tn = nt.nodes.new("ShaderNodeTexImage"); bsdf = nt.nodes["Principled BSDF"]
nt.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"]); bsdf.inputs["Roughness"].default_value = 0.8
mesh.data.materials.clear(); mesh.data.materials.append(mat)
sc = bpy.context.scene; sc.render.engine = "BLENDER_EEVEE"; sc.render.resolution_x = 420; sc.render.resolution_y = 560
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.93, 0.89, 0.8, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun); sun.rotation_euler = (0.8, 0.2, -0.5); sun.data.energy = 3
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"
zs = [(mesh.matrix_world @ v.co).z for v in mesh.data.vertices]; top = max(zs)
eye = stats.get("face", {}).get("eye_centres")
k = stats.get("scale_applied", 1.0)
face_z = (sum(e[2] for e in eye) / 2 * k) if eye else top * 0.8
views = [("body", Vector((0, 0, top * 0.5)), top * 1.12, Vector((0.45, -3, 0.15))), ("face", Vector((0, 0, face_z - 0.02)), 0.62, Vector((0.25, -3, 0.05)))]
tiles = []


def render(tag):
    row = []
    for vn, t, scale, off in views:
        cam.data.ortho_scale = scale; cam.location = t + off; cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(os.path.dirname(OUT), f"_prev_{tag}_{vn}.png"); sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(path); p = np.empty(len(im.pixels), np.float32); im.pixels.foreach_get(p)
        row.append(p.reshape(im.size[1], im.size[0], 4)); bpy.data.images.remove(im); os.remove(path)
    tiles.append(np.concatenate(row, axis=1))


refs = [np.array(Z[z]["mean_linear_rgb"]) for z, _ in ZONES]
default = recolour(refs)
print("[preview] default colourway max |diff| vs texture (0-255):", float(np.abs(default - base).max() * 255))
variants = [("texture", base), ("default", default),
            ("mask", np.clip(mask[:, :, :3] + mask[:, :, 3:4] * np.array([1, 0.75, 0.5]) + reg[:, :, 0:1] * np.array([0.6, 0.5, 0]) + reg[:, :, 2:3] * np.array([0.5, 0.25, 0]), 0, 1))]
variants += [(f"way{i}", recolour([hx(c) for c in way])) for i, way in enumerate(WAYS)]
for tag, rgb in variants:
    tn.image = tex_image(tag, rgb.astype(np.float32)); render(tag)
cols = 4
rows = [np.concatenate(tiles[i:i + cols] + [np.ones_like(tiles[0])] * (cols - len(tiles[i:i + cols])), axis=1) for i in range(0, len(tiles), cols)]
grid = np.concatenate(rows[::-1], axis=0)  # Blender rows are bottom-up
im = bpy.data.images.new("grid", grid.shape[1], grid.shape[0]); im.pixels.foreach_set(grid.astype(np.float32).reshape(-1))
im.filepath_raw = OUT; im.file_format = "PNG"; im.save()
print("[preview] wrote", OUT)
