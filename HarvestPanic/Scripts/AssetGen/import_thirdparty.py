"""HARVEST PANIC - headless import of free (CC0) third-party GLBs.

Run (content-only sandbox project or the game project, never a window):
  HP_FREE_REPO=<repo root> [HP_FREE_ONLY=SK_Pig,SM_Crate] \
  UnrealEditor-Cmd <project>.uproject -run=pythonscript \
    -script=<repo>/HarvestPanic/Scripts/AssetGen/import_thirdparty.py -unattended -nullrhi -nosplash
Reads tools/freeassets/manifest.json (+ spec rows from tools/tripo/batch01.json) and
tools/freeassets/downloads/<pack>/<file>.glb. Writes /Game/HarvestPanic/ThirdParty/<Pack>/<id>/.
Two passes per item: import at scale 1 to measure, then re-import with Interchange
import_offset_uniform_scale (and base-centre offset for statics) so the asset is spec size.
Writes a verification report to tools/freeassets/import_report.json.
"""
import json
import math
import os

import unreal

REPO = os.environ.get("HP_FREE_REPO") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MAN = os.path.join(REPO, "tools", "freeassets", "manifest.json")
SPEC = os.path.join(REPO, "tools", "tripo", "batch01.json")
DL = os.path.join(REPO, "tools", "freeassets", "downloads")
ROOT = "/Game/HarvestPanic/ThirdParty"
TMP = "/Game/_HPImportTmp"
EAL = unreal.EditorAssetLibrary


def log(m):
    unreal.log_warning("[FreeImport] " + m)  # warning level so it shows in commandlet output


def make_pipeline(name, scale, offset, static, combine=True):
    path = "%s/P_%s" % (TMP, name)
    if EAL.does_asset_exist(path):
        EAL.delete_asset(path)
    p = EAL.duplicate_loaded_asset(unreal.load_asset("/Interchange/Pipelines/DefaultAssetsPipeline"), path)
    p.set_editor_property("import_offset_uniform_scale", scale)
    p.set_editor_property("import_offset_translation", offset)
    mp = p.get_editor_property("mesh_pipeline")
    if static and combine:
        enum = unreal.InterchangeCombineStaticMeshesBehavior
        for n in ("COMBINE_ALL", "ALL", "COMBINE_ALL_MESHES", "COMBINE"):
            if hasattr(enum, n):
                mp.set_editor_property("combine_static_meshes_behavior", getattr(enum, n))
                break
    mp.set_editor_property("build_nanite", False)
    EAL.save_loaded_asset(p)
    return p


def do_import(glb, dest, pipeline):
    if EAL.does_directory_exist(dest):
        EAL.delete_directory(dest)
    mgr = unreal.InterchangeManager.get_interchange_manager_scripted()
    src = unreal.InterchangeManager.create_source_data(glb)
    prm = unreal.ImportAssetParameters()
    prm.is_automated = True
    prm.replace_existing = True
    prm.override_pipelines = [unreal.SoftObjectPath(pipeline.get_path_name())]
    mgr.import_asset(dest, src, prm)
    out = {}
    for a in EAL.list_assets(dest, recursive=True, include_folder=False):
        o = EAL.load_asset(a)
        out.setdefault(o.get_class().get_name(), []).append(o)
    return out


def bbox(o):
    if isinstance(o, unreal.StaticMesh):
        b = o.get_bounding_box()
    else:
        b = o.get_imported_bounds() if hasattr(o, "get_imported_bounds") else o.get_bounds().box
        if not isinstance(b, unreal.Box):
            ext = o.get_bounds()
            return (ext.origin - ext.box_extent), (ext.origin + ext.box_extent)
    return b.min, b.max


def dims(mn, mx):
    return [round(mx.x - mn.x, 1), round(mx.y - mn.y, 1), round(mx.z - mn.z, 1)]


def tris(o):
    try:
        if isinstance(o, unreal.StaticMesh):
            return unreal.get_editor_subsystem(unreal.StaticMeshEditorSubsystem).get_number_triangles(o, 0) if hasattr(unreal.StaticMeshEditorSubsystem, "get_number_triangles") else o.get_num_triangles(0)
        return unreal.get_editor_subsystem(unreal.SkeletalMeshEditorSubsystem).get_num_verts(o, 0)
    except Exception as e:  # noqa
        return "n/a"


def fit_size(d, fit):
    if fit == "height":
        return d[2]
    if fit == "length":
        return max(d[0], d[1])
    return max(d)


def smlib():
    # StaticMeshEditorSubsystem is not instantiated under -run=pythonscript; fall back to the library.
    return unreal.get_editor_subsystem(unreal.StaticMeshEditorSubsystem) or unreal.EditorStaticMeshLibrary


def static_post(m, it):
    sub = smlib()
    sub.remove_collisions(m)
    kind = it.get("collision", "box")
    shapes = {"box": "BOX", "sphere": "SPHERE", "capsule": "CAPSULE"}
    if kind in shapes:
        sub.add_simple_collisions(m, getattr(unreal.ScriptCollisionShapeType, shapes[kind]))
    elif kind != "none":
        sub.set_convex_decomposition_collisions(m, 8, 8, 100000)
    mn, mx = bbox(m)
    cx, cy, cz = (mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2
    places = {"carry_N": (mx.x, cy, cz), "carry_S": (mn.x, cy, cz), "carry_E": (cx, mx.y, cz), "carry_W": (cx, mn.y, cz),
              "grip": (cx, cy, mx.z), "hand_grip": (cx, cy, mx.z), "latch": (mx.x, cy, cz), "sell_zone": (mx.x, cy, 0),
              "hoop_center": (mx.x - (mx.z - mn.z) / 2, cy, cz)}
    if it["id"] == "SM_Net":
        places["grip"] = (mn.x + 30, cy, cz)
    for n in it.get("sockets", []):
        if n.startswith("#"):
            continue
        s = unreal.StaticMeshSocket(m)
        x, y, z = places.get(n, (cx, cy, mx.z))
        s.set_editor_property("socket_name", n)
        s.set_editor_property("relative_location", unreal.Vector(x, y, z))
        m.add_socket(s)
    return kind


def skel_sockets(sk, it):
    """SkeletalMeshSocket.socket_name is read-only from Python in UE 5.8, so sockets are
    not authored here. Gameplay attaches to the bone named in it['head_bone'] directly
    (or a socket is added once by hand in the Skeleton editor). Recorded in metadata."""
    unreal.EditorAssetLibrary.set_metadata_tag(sk, "HP_HeadBone", it["head_bone"])
    return []


def main():
    man = json.load(open(MAN))
    spec = {i["id"]: i for i in json.load(open(SPEC))["items"]}
    only = [s for s in os.environ.get("HP_FREE_ONLY", "").split(",") if s]
    report = []
    for it in man["items"]:
        if only and it["id"] not in only:
            continue
        sp = spec.get(it.get("spec") or "", {})
        it.setdefault("collision", sp.get("collision", "box"))
        it.setdefault("sockets", sp.get("sockets", []))
        it.setdefault("mass_kg", sp.get("mass_kg"))
        glb = os.path.join(DL, it["glb"])
        dest = "%s/%s/%s" % (ROOT, it["pack"], it["id"])
        static = it["kind"] == "static"
        cls = "StaticMesh" if static else "SkeletalMesh"
        got = do_import(glb, dest, make_pipeline(it["id"] + "_m", 1.0, unreal.Vector(0, 0, 0), static, it.get("combine", True)))
        if cls not in got:
            report.append({"id": it["id"], "result": "IMPORT-FAIL", "classes": list(got)})
            continue
        mn, mx = bbox(got[cls][0])
        d = dims(mn, mx)
        f = 1.0 if it["fit"] == "none" else it["target_cm"] / max(fit_size(d, it["fit"]), 1e-3)
        off = unreal.Vector(-(mn.x + mx.x) / 2 * f, -(mn.y + mx.y) / 2 * f, -mn.z * f) if static else unreal.Vector(0, 0, 0)
        got = do_import(glb, dest, make_pipeline(it["id"], f, off, static, it.get("combine", True)))
        mesh = got[cls][0]
        if it["fit"] != "none" and abs(f - 1.0) > 1e-3:
            # Interchange applies the uniform scale more than linearly to some skinned
            # GLBs (observed: size ~ f^2 for Quaternius rigs). Measure the exponent and solve.
            mn2, mx2 = bbox(mesh)
            s0, s1 = fit_size(d, it["fit"]), fit_size(dims(mn2, mx2), it["fit"])
            pexp = math.log(s1 / s0) / math.log(f)
            if abs(pexp - 1.0) > 0.05:
                f = (it["target_cm"] / s0) ** (1.0 / pexp)
                off = unreal.Vector(-(mn.x + mx.x) / 2 * f ** pexp, -(mn.y + mx.y) / 2 * f ** pexp, -mn.z * f ** pexp) if static else off
                log("%s scale exponent %.2f -> re-import with %.4f" % (it["id"], pexp, f))
                got = do_import(glb, dest, make_pipeline(it["id"], f, off, static, it.get("combine", True)))
                mesh = got[cls][0]
        if static and len(got[cls]) > 1:
            log("WARN %s produced %d static meshes" % (it["id"], len(got[cls])))
        EAL.rename_asset(mesh.get_path_name(), "%s/%s" % (dest, it["id"]))
        rec = {"want_sockets": [n for n in it.get("sockets", []) if not n.startswith("#")], "id": it["id"], "path": "%s/%s" % (dest, it["id"]), "scale": round(f, 4), "src": it["src"]}
        if static:
            rec["collision"] = static_post(mesh, it)
        else:
            rec["sockets"] = skel_sockets(mesh, it)
        if it.get("mass_kg"):
            EAL.set_metadata_tag(mesh, "HP_MassKg", str(it["mass_kg"]))
        EAL.set_metadata_tag(mesh, "HP_Source", it["src"])
        EAL.set_metadata_tag(mesh, "HP_Licence", "CC0 1.0")
        EAL.save_directory(dest, only_if_is_dirty=False, recursive=True)
        report.append(rec)
    EAL.delete_directory(TMP)
    verify(report)
    path = os.path.join(REPO, "tools", "freeassets", "import_report.json")
    json.dump(report, open(path, "w"), indent=1)
    log("wrote " + path)


def verify(report):
    """Reload everything from disk and record metadata (bounds cm, tris, bones, anims)."""
    for r in report:
        if "path" not in r:
            continue
        m = EAL.load_asset(r["path"])
        r["loaded"] = m is not None
        if not m:
            continue
        mn, mx = bbox(m)
        r["bounds_cm"] = dims(mn, mx)
        if isinstance(m, unreal.StaticMesh):
            r["tris"] = m.get_num_triangles(0) if hasattr(m, "get_num_triangles") else "n/a"
            r["sockets"] = ["%s@(%.0f,%.0f,%.0f)" % (n, sk.relative_location.x, sk.relative_location.y, sk.relative_location.z)
                            for n, sk in ((n, m.find_socket(n)) for n in r.get("want_sockets", [])) if sk]
            r["materials"] = len(m.static_materials)
        else:
            skel = m.get_editor_property("skeleton")
            r["skeleton"] = skel.get_path_name()
            try:
                names = [str(b) for b in skel.get_reference_pose().get_bone_names()]
                r["bones"] = len(names)
                r["bone_names"] = names
            except Exception:
                r["bones"] = "n/a"
            try:
                r["socket_list"] = ["%s@%s" % (sk.get_editor_property("socket_name"), sk.get_editor_property("bone_name")) for sk in (m.get_socket_by_index(i) for i in range(m.num_sockets()))]
            except Exception as e:  # noqa
                r["socket_list"] = "err %s" % e
            r["verts"] = tris(m)
            folder = r["path"].rsplit("/", 1)[0]
            anims = []
            for a in EAL.list_assets(folder, recursive=True, include_folder=False):
                o = EAL.load_asset(a)
                if isinstance(o, unreal.AnimSequence):
                    anims.append({"path": o.get_path_name().split(".")[0], "len_s": round(o.get_play_length(), 2)})
            r["anims"] = anims
            r["materials"] = len(m.get_editor_property("materials"))
        log(json.dumps(r))


main()
