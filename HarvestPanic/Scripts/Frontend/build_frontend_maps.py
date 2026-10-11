"""Builds /Game/HarvestPanic/Frontend/Lvl_MainMenu and Lvl_Lobby from Emberlight meshes.

Run headless:
  UnrealEditor-Cmd <abs .uproject> -run=pythonscript -script=<abs this file> -unattended -nullrhi
Idempotent: both maps are recreated from scratch every run.
"""
import unreal

EL = "/Game/Emberlight/Meshes"
OUT = "/Game/HarvestPanic/Frontend"
GROUND_MATERIALS = ["/Game/Emberlight/Materials/Background/MI_BackgroundTerrain", "/Game/Emberlight/Materials/Landscape/MI_Landscape"]
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)


def log(msg):
    unreal.log("HPFrontendMaps: " + msg)


def mesh(path):
    m = unreal.load_asset(path)
    if not m:
        unreal.log_warning("HPFrontendMaps: missing mesh " + path)
    return m


def place(path, loc, yaw=0.0, scale=1.0, label=None, collide=True):
    m = mesh(path)
    if not m:
        return None
    a = eas.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(*loc), unreal.Rotator(0.0, 0.0, yaw))
    comp = a.static_mesh_component
    comp.set_static_mesh(m)
    a.set_actor_scale3d(unreal.Vector(scale, scale, scale))
    if not collide:
        comp.set_collision_enabled(unreal.CollisionEnabled.NO_COLLISION)
    if label:
        a.set_actor_label(label)
    return a


def bounds_report(paths):
    for p in paths:
        m = mesh(p)
        if m:
            b = m.get_bounds()
            log("%s extent=%s" % (p.split("/")[-1], b.box_extent))


def lighting(sun_pitch=-28.0, sun_yaw=-130.0, warm=True):
    sun = eas.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 2000), unreal.Rotator(0.0, sun_pitch, sun_yaw))
    lc = sun.get_component_by_class(unreal.DirectionalLightComponent)
    lc.set_intensity(7.0)
    lc.set_light_color(unreal.LinearColor(1.0, 0.82, 0.62, 1.0) if warm else unreal.LinearColor(1, 1, 1, 1))
    lc.set_editor_property("atmosphere_sun_light", True)
    eas.spawn_actor_from_class(unreal.SkyAtmosphere, unreal.Vector(0, 0, 0))
    sky = eas.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 500))
    sc = sky.get_component_by_class(unreal.SkyLightComponent)
    sc.set_editor_property("real_time_capture", True)
    sc.set_intensity(1.2)
    fog = eas.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector(0, 0, 0))
    fc = fog.get_component_by_class(unreal.ExponentialHeightFogComponent)
    fc.set_fog_density(0.02)
    fc.set_fog_inscattering_color(unreal.LinearColor(0.9, 0.62, 0.38, 1.0))
    eas.spawn_actor_from_class(unreal.VolumetricCloud, unreal.Vector(0, 0, 0))
    ppv = eas.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0))
    ppv.set_editor_property("unbound", True)
    s = ppv.settings
    s.set_editor_property("override_auto_exposure_method", True)
    s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True)
    s.set_editor_property("auto_exposure_bias", 10.5)
    s.set_editor_property("override_color_saturation", True)
    s.set_editor_property("color_saturation", unreal.Vector4(1.12, 1.08, 1.0, 1.0))
    s.set_editor_property("override_vignette_intensity", True)
    s.set_editor_property("vignette_intensity", 0.45)
    ppv.set_editor_property("settings", s)


def ground(size_m=120, z=0.0):
    a = place("/Engine/BasicShapes/Plane", (0, 0, z), scale=1.0, label="Ground")
    a.set_actor_scale3d(unreal.Vector(size_m, size_m, 1))
    mat = None
    for cand in GROUND_MATERIALS:
        if unreal.EditorAssetLibrary.does_asset_exist(cand):
            mat = unreal.load_asset(cand)
            log("ground material " + cand)
            break
    if mat:
        a.static_mesh_component.set_material(0, mat)
    return a


def set_gamemode(cls_path):
    world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    ws = world.get_world_settings()
    cls = unreal.load_class(None, cls_path)
    ws.set_editor_property("default_game_mode", cls)
    log("GameMode override = %s" % cls_path)


def new_map(name):
    path = "%s/%s" % (OUT, name)
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        # Delete via the asset registry, falling back to removing the file.
        if not unreal.EditorAssetLibrary.delete_asset(path):
            import os
            f = os.path.join(unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_content_dir()), "HarvestPanic/Frontend/%s.umap" % name)
            if os.path.exists(f):
                os.remove(f)
    if not les.new_level(path):
        raise RuntimeError("could not create " + path)
    return path


def fence_line(start, end, step=420.0, yaw=0.0):
    import math
    dx, dy = end[0] - start[0], end[1] - start[1]
    n = max(1, int(math.hypot(dx, dy) // step))
    for i in range(n + 1):
        t = i / float(n)
        place(EL + "/Fences/SM_WoodenFenceLong", (start[0] + dx * t, start[1] + dy * t, 0), yaw=yaw)


def wheat_patch(cx, cy, cols, rows, gap=110.0):
    for i in range(cols):
        for j in range(rows):
            place(EL + "/Foliage/SM_WheatCard02_Combined", (cx + i * gap, cy + j * gap, 0), yaw=(i * 37 + j * 53) % 360, collide=False)


def grass_scatter(cx, cy, radius, count, seed=7):
    import random
    rnd = random.Random(seed)
    for _ in range(count):
        x = cx + rnd.uniform(-radius, radius)
        y = cy + rnd.uniform(-radius, radius)
        place(EL + "/Foliage/SM_GrassCard03_Moss", (x, y, 0), yaw=rnd.uniform(0, 360), scale=rnd.uniform(0.9, 1.6), collide=False)


def build_main_menu():
    path = new_map("Lvl_MainMenu")
    lighting(sun_pitch=-14.0, sun_yaw=-150.0)
    ground(160)
    place("/Game/Emberlight/Meshes/Background/SM_BackgroundTerrain01", (6000, 0, -40), scale=1.0, label="Hills", collide=False)
    # Barn right of frame (title column sits on the left), windmill behind.
    place(EL + "/Structures/SM_Barn", (3600, 1500, 0), yaw=210, label="Barn")
    grass_scatter(2200, 0, 1800, 260)
    place(EL + "/Structures/SM_Windmill", (4200, -900, 0), yaw=160, label="Windmill")
    place(EL + "/Structures/SM_Windmill_Rotate", (4200, -900, 0), yaw=160, label="WindmillBlades", collide=False)
    place(EL + "/Props/SM_Scarecrow", (1300, -350, 0), yaw=200)
    place(EL + "/Props/SM_HayBalesCylinder", (1500, 380, 0), yaw=20)
    place(EL + "/Props/SM_HayBalesRectangular", (1350, 560, 0), yaw=70)
    place(EL + "/Props/SM_Crate", (1180, 300, 0), yaw=10)
    place(EL + "/Props/SM_Harvest_Basket", (1120, 200, 0), yaw=40)
    place(EL + "/Props/SM_Pitchfork", (1250, 650, 0), yaw=80)
    place(EL + "/Props/SM_Coop", (2600, -700, 0), yaw=150)
    place(EL + "/Lighting/SM_LanternOutdoor", (1800, 520, 0), yaw=0)
    for i, (x, y) in enumerate([(3200, -2200), (3600, 2600), (5200, 1400), (2600, -3000), (5600, -2600)]):
        place(EL + "/Trees/SM_Tree01", (x, y, 0), yaw=i * 70, scale=1.0 + 0.15 * (i % 3))
    fence_line((900, -1400), (900, -300), yaw=90)
    fence_line((900, -1400), (2600, -1400), yaw=0)
    wheat_patch(1100, -1300, 12, 8)
    for i in range(8):
        place(EL + "/Foliage/SM_FlowerGroup", (700 + i * 180, 900 + (i % 3) * 140, 0), yaw=i * 45, collide=False)
        place(EL + "/Foliage/SM_Bush03", (1500 + i * 300, 1500 + (i % 2) * 200, 0), yaw=i * 60, collide=False)
    ps = eas.spawn_actor_from_class(unreal.PlayerStart, unreal.Vector(-200, -150, 260), unreal.Rotator(0.0, -6.0, 8.0))
    ps.set_actor_label("MenuCameraStart")
    set_gamemode("/Script/HarvestPanic.HPMenuGameMode")
    les.save_current_level()
    log("saved " + path)


def build_lobby():
    path = new_map("Lvl_Lobby")
    lighting(sun_pitch=-35.0, sun_yaw=-120.0)
    ground(80)
    # Lobby Yard ~40 x 40 m in front of the barn porch.
    place(EL + "/Structures/SM_Barn", (1900, 0, 0), yaw=180, label="Barn")
    place(EL + "/Structures/SM_Windmill", (2600, -2200, 0), yaw=140, label="Windmill")
    place(EL + "/Structures/SM_Windmill_Rotate", (2600, -2200, 0), yaw=140, collide=False)
    place(EL + "/Structures/SM_Gate14", (-2000, 0, 0), yaw=90, label="Gate")
    fence_line((-2000, -2000), (2000, -2000), yaw=0)
    fence_line((-2000, 2000), (2000, 2000), yaw=0)
    fence_line((-2000, -2000), (-2000, -500), yaw=90)
    fence_line((-2000, 500), (-2000, 2000), yaw=90)
    for i, (x, y) in enumerate([(-400, -900), (-200, -1100), (100, -950), (-500, 900), (-250, 1150), (150, 1000)]):
        place(EL + "/Props/SM_HayBalesRectangular", (x, y, 0), yaw=i * 30, label="HayBale%d" % i)
    place(EL + "/Props/SM_HayBalesCylinder", (600, -1500, 0), yaw=10)
    place(EL + "/Props/SM_Scarecrow", (-1200, 1400, 0), yaw=30)
    place(EL + "/Props/SM_Coop", (-900, -1500, 0), yaw=60)
    place(EL + "/Props/SM_Crate", (900, 700, 0), yaw=15)
    place(EL + "/Props/SM_Crate02", (980, 820, 0), yaw=40)
    place(EL + "/Props/SM_Chair01", (1100, -400, 0), yaw=200)
    place(EL + "/Lighting/SM_LanternChained", (1150, 400, 0), yaw=0)
    for i in range(10):
        place(EL + "/Foliage/SM_FlowerGroup02", (-1800 + i * 380, 1850, 0), yaw=i * 33, collide=False)
        place(EL + "/Foliage/SM_FlowerGroup", (-1800 + i * 380, -1850, 0), yaw=i * 41, collide=False)
    for i, (x, y) in enumerate([(-3200, -2600), (-3400, 2400), (3600, 2600), (-2800, 0)]):
        place(EL + "/Trees/SM_Tree01", (x, y, 0), yaw=i * 80)
    # Porch ready zone in front of the barn doors, dinner bell beside it.
    porch_cls = unreal.load_class(None, "/Script/HarvestPanic.HPPorchReadyZone")
    porch = eas.spawn_actor_from_class(porch_cls, unreal.Vector(1050, 0, 150), unreal.Rotator(0, 0, 0))
    porch.set_actor_label("PorchReadyZone")
    place(EL + "/Props/SM_EmptySack", (1050, -250, 0), yaw=0, collide=False)
    bell_cls = unreal.load_class(None, "/Script/HarvestPanic.HPDinnerBell")
    bell = eas.spawn_actor_from_class(bell_cls, unreal.Vector(900, 650, 0), unreal.Rotator(0, 0, 180))
    bell.set_actor_label("DinnerBell")
    for i in range(4):
        ps = eas.spawn_actor_from_class(unreal.PlayerStart, unreal.Vector(-900, -450 + i * 300, 120), unreal.Rotator(0, 0, 0))
        ps.set_actor_label("LobbyStart%d" % i)
    set_gamemode("/Script/HarvestPanic.HPLobbyGameMode")
    les.save_current_level()
    log("saved " + path)


bounds_report([EL + "/Structures/SM_Barn", EL + "/Structures/SM_Windmill", EL + "/Fences/SM_WoodenFenceLong",
               EL + "/Trees/SM_Tree01", EL + "/Props/SM_BellPost", EL + "/Props/SM_HayBalesRectangular",
               "/Game/Emberlight/Meshes/Background/SM_BackgroundTerrain01"])
build_main_menu()
build_lobby()
log("done")
