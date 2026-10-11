"""HARVEST PANIC - import Tripo-generated GLBs into the project (headless).

Run:
  /Applications/UE_5.8/Engine/Binaries/Mac/UnrealEditor-Cmd \
    "<repo>/HarvestPanic/HarvestPanic.uproject" -run=pythonscript \
    -script="<repo>/HarvestPanic/Scripts/AssetGen/import_generated.py" -unattended -nullrhi -nosplash

Env (optional):
  HP_TRIPO_ONLY=SK_Pig_Hamlet,SM_FeedBucket   restrict to ids
  HP_TRIPO_ACCEPTED=1                           only import ids with "qa": "accept" in state.json

Reads tools/tripo/batch01.json (manifest) and tools/tripo/out/<id>/<id>.glb.
Writes /Game/HarvestPanic/Generated/<Category>/<id>/.

Notes:
- Everything is imported as StaticMesh (Tripo GLB is unrigged). Rigged FBX from
  the rig step is a separate, manual import into a shared skeleton.
- Pivot is applied with an import transform: mesh centred on X/Y, bottom at Z=0
  ("base centre"/"ground"); anything else (handle apex, hinge, grip, knot) is
  flagged in the summary as PIVOT-MANUAL.
"""
import json
import os

import unreal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MANIFEST = os.path.join(ROOT, "tools", "tripo", "batch01.json")
STATE = os.path.join(ROOT, "tools", "tripo", "state.json")
OUT = os.path.join(ROOT, "tools", "tripo", "out")
DEST = "/Game/HarvestPanic/Generated"
MASTER_MI = "/Game/HarvestPanic/Materials/M_HP_Prop"  # used only if it exists


def log(msg):
    unreal.log("[TripoImport] " + msg)


def import_glb(path, dest):
    task = unreal.AssetImportTask()
    task.filename = path
    task.destination_path = dest
    task.automated = True
    task.replace_existing = True
    task.save = False
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    meshes = []
    for p in task.imported_object_paths:
        a = unreal.load_asset(p)
        if isinstance(a, unreal.StaticMesh):
            meshes.append(a)
    return meshes


def bounds_cm(mesh):
    bb = mesh.get_bounding_box()
    return bb.min, bb.max


def rescale_and_pivot(mesh, target_cm, pivot):
    """Bake scale + base-centre pivot into LOD0 via build settings scale and
    a positional offset applied through the mesh description."""
    mn, mx = bounds_cm(mesh)
    size = max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z)
    if size <= 0:
        return 1.0, "empty"
    factor = (target_cm / size) if target_cm else 1.0
    sub = unreal.get_editor_subsystem(unreal.StaticMeshEditorSubsystem)
    desc = mesh.get_static_mesh_description(0)
    centre = unreal.Vector((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z)
    xf = unreal.Transform(location=centre * -1.0)
    desc.transform_mesh(xf) if hasattr(desc, "transform_mesh") else None
    if hasattr(desc, "transform_mesh"):
        desc.transform_mesh(unreal.Transform(scale=unreal.Vector(factor, factor, factor)))
        mesh.build_from_static_mesh_descriptions([desc])
        note = "ok"
    else:  # fallback: build-settings scale only
        bs = sub.get_lod_build_settings(mesh, 0)
        bs.build_scale3d = unreal.Vector(factor, factor, factor)
        sub.set_lod_build_settings(mesh, 0, bs)
        note = "scale-only"
    p = pivot.lower()
    if not any(k in p for k in ("base", "ground", "feet", "between feet")):
        note += " PIVOT-MANUAL(%s)" % pivot[:30]
    return factor, note


def collision(mesh, kind):
    sub = unreal.get_editor_subsystem(unreal.StaticMeshEditorSubsystem)
    sub.remove_collisions(mesh)
    if kind == "none":
        return "none"
    shape = {"box": unreal.ScriptCollisionShapeType.BOX,
             "sphere": unreal.ScriptCollisionShapeType.SPHERE,
             "capsule": unreal.ScriptCollisionShapeType.CAPSULE}.get(kind)
    if shape is not None:
        sub.add_simple_collisions(mesh, shape)
        return kind
    # convex / ucx_custom -> auto convex (UCX must be hand-authored later)
    sub.set_convex_decomposition_collisions(mesh, 10, 8, 100000)
    return "convex" if kind == "convex" else "convex(UCX-TODO)"


def sockets(mesh, names):
    mn, mx = bounds_cm(mesh)
    cx, cy, cz = (mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2
    places = {"head": (mx.x * 0.8, cy, mx.z), "carry_N": (mx.x, cy, cz), "carry_S": (mn.x, cy, cz),
              "carry_E": (cx, mx.y, cz), "carry_W": (cx, mn.y, cz)}
    have = {s.socket_name for s in mesh.get_editor_property("sockets") or []}
    added = 0
    for n in names:
        if unreal.Name(n) in have:
            continue
        s = unreal.StaticMeshSocket(mesh)
        s.socket_name = n
        x, y, z = places.get(n, (cx, cy, mx.z))
        s.relative_location = unreal.Vector(x, y, z)
        mesh.add_socket(s)
        added += 1
    return added


def main():
    man = json.load(open(MANIFEST))
    st = json.load(open(STATE)) if os.path.exists(STATE) else {"items": {}}
    only = [s for s in os.environ.get("HP_TRIPO_ONLY", "").split(",") if s]
    need_accept = os.environ.get("HP_TRIPO_ACCEPTED") == "1"
    rows = []
    for it in man["items"]:
        iid = it["id"]
        glb = os.path.join(OUT, iid, iid + ".glb")
        if (only and iid not in only) or not os.path.exists(glb):
            continue
        if need_accept and st["items"].get(iid, {}).get("qa") != "accept":
            rows.append((iid, "SKIP(qa)", "", "", "", ""))
            continue
        dest = "%s/%s/%s" % (DEST, it["category"], iid)
        meshes = import_glb(glb, dest)
        if not meshes:
            rows.append((iid, "IMPORT-FAIL", "", "", "", ""))
            continue
        mesh = meshes[0]
        target = (it.get("scale_cm") or {}).get("max_dim_cm")
        factor, pnote = rescale_and_pivot(mesh, target, it.get("pivot", ""))
        col = collision(mesh, it["collision"])
        nsock = sockets(mesh, it.get("sockets", []))
        if it.get("mass_kg"):
            # StaticMesh has no mass; store for the carry BP (physics-carry owner).
            unreal.EditorAssetLibrary.set_metadata_tag(mesh, "HP_MassKg", str(it["mass_kg"]))
        rec = st["items"].get(iid, {})
        unreal.EditorAssetLibrary.set_metadata_tag(mesh, "TripoTaskId", rec.get("model_task_id", ""))
        unreal.EditorAssetLibrary.set_metadata_tag(mesh, "TripoPrompt", it["prompt"][-200:])
        if unreal.EditorAssetLibrary.does_asset_exist(MASTER_MI):
            mi = unreal.load_asset(MASTER_MI)
            for i in range(len(mesh.static_materials)):
                if not mesh.get_material(i):
                    mesh.set_material(i, mi)
        mn, mx = bounds_cm(mesh)
        dims = "%.0fx%.0fx%.0f" % (mx.x - mn.x, mx.y - mn.y, mx.z - mn.z)
        unreal.EditorAssetLibrary.save_directory(dest, only_if_is_dirty=False, recursive=True)
        ok = unreal.EditorAssetLibrary.load_asset(mesh.get_path_name()) is not None
        rows.append((iid, "OK" if ok else "LOAD-FAIL", dims, "%.3f %s" % (factor, pnote), col,
                     "%d sock, %s kg" % (nsock, it.get("mass_kg"))))
    log("%-20s %-10s %-14s %-28s %-16s %s" % ("id", "result", "size_cm", "scale/pivot", "collision", "extra"))
    for r in rows:
        log("%-20s %-10s %-14s %-28s %-16s %s" % r)
    log("imported %d item(s)" % sum(1 for r in rows if r[1] == "OK"))


main()
