"""Generate a simple stand-in catching net (CC0, made in-house) as GLB.
Usage: python3 -I make_net.py out.glb   (units: metres, +X forward along handle, Z up)
Parts: wooden handle (cylinder 1.0 m), hoop (torus Ø0.5 m), bag (closed cone, opaque cream)."""
import json, math, struct, sys

def cyl(r, x0, x1, n=8):
    v, i = [], []
    for k in range(n):
        a = 2*math.pi*k/n; y, z = r*math.cos(a), r*math.sin(a)
        v += [(x0, y, z), (x1, y, z)]
    for k in range(n):
        a, b = 2*k, 2*((k+1) % n)
        i += [a, b, a+1, b, b+1, a+1]
    c0, c1 = len(v), len(v)+1; v += [(x0, 0, 0), (x1, 0, 0)]
    for k in range(n):
        a, b = 2*k, 2*((k+1) % n); i += [c0, a, b, c1, b+1, a+1]
    return v, i

def torus(R, r, cx, n=24, m=6):
    v, i = [], []
    for a in range(n):
        A = 2*math.pi*a/n
        for b in range(m):
            B = 2*math.pi*b/m; d = R + r*math.cos(B)
            v.append((cx + d*math.cos(A), d*math.sin(A), r*math.sin(B)))
    for a in range(n):
        for b in range(m):
            p = a*m+b; q = ((a+1) % n)*m+b; p2 = a*m+(b+1) % m; q2 = ((a+1) % n)*m+(b+1) % m
            i += [p, q, p2, q, q2, p2]
    return v, i

def cone(R, cx, depth, n=24):
    # open bag hanging below hoop plane (hoop lies in XY plane), apex at -Z
    v = [(cx + R*math.cos(2*math.pi*k/n), R*math.sin(2*math.pi*k/n), 0) for k in range(n)]
    v.append((cx, 0, -depth)); ap = n; i = []
    for k in range(n):
        i += [k, (k+1) % n, ap, (k+1) % n, k, ap]  # double-sided
    return v, i

parts = [("Wood", [0.55, 0.38, 0.22, 1], cyl(0.02, 0.0, 1.0)),
         ("Wood", [0.55, 0.38, 0.22, 1], torus(0.25, 0.02, 1.25)),
         ("Rope", [0.96, 0.92, 0.80, 1], cone(0.24, 1.25, 0.45))]
buf = b""; acc = []; views = []; meshes_prims = []; mats = {}
for name, col, (v, idx) in parts:
    if name not in mats: mats[name] = (len(mats), col)
    vb = b"".join(struct.pack("<3f", *p) for p in v); ib = b"".join(struct.pack("<I", k) for k in idx)
    for data, tgt in ((vb, 34962), (ib, 34963)):
        while len(buf) % 4: buf += b"\0"
        views.append({"buffer": 0, "byteOffset": len(buf), "byteLength": len(data), "target": tgt}); buf += data
    mn = [min(p[a] for p in v) for a in range(3)]; mx = [max(p[a] for p in v) for a in range(3)]
    acc.append({"bufferView": len(views)-2, "componentType": 5126, "count": len(v), "type": "VEC3", "min": mn, "max": mx})
    acc.append({"bufferView": len(views)-1, "componentType": 5125, "count": len(idx), "type": "SCALAR"})
    meshes_prims.append({"attributes": {"POSITION": len(acc)-2}, "indices": len(acc)-1, "material": mats[name][0]})
# glTF is Y-up: rotate so our Z-up becomes glTF Y-up (-90 deg about X)
g = {"asset": {"version": "2.0", "generator": "HarvestPanic make_net.py (CC0)"},
     "scene": 0, "scenes": [{"nodes": [0]}],
     "nodes": [{"name": "SM_Net", "mesh": 0, "rotation": [-0.7071068, 0, 0, 0.7071068]}],
     "meshes": [{"name": "SM_Net", "primitives": meshes_prims}],
     "materials": [{"name": n, "pbrMetallicRoughness": {"baseColorFactor": c, "metallicFactor": 0, "roughnessFactor": 0.9}, "doubleSided": True}
                   for n, (k, c) in sorted(mats.items(), key=lambda x: x[1][0])],
     "accessors": acc, "bufferViews": views, "buffers": [{"byteLength": len(buf)}]}
js = json.dumps(g).encode(); js += b" " * ((4 - len(js) % 4) % 4)
while len(buf) % 4: buf += b"\0"
out = b"glTF" + struct.pack("<II", 2, 12+8+len(js)+8+len(buf)) + struct.pack("<I4s", len(js), b"JSON") + js + struct.pack("<I4s", len(buf), b"BIN\0") + buf
open(sys.argv[1], "wb").write(out)
