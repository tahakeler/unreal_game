"""Reload Lvl_Muddlebury headless and log marker counts per HP_* tag + sanity checks.
Run like build_muddlebury.py (-run=pythonscript -script=<abs path> -unattended -nullrhi)."""
import collections
import unreal

EXPECT = {"HP_PlayerStart": 4, "HP_MarketPen": 1, "HP_CritterSpawner_Pig:3": 1, "HP_CritterSpawner_Chicken:8": 1,
          "HP_Goods_PrizePumpkin": 1, "HP_Goods_Crate": 4, "HP_BridgePlank": 20, "HP_River": 1, "HP_RiverPile": 1,
          "HP_BarnRespawn": 1, "HP_Wardrobe": 1}


def out(m):
    unreal.log_warning("[verify_muddlebury] " + m)


les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
les.load_level("/Game/HarvestPanic/Maps/Lvl_Muddlebury")
actors = unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors()
tags = collections.Counter()
classes = collections.Counter()
for a in actors:
    classes[a.get_class().get_name()] += 1
    for t in a.tags:
        tags[str(t)] += 1
out("total actors %d" % len(actors))
ok = True
for t, n in sorted(tags.items()):
    if t.startswith("HP_") and not t.startswith("HP_BridgePlank_") and not t.startswith("HP_PlayerStart_"):
        exp = EXPECT.get(t)
        flag = "" if exp is None or exp == n else "  <-- expected %d" % exp
        out("tag %-30s %d%s" % (t, n, flag))
for t, n in EXPECT.items():
    if tags.get(t, 0) != n:
        ok = False
        if t not in tags:
            out("tag %-30s MISSING" % t)
for c in ("PlayerStart", "NavMeshBoundsVolume", "BlockingVolume", "DirectionalLight", "SkyAtmosphere", "SkyLight",
          "ExponentialHeightFog", "PostProcessVolume", "RecastNavMesh", "NiagaraActor", "StaticMeshActor"):
    out("class %-24s %d" % (c, classes.get(c, 0)))
for a in actors:
    if a.get_class().get_name() in ("NavMeshBoundsVolume", "BlockingVolume"):
        o, e = a.get_actor_bounds(False)
        out("bounds %-24s center=(%.0f,%.0f,%.0f) ext=(%.0f,%.0f,%.0f)" % (a.get_actor_label(), o.x, o.y, o.z, e.x, e.y, e.z))
# bridge geometry
planks = sorted([a for a in actors if "HP_BridgePlank" in [str(t) for t in a.tags]], key=lambda a: a.get_actor_location().x)
if planks:
    xs = [p.get_actor_bounds(False) for p in planks]
    out("bridge span x %.0f..%.0f (%.1f m), plank mobility %s" % (xs[0][0].x - xs[0][1].x, xs[-1][0].x + xs[-1][1].x,
        ((xs[-1][0].x + xs[-1][1].x) - (xs[0][0].x - xs[0][1].x)) / 100.0, planks[0].static_mesh_component.mobility))
pen = [a for a in actors if "HP_MarketPen" in [str(t) for t in a.tags]]
barn = [a for a in actors if "HP_BarnRespawn" in [str(t) for t in a.tags]]
if pen and barn:
    d = (pen[0].get_actor_location() - barn[0].get_actor_location()).length() / 100.0
    out("barn->market pen distance %.1f m" % d)
out("RESULT %s" % ("PASS" if ok else "FAIL"))
