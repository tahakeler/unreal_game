"""Read-only GLB inspector (parses the JSON chunk only). Usage: python3 -I glb_inspect.py a.glb [...]"""
import json, struct, sys
def load(p):
    b = open(p, "rb").read()
    assert b[:4] == b"glTF"
    ln, typ = struct.unpack_from("<I4s", b, 12)
    return json.loads(b[20:20+ln])
for p in sys.argv[1:]:
    g = load(p); acc = g.get("accessors", [])
    tris = 0; lo=[1e9]*3; hi=[-1e9]*3
    for m in g.get("meshes", []):
        for pr in m["primitives"]:
            a = acc[pr["indices"]]["count"] if "indices" in pr else acc[pr["attributes"]["POSITION"]]["count"]
            tris += a // 3
            pa = acc[pr["attributes"]["POSITION"]]
            if "min" in pa:
                lo = [min(x,y) for x,y in zip(lo,pa["min"])]; hi=[max(x,y) for x,y in zip(hi,pa["max"])]
    nodes = g.get("nodes", [])
    skins = g.get("skins", [])
    joints = [nodes[j].get("name","?") for j in skins[0]["joints"]] if skins else []
    anims = []
    for an in g.get("animations", []):
        t = max((acc[s["input"]].get("max",[0])[0] for s in an["samplers"]), default=0)
        anims.append(f'{an.get("name")}({t:.2f}s)')
    scl = [n.get("scale") for n in nodes if n.get("scale")][:2]
    print(f"== {p.split('/')[-1]} tris={tris} meshbounds(m)={[round(h-l,3) for l,h in zip(lo,hi)]} nodeScales={scl} mats={[m.get('name') for m in g.get('materials',[])]} textures={len(g.get('images',[]))}")
    if joints: print("  bones", len(joints), joints)
    if anims: print("  anims", anims)
