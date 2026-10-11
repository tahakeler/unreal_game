"""
HARVEST PANIC - Region 1 "Muddlebury Farm" level builder.

Headless + reproducible. Rebuilds /Game/HarvestPanic/Maps/Lvl_Muddlebury from scratch
(and the helper materials under /Game/HarvestPanic/Environment) on every run.

Run:
  /Applications/UE_5.8/Engine/Binaries/Mac/UnrealEditor-Cmd <abs>/HarvestPanic.uproject \
      -run=pythonscript -script=<abs>/HarvestPanic/Scripts/LevelBuild/build_muddlebury.py \
      -unattended -nullrhi

Layout spec: design/levels/muddlebury-farm.md
Units: cm. +X = east (toward bridge/market), +Y = north, river flows toward -Y.
Gameplay is NOT placed here: only tagged marker actors (HP_*), resolved at runtime.
This script must never reference HarvestPanic C++ classes.
"""
import math
import random
import unreal

MAP_PATH = "/Game/HarvestPanic/Maps/Lvl_Muddlebury"
ENV = "/Game/HarvestPanic/Environment"
MAT_DIR = ENV + "/Materials"
EL = "/Game/Emberlight"

random.seed(1337)  # deterministic scatter

eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
MEL = unreal.MaterialEditingLibrary
atools = unreal.AssetToolsHelpers.get_asset_tools()
EAL = unreal.EditorAssetLibrary

STATS = {}


def log(msg):
    unreal.log_warning("[build_muddlebury] " + msg)


def load(path):
    a = unreal.load_asset(path)
    if a is None:
        raise RuntimeError("missing asset " + path)
    return a


# ---------------------------------------------------------------- materials
def _sampler_for(tex):
    cs = tex.get_editor_property("compression_settings")
    srgb = tex.get_editor_property("srgb")
    if cs in (unreal.TextureCompressionSettings.TC_GRAYSCALE, unreal.TextureCompressionSettings.TC_ALPHA):
        return unreal.MaterialSamplerType.SAMPLERTYPE_GRAYSCALE if srgb else unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_GRAYSCALE
    if cs == unreal.TextureCompressionSettings.TC_NORMALMAP:
        return unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL
    if cs == unreal.TextureCompressionSettings.TC_MASKS or not srgb:
        return unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR
    return unreal.MaterialSamplerType.SAMPLERTYPE_COLOR


def fresh(path):
    if EAL.does_asset_exist(path):
        EAL.delete_asset(path)


def new_material(name):
    path = MAT_DIR + "/" + name
    fresh(path)
    return atools.create_asset(name, MAT_DIR, unreal.Material, unreal.MaterialFactoryNew())


def expr(mat, cls, x, y):
    return MEL.create_material_expression(mat, cls, x, y)


def scalar(mat, name, val, x, y):
    e = expr(mat, unreal.MaterialExpressionScalarParameter, x, y)
    e.set_editor_property("parameter_name", name)
    e.set_editor_property("default_value", val)
    return e


def vec(mat, name, col, x, y):
    e = expr(mat, unreal.MaterialExpressionVectorParameter, x, y)
    e.set_editor_property("parameter_name", name)
    e.set_editor_property("default_value", unreal.LinearColor(*col))
    return e


def world_uv(mat, scale_param, scale_val, x, y, name_suffix=""):
    wp = expr(mat, unreal.MaterialExpressionWorldPosition, x, y)
    mask = expr(mat, unreal.MaterialExpressionComponentMask, x + 150, y)
    mask.set_editor_property("r", True)
    mask.set_editor_property("g", True)
    MEL.connect_material_expressions(wp, "", mask, "")
    s = scalar(mat, scale_param, scale_val, x + 150, y + 100)
    div = expr(mat, unreal.MaterialExpressionDivide, x + 300, y)
    MEL.connect_material_expressions(mask, "", div, "A")
    MEL.connect_material_expressions(s, "", div, "B")
    return div


def tex_param(mat, name, tex, x, y):
    e = expr(mat, unreal.MaterialExpressionTextureSampleParameter2D, x, y)
    e.set_editor_property("parameter_name", name)
    e.set_editor_property("texture", tex)
    e.set_editor_property("sampler_type", _sampler_for(tex))
    return e


def build_ground_master():
    """M_HP_WorldGround: top-down world-aligned albedo, macro noise tint variation,
    desaturation control (art bible V2: world rests), flat normal (Emberlight style)."""
    m = new_material("M_HP_WorldGround")
    base_tex = load(EL + "/Textures/Landscape/Grass/T_Grass02_Basecolor")
    noise_tex = load(EL + "/Textures/Noise/T_Noise01")
    uv = world_uv(m, "Tiling", 600.0, -1400, -200)
    base = tex_param(m, "BaseTex", base_tex, -1000, -200)
    MEL.connect_material_expressions(uv, "", base, "UVs")
    muv = world_uv(m, "MacroScale", 4000.0, -1400, 200)
    noise = tex_param(m, "MacroNoise", noise_tex, -1000, 200)
    MEL.connect_material_expressions(muv, "", noise, "UVs")
    ta = vec(m, "TintA", (1, 1, 1, 1), -700, 300)
    tb = vec(m, "TintB", (0.85, 0.85, 0.8, 1), -700, 450)
    lerp = expr(m, unreal.MaterialExpressionLinearInterpolate, -450, 300)
    MEL.connect_material_expressions(tb, "", lerp, "A")
    MEL.connect_material_expressions(ta, "", lerp, "B")
    MEL.connect_material_expressions(noise, "R", lerp, "Alpha")
    mul = expr(m, unreal.MaterialExpressionMultiply, -300, -100)
    MEL.connect_material_expressions(base, "RGB", mul, "A")
    MEL.connect_material_expressions(lerp, "", mul, "B")
    des = expr(m, unreal.MaterialExpressionDesaturation, -150, -100)
    dfrac = scalar(m, "Desaturate", 0.1, -300, 50)
    MEL.connect_material_expressions(mul, "", des, "")
    MEL.connect_material_expressions(dfrac, "", des, "Fraction")
    MEL.connect_material_property(des, "", unreal.MaterialProperty.MP_BASE_COLOR)
    r = scalar(m, "Roughness", 0.85, -300, 150)
    MEL.connect_material_property(r, "", unreal.MaterialProperty.MP_ROUGHNESS)
    sp = scalar(m, "Specular", 0.2, -300, 220)
    MEL.connect_material_property(sp, "", unreal.MaterialProperty.MP_SPECULAR)
    MEL.recompile_material(m)
    EAL.save_loaded_asset(m)
    return m


def build_water():
    """M_HP_Water: opaque stylized river (Mac friendly, no translucency sorting / SSR cost).
    Two panning noise layers lerp deep/shallow colour + emissive foam glints."""
    m = new_material("M_HP_Water")
    noise_tex = load(EL + "/Textures/Noise/T_Noise03")
    layers = []
    for i, (scale, sx, sy) in enumerate(((900.0, 0.004, -0.05), (2300.0, -0.01, -0.025))):
        uv = world_uv(m, "FlowScale%d" % i, scale, -1600, -300 + i * 400)
        pan = expr(m, unreal.MaterialExpressionPanner, -1150, -300 + i * 400)
        pan.set_editor_property("speed_x", sx)
        pan.set_editor_property("speed_y", sy)
        MEL.connect_material_expressions(uv, "", pan, "Coordinate")
        t = tex_param(m, "FlowNoise%d" % i, noise_tex, -950, -300 + i * 400)
        MEL.connect_material_expressions(pan, "", t, "UVs")
        layers.append(t)
    mul = expr(m, unreal.MaterialExpressionMultiply, -650, -100)
    MEL.connect_material_expressions(layers[0], "R", mul, "A")
    MEL.connect_material_expressions(layers[1], "R", mul, "B")
    deep = vec(m, "DeepColor", (0.03, 0.16, 0.20, 1), -650, 100)
    shallow = vec(m, "ShallowColor", (0.18, 0.42, 0.42, 1), -650, 250)
    lerp = expr(m, unreal.MaterialExpressionLinearInterpolate, -400, 100)
    MEL.connect_material_expressions(deep, "", lerp, "A")
    MEL.connect_material_expressions(shallow, "", lerp, "B")
    MEL.connect_material_expressions(mul, "", lerp, "Alpha")
    MEL.connect_material_property(lerp, "", unreal.MaterialProperty.MP_BASE_COLOR)
    # foam glints: pow(noise product, k) * foam colour
    pw = expr(m, unreal.MaterialExpressionPower, -400, -250)
    k = scalar(m, "FoamSharpness", 6.0, -600, -300)
    MEL.connect_material_expressions(mul, "", pw, "Base")
    MEL.connect_material_expressions(k, "", pw, "Exp")
    foam = vec(m, "FoamColor", (0.6, 0.65, 0.6, 1), -400, -400)
    fm = expr(m, unreal.MaterialExpressionMultiply, -200, -300)
    MEL.connect_material_expressions(pw, "", fm, "A")
    MEL.connect_material_expressions(foam, "", fm, "B")
    MEL.connect_material_property(fm, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.connect_material_property(scalar(m, "Roughness", 0.12, -200, 300), "", unreal.MaterialProperty.MP_ROUGHNESS)
    MEL.connect_material_property(scalar(m, "Specular", 0.6, -200, 380), "", unreal.MaterialProperty.MP_SPECULAR)
    MEL.recompile_material(m)
    EAL.save_loaded_asset(m)
    return m


def make_mi(name, parent, scalars=None, vectors=None, textures=None):
    path = MAT_DIR + "/" + name
    fresh(path)
    mi = atools.create_asset(name, MAT_DIR, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    MEL.set_material_instance_parent(mi, parent)
    for k, v in (scalars or {}).items():
        MEL.set_material_instance_scalar_parameter_value(mi, k, v)
    for k, v in (vectors or {}).items():
        MEL.set_material_instance_vector_parameter_value(mi, k, unreal.LinearColor(*v))
    for k, v in (textures or {}).items():
        MEL.set_material_instance_texture_parameter_value(mi, k, load(v))
    MEL.update_material_instance(mi)
    EAL.save_loaded_asset(mi)
    return mi


def build_materials():
    EAL.make_directory(MAT_DIR)
    ground = build_ground_master()
    water = build_water()
    T = EL + "/Textures/Landscape/"
    M = {}
    # Region 1 palette (art bible 4.2): ground #A88A5A, foliage #7FA34A, wood #8A6040.
    M["grass"] = make_mi("MI_HP_Grass", ground, {"Tiling": 700.0, "Desaturate": 0.15},
                         {"TintA": (0.95, 1.0, 0.78, 1), "TintB": (0.78, 0.86, 0.6, 1)},
                         {"BaseTex": T + "Grass/T_Grass02_Basecolor"})
    M["soil"] = make_mi("MI_HP_SoilPath", ground, {"Tiling": 350.0, "Desaturate": 0.15},
                        {"TintA": (1.05, 0.92, 0.75, 1), "TintB": (0.9, 0.8, 0.66, 1)},
                        {"BaseTex": T + "Soil/T_Soil_Basecolor"})
    M["mud"] = make_mi("MI_HP_Mud", ground, {"Tiling": 300.0, "Desaturate": 0.1, "Roughness": 0.35, "Specular": 0.5},
                       {"TintA": (0.55, 0.42, 0.3, 1), "TintB": (0.45, 0.34, 0.24, 1)},
                       {"BaseTex": T + "Soil/T_Soil_Basecolor"})
    M["shore"] = make_mi("MI_HP_Shore", ground, {"Tiling": 400.0, "Desaturate": 0.2},
                         {"TintA": (1, 0.95, 0.85, 1), "TintB": (0.85, 0.8, 0.7, 1)},
                         {"BaseTex": T + "ShoreSoil/T_ShoreSoil_Basecolor"})
    M["cobble"] = make_mi("MI_HP_TownCobble", ground, {"Tiling": 450.0, "Desaturate": 0.45},
                          {"TintA": (1.0, 0.92, 0.82, 1), "TintB": (0.85, 0.8, 0.72, 1)},
                          {"BaseTex": T + "Cliff/T_Cliff_Basecolor"})
    M["riverbed"] = make_mi("MI_HP_RiverBed", ground, {"Tiling": 500.0, "Desaturate": 0.3},
                            {"TintA": (0.45, 0.42, 0.35, 1), "TintB": (0.35, 0.33, 0.3, 1)},
                            {"BaseTex": T + "Cliff/T_Cliff_Basecolor"})
    M["water"] = water
    # Brick barn (art bible 4.1: red is reserved for loss; barn -> brick #9C4A3A).
    M["barn_brick"] = make_mi("MI_HP_Barn_Brick", load(EL + "/Materials/Structures/MI_Barn"),
                              {"Default Desaturation": 0.35}, {"Default Tint": (0.86, 0.78, 0.74, 0)})
    M["roof_brick"] = make_mi("MI_HP_Roof_Brick", load(EL + "/Materials/Props/MI_RoofRed"),
                              {"Default Desaturation": 0.3}, {"Default Tint": (0.30, 0.17, 0.13, 0)})
    # Market stall awnings: gold trim is allowed at market (money semantic).
    M["awning_gold"] = make_mi("MI_HP_Awning_Gold", load(EL + "/Materials/Props/MI_RoofRed"),
                               {"Default Hue": 0.0}, {"Default Tint": (0.75, 0.52, 0.12, 0)})
    M["awning_cream"] = make_mi("MI_HP_Awning_Cream", load(EL + "/Materials/Props/MI_Plaster"),
                                {}, {"Default Tint": (0.85, 0.8, 0.68, 0)})
    M["wood"] = load(EL + "/Materials/Props/MI_StandartWood")
    M["plank"] = load(EL + "/Materials/Structures/MI_WoodSeamless")
    M["rope"] = load(EL + "/Materials/Props/MI_RopeCoil")
    M["plaster"] = load(EL + "/Materials/Props/MI_Plaster")
    M["hills"] = make_mi("MI_HP_BackgroundHills", load(EL + "/Materials/Background/MI_BackgroundTerrain"),
                         {"Desaturate": 0.25}, {"Tint": (0.24, 0.24, 0.07, 1)})
    return M


# ---------------------------------------------------------------- actors
MESH_CACHE = {}


def mesh(name):
    if name not in MESH_CACHE:
        if name.startswith("/"):
            MESH_CACHE[name] = load(name)
        else:
            # find under Emberlight/Meshes
            for sub in ("Props", "Structures", "Foliage", "Fences", "Trees", "Rocks", "Lighting", "Fruits",
                        "Background", "Demo/Misc"):
                p = "%s/Meshes/%s/%s" % (EL, sub, name)
                if EAL.does_asset_exist(p):
                    MESH_CACHE[name] = load(p)
                    break
            else:
                raise RuntimeError("mesh not found " + name)
    return MESH_CACHE[name]


CUBE = "/Engine/BasicShapes/Cube"
CYL = "/Engine/BasicShapes/Cylinder"
PLANE = "/Engine/BasicShapes/Plane"


def place(mesh_name, loc, rot=(0, 0, 0), scale=(1, 1, 1), label=None, folder="Props", tags=None,
          mat=None, collision=True, movable=False, shadows=True, hidden_in_game=False):
    """rot = (pitch, yaw, roll)."""
    sm = mesh(mesh_name) if mesh_name else None
    # spawn_actor_from_object (actor factories) does not work inside -run=pythonscript, so spawn by class.
    a = eas.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(*loc),
                                   unreal.Rotator(roll=rot[2], pitch=rot[0], yaw=rot[1]))
    if sm:
        a.static_mesh_component.set_static_mesh(sm)
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    a.set_actor_scale3d(unreal.Vector(*scale))
    comp = a.static_mesh_component
    if movable:
        comp.set_mobility(unreal.ComponentMobility.MOVABLE)
    if mat is not None:
        for i in range(max(1, comp.get_num_materials())):
            comp.set_material(i, mat)
    if not collision:
        comp.set_collision_profile_name("NoCollision")
    if not shadows:
        comp.set_cast_shadow(False)
    if hidden_in_game:
        a.set_actor_hidden_in_game(True)
    if label:
        a.set_actor_label(label)
    a.set_folder_path(folder)
    if tags:
        a.tags = [unreal.Name(t) for t in tags]
    STATS[folder] = STATS.get(folder, 0) + 1
    return a


def marker(tag_list, loc, label, rot=(0, 0, 0), box=None):
    """Invisible gameplay marker (StaticMeshActor). box=(x,y,z) metres -> visible-in-editor cube,
    hidden in game, no collision, scaled to the volume it represents."""
    if box:
        return place(CUBE, loc, rot, (box[0], box[1], box[2]), label, "Markers", tag_list,
                     collision=False, shadows=False, hidden_in_game=True)
    return place(None, loc, rot, (1, 1, 1), label, "Markers", tag_list, collision=False)


def slab(x0, x1, y0, y1, top, mat, label, folder="Ground", thick=100.0):
    """Axis-aligned ground slab whose top face is at z=top."""
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    return place(CUBE, (cx, cy, top - thick / 2.0), (0, 0, 0),
                 ((x1 - x0) / 100.0, (y1 - y0) / 100.0, thick / 100.0), label, folder, mat=mat)


def slope_x(x0, x1, z0, z1, y0, y1, mat, label, thick=100.0):
    """Ramp slab going from (x0,z0) to (x1,z1) along X."""
    dx, dz = x1 - x0, z1 - z0
    length = math.hypot(dx, dz)
    pitch = math.degrees(math.atan2(dz, dx))
    up = (-math.sin(math.radians(pitch)), math.cos(math.radians(pitch)))
    cx = (x0 + x1) / 2.0 - up[0] * thick / 2.0
    cz = (z0 + z1) / 2.0 - up[1] * thick / 2.0
    return place(CUBE, (cx, (y0 + y1) / 2.0, cz), (pitch, 0, 0),
                 ((length + 40) / 100.0, (y1 - y0) / 100.0, thick / 100.0), label, "Ground/River", mat=mat)


def fence_line(p0, p1, folder, gaps=(), mesh_name="SM_WoodenFence", seg=300.0):
    """Fence from p0 to p1; gaps = list of (t0,t1) fractions left open (critter shortcuts/gates)."""
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(dx, dy)
    yaw = math.degrees(math.atan2(dy, dx))
    n = max(1, int(round(L / seg)))
    sx = (L / n) / seg
    for i in range(n):
        t = (i + 0.5) / n
        if any(g0 <= t <= g1 for g0, g1 in gaps):
            continue
        place(mesh_name, (p0[0] + dx * t, p0[1] + dy * t, 0), (0, yaw, 0), (sx, 1, 1), None, folder)


def fence_rect(cx, cy, w, h, folder, gaps=None):
    gaps = gaps or {}
    x0, x1, y0, y1 = cx - w / 2, cx + w / 2, cy - h / 2, cy + h / 2
    fence_line((x0, y0), (x1, y0), folder, gaps.get("S", ()))
    fence_line((x1, y0), (x1, y1), folder, gaps.get("E", ()))
    fence_line((x1, y1), (x0, y1), folder, gaps.get("N", ()))
    fence_line((x0, y1), (x0, y0), folder, gaps.get("W", ()))


def scatter(mesh_names, count, x0, x1, y0, y1, folder, smin=0.8, smax=1.2, avoid=(), z=0.0,
            collision=False, shadows=True, zscale=None):
    placed = 0
    tries = 0
    while placed < count and tries < count * 20:
        tries += 1
        x, y = random.uniform(x0, x1), random.uniform(y0, y1)
        if any(a[0] <= x <= a[1] and a[2] <= y <= a[3] for a in avoid):
            continue
        s = random.uniform(smin, smax)
        sz = s * (zscale if zscale else 1.0)
        place(random.choice(mesh_names), (x, y, z), (0, random.uniform(0, 360), 0), (s, s, sz), None, folder,
              collision=collision, shadows=shadows)
        placed += 1


# ---------------------------------------------------------------- layout constants
FARM_X0, FARM_X1 = -12000.0, 6000.0         # farm ground (west bank top edge at 6000)
TOWN_X0, TOWN_X1 = 10000.0, 17500.0         # market side (east bank top edge at 10000)
MAP_Y0, MAP_Y1 = -14000.0, 9500.0
RIVER_BED_X0, RIVER_BED_X1 = 6800.0, 9200.0
BED_Z = -600.0
WATER_Z = -420.0
BRIDGE_X0, BRIDGE_X1 = 6000.0, 10000.0      # 40 m span
BRIDGE_Y = 0.0
BARN = (-2600.0, 0.0)
PEN_MARKET = (11400.0, 0.0)


def build_ground(M):
    # farm and town plateaus (top z = 0)
    slab(FARM_X0, FARM_X1, MAP_Y0, MAP_Y1, 0, M["grass"], "Ground_Farm")
    slab(TOWN_X0, TOWN_X1, MAP_Y0, MAP_Y1, 0, M["grass"], "Ground_Town")
    # river channel
    slope_x(FARM_X1, RIVER_BED_X0, 0, BED_Z, MAP_Y0, MAP_Y1, M["shore"], "RiverBank_West")
    slope_x(RIVER_BED_X1, TOWN_X0, BED_Z, 0, MAP_Y0, MAP_Y1, M["shore"], "RiverBank_East")
    slab(RIVER_BED_X0 - 50, RIVER_BED_X1 + 50, MAP_Y0, MAP_Y1, BED_Z, M["riverbed"], "RiverBed", "Ground/River")
    w = place(PLANE, ((FARM_X1 + TOWN_X0) / 2, (MAP_Y0 + MAP_Y1) / 2, WATER_Z), (0, 0, 0),
              ((TOWN_X0 - FARM_X1 - 600) / 100.0, (MAP_Y1 - MAP_Y0) / 100.0, 1), "RiverWater", "Ground/River",
              mat=M["water"], collision=False, shadows=False)
    # dirt paths (thin decal-like slabs 2 cm proud of the ground)
    path_top = 2.0
    slab(BARN[0] + 900, BRIDGE_X0, -260, 260, path_top, M["soil"], "Path_BarnToBridge", "Ground/Paths", 10)
    slab(BRIDGE_X1, PEN_MARKET[0] - 300, -260, 260, path_top, M["soil"], "Path_BridgeToMarket", "Ground/Paths", 10)
    slab(600, 900, 260, 2500, path_top, M["soil"], "Path_ToPigPen", "Ground/Paths", 10)
    slab(600, 900, -2900, -260, path_top, M["soil"], "Path_ToCoop", "Ground/Paths", 10)
    slab(-6000, BARN[0] - 900, -200, 200, path_top, M["soil"], "Path_ToWindmill", "Ground/Paths", 10)
    slab(1500, 1800, -6200, -2900, path_top, M["soil"], "Path_ToOrchard", "Ground/Paths", 10)
    slab(4800, 5400, -12500, -260, path_top, M["soil"], "Path_RiverWalk", "Ground/Paths", 10)
    # barn yard
    slab(BARN[0] + 700, BARN[0] + 2400, -1300, 1300, path_top + 0.5, M["soil"], "BarnYard", "Ground/Paths", 10)
    # market square
    slab(10800, 16200, -2800, 2800, path_top, M["cobble"], "MarketSquare", "Ground/Paths", 10)


def build_bridge(M):
    """40 m plank bridge: 20 separate planks (tag HP_BridgePlank), rope rails, end posts."""
    n = 20
    span = BRIDGE_X1 - BRIDGE_X0
    pl = span / n
    sag = 70.0
    end_z = 12.0

    def deck_z(x):
        t = (x - (BRIDGE_X0 + BRIDGE_X1) / 2.0) / (span / 2.0)
        return end_z - sag * (1 - t * t)

    for i in range(n):
        xa, xb = BRIDGE_X0 + i * pl, BRIDGE_X0 + (i + 1) * pl
        xc = (xa + xb) / 2
        pitch = math.degrees(math.atan2(deck_z(xb) - deck_z(xa), pl))
        jitter = random.uniform(-1.2, 1.2)
        place(CUBE, (xc, BRIDGE_Y + random.uniform(-6, 6), deck_z(xc) - 6), (pitch, jitter, random.uniform(-0.8, 0.8)),
              ((pl - 6) / 100.0, 2.4, 0.12), "BridgePlank_%02d" % (i + 1), "Bridge/Planks",
              tags=["HP_BridgePlank", "HP_BridgePlank_%02d" % (i + 1)], mat=M["plank"], movable=True)
    # end posts (2 per side) + abutment logs
    for x in (BRIDGE_X0 - 60, BRIDGE_X1 + 60):
        for y in (-150, 150):
            place("SM_WoodenPillar03", (x, BRIDGE_Y + y, -60), (90, 0, 0), (0.36, 1.6, 1.6), None, "Bridge/Structure",
                  mat=M["wood"])
        place("SM_WoodenPillar03", (x, BRIDGE_Y, 6), (0, 90, 0), (0.55, 1.8, 0.8), None, "Bridge/Structure",
              mat=M["wood"])
        place("SM_RopeCoil", (x + (-120 if x < 8000 else 120), BRIDGE_Y + 200, 0), (0, 30, 0), 1.3, None,
              "Bridge/Structure")
    # rope: hand rails at +100 cm, under-ropes at deck edges, hangers every 2 planks
    def rope(p, q, r=3.0, coll=False):
        dx, dy, dz = q[0] - p[0], q[1] - p[1], q[2] - p[2]
        L = math.sqrt(dx * dx + dy * dy + dz * dz)
        pitch = math.degrees(math.atan2(math.hypot(dx, dy), dz))  # cylinder axis is Z
        yaw = math.degrees(math.atan2(dy, dx))
        place(CYL, ((p[0] + q[0]) / 2, (p[1] + q[1]) / 2, (p[2] + q[2]) / 2), (pitch, yaw, 0),
              (r * 2 / 100.0, r * 2 / 100.0, (L + 2) / 100.0), None, "Bridge/Rope", mat=M["rope"],
              collision=coll, shadows=False)

    for side in (-1, 1):
        y_rail = BRIDGE_Y + side * 135
        y_under = BRIDGE_Y + side * 115
        prev = None
        for i in range(n + 1):
            x = BRIDGE_X0 + i * pl
            rail = (x, y_rail, deck_z(x) + 100 + (25 if i in (0, n) else 0))
            under = (x, y_under, deck_z(x) - 14)
            if prev:
                rope(prev[0], rail, 3.5, coll=True)
                rope(prev[1], under, 2.5)
            if i % 2 == 0:
                rope(under, rail, 1.5)
            prev = (rail, under)
    # toll sign (Landlord twist "Bridge Toll")
    place("SM_WoodenSignpost", (BRIDGE_X0 - 350, BRIDGE_Y - 420, 0), (0, 200, 0), 1.4, "Sign_BridgeToll", "Bridge/Structure")
    place("SM_OilLantern", (BRIDGE_X0 - 60, BRIDGE_Y - 150, 230), (0, 0, 0), 1.0, None, "Bridge/Structure")
    place("SM_OilLantern", (BRIDGE_X1 + 60, BRIDGE_Y + 150, 230), (0, 0, 0), 1.0, None, "Bridge/Structure")


def build_river_dressing(M):
    # rocks along both banks (also break the straight water line), reeds/grass
    for side_x, yaw0 in ((6400, 0), (9600, 180)):
        y = MAP_Y0 + 600
        while y < MAP_Y1 - 400:
            if abs(y - BRIDGE_Y) > 700:
                place(random.choice(["SM_Rock02", "SM_Rock09"]),
                      (side_x + random.uniform(-200, 200), y, -330 + random.uniform(-60, 40)),
                      (random.uniform(-10, 10), random.uniform(0, 360), 0), random.uniform(0.8, 1.6), None,
                      "River/Rocks")
            y += random.uniform(900, 1700)
    # stepping-rock tease downstream (not a crossing: gaps too big; comedy bait)
    for k, x in enumerate((7300, 7900, 8600)):
        place("SM_Rock09", (x, -6500 + k * 120, WATER_Z - 120), (0, k * 70, 0), (1.4, 1.4, 0.9), None, "River/Rocks")
    # spit-out pile on the WEST (farm) bank, 120 m downstream of the bridge
    px, py = 5400.0, BRIDGE_Y - 12000.0
    slab(px - 500, px + 500, py - 500, py + 500, 3, M["shore"], "SpitOutBeach", "River/SpitOut", 10)
    place("SM_PileofGrain", (px, py, 0), (0, 0, 0), (2.5, 2.5, 1.6), None, "River/SpitOut", mat=M["shore"])
    place("SM_WoodenSignpost", (px - 300, py + 350, 0), (0, 160, 0), 1.3, "Sign_LostAndFound", "River/SpitOut")
    for k in range(5):
        place("SM_Rock09", (px + random.uniform(-450, 450), py + random.uniform(-450, 450), 0),
              (0, random.uniform(0, 360), 0), random.uniform(0.4, 0.8), None, "River/SpitOut")
    marker(["HP_RiverPile"], (px, py, 30), "HP_RiverPile")
    # river kill/carry volume
    marker(["HP_River"], ((6560 + 9440) / 2, (MAP_Y0 + MAP_Y1) / 2, (BED_Z + WATER_Z + 60) / 2), "HP_River",
           box=((9440 - 6560) / 100.0, (MAP_Y1 - MAP_Y0) / 100.0, (WATER_Z + 60 - BED_Z) / 100.0))


def build_farm(M):
    bx, by = BARN
    # --- barn (front faces +X toward bridge), brick-toned per art bible
    place("SM_Barn", (bx, by, 1), (0, -90, 0), 1.26, "Barn", "Farm/Barn", mat=M["barn_brick"])
    place("SM_Barn_Door", (bx + 930, by + 296, 0.5), (0, 40, 0), (1.26, 1.26, 1.15), "BarnDoor_N", "Farm/Barn")
    place("SM_Barn_Door", (bx + 899, by - 280, 0.5), (0, -50, 0), (1.26, 1.26, 1.15), "BarnDoor_S", "Farm/Barn")
    # barn yard dressing: hay stacks (hide spots), cart, tools, crates (static decor)
    for (x, y, yaw) in ((bx + 1200, by + 1050, 10), (bx + 1350, by + 1150, 80), (bx + 1250, by + 1100, 40)):
        place("SM_HayBalesRectangular", (x, y, 0), (0, yaw, 0), 1.6, None, "Farm/Barn")
    place("SM_HayBalesRectangular", (bx + 1270, by + 1100, 80), (0, 25, 0), 1.6, None, "Farm/Barn")
    place("SM_HayBalesCylinder", (bx + 1100, by - 1150, 0), (0, 0, 0), 1.6, None, "Farm/Barn")
    place("SM_HayBalesCylinder", (bx + 1300, by - 1080, 0), (0, 70, 0), 1.6, None, "Farm/Barn")
    place("SM_WoodenCart", (bx + 2000, by - 1000, 0), (0, 110, 0), 1.5, "Cart_HideUnder", "Farm/Barn")
    place("SM_WoodenWheelbarrow", (bx + 1650, by + 820, 0), (0, -30, 0), 1.3, None, "Farm/Barn")
    place("SM_Pitchfork", (bx + 1000, by + 760, 95), (0, 0, 12), 1.2, None, "Farm/Barn")
    place("SM_BellPost", (bx + 1450, by - 650, 0), (0, 90, 0), 1.3, "FarmBell", "Farm/Barn")
    place("SM_LanternOutdoor", (bx + 1000, by - 700, 0), (0, 180, 0), 1.0, None, "Farm/Barn")
    place("SM_WoodenSignpost", (BRIDGE_X0 - 1200, 450, 0), (0, 180, 0), 1.3, "Sign_ToMarket", "Farm/Barn")
    for k in range(6):
        place(random.choice(["SM_Crate", "SM_Crate02", "SM_EmptySack", "SM_Bushel"]),
              (bx + 1000 + random.uniform(-60, 60), by - 950 + k * 70, 0), (0, random.uniform(0, 360), 0),
              1.25, None, "Farm/Barn")

    # --- gameplay markers at barn
    for k, y in enumerate((-450, -150, 150, 450)):
        ps = eas.spawn_actor_from_class(unreal.PlayerStart, unreal.Vector(bx + 1500, by + y, 100),
                                        unreal.Rotator(0, 0, 0))
        ps.set_actor_label("PlayerStart_%d" % (k + 1))
        ps.set_folder_path("Markers")
        ps.tags = [unreal.Name("HP_PlayerStart"), unreal.Name("HP_PlayerStart_%d" % (k + 1))]
        STATS["Markers"] = STATS.get("Markers", 0) + 1
    marker(["HP_BarnRespawn"], (bx + 1250, by, 20), "HP_BarnRespawn")
    # customization wardrobe: inside the barn, just behind the open doors, near the player starts
    marker(["HP_Wardrobe"], (bx + 600, by + 300, 20), "HP_Wardrobe", rot=(0, 180, 0))
    for k in range(4):
        marker(["HP_Goods_Crate"], (bx + 1900, by + 650 + k * 140, 20), "HP_Goods_Crate_%d" % (k + 1))

    # --- pig pen (18 x 12 m) with mud wallow; S gate gap, N critter hole behind hay
    pcx, pcy, pw, ph = 1600.0, 3400.0, 1800.0, 1200.0
    slab(pcx - 700, pcx + 700, pcy - 450, pcy + 450, 3, M["mud"], "PigPen_Mud", "Farm/PigPen", 10)
    fence_rect(pcx, pcy, pw, ph, "Farm/PigPen", {"S": ((0.4, 0.6),), "N": ((0.75, 0.86),)})
    place("SM_Gate14", (pcx, pcy - ph / 2, 0), (0, 0, 0), (0.42, 0.6, 0.42), "PigPen_Gate", "Farm/PigPen")
    place("SM_HayBalesRectangular", (pcx + 520, pcy + ph / 2 + 160, 0), (0, 15, 0), 1.6, "Hay_PigHole", "Farm/PigPen")
    place("SM_HayBalesRectangular", (pcx + 380, pcy + ph / 2 + 200, 0), (0, -10, 0), 1.6, None, "Farm/PigPen")
    place("SM_WoodenPillar03", (pcx - 500, pcy + 350, 30), (0, 0, 0), (0.4, 2.5, 1.2), "PigTrough", "Farm/PigPen")
    marker(["HP_CritterSpawner_Pig:3"], (pcx, pcy, 50), "HP_CritterSpawner_Pig")

    # --- chicken coop + yard (15 x 15 m); E gap toward orchard (chickens love the orchard)
    ccx, ccy = 1600.0, -3600.0
    place("SM_Coop", (ccx - 300, ccy - 200, 0), (0, 180, 0), 1.35, "ChickenCoop", "Farm/Coop")
    fence_rect(ccx, ccy, 1500, 1500, "Farm/Coop", {"N": ((0.45, 0.62),), "E": ((0.1, 0.22),)})
    place("SM_PileofGrain", (ccx + 350, ccy + 300, 0), (0, 0, 0), 1.2, None, "Farm/Coop")
    place("SM_Harvest_Basket", (ccx + 500, ccy + 500, 0), (0, 0, 0), 1.3, None, "Farm/Coop")
    marker(["HP_CritterSpawner_Chicken:8"], (ccx + 300, ccy + 200, 50), "HP_CritterSpawner_Chicken")

    # --- vegetable rows west of barn (open, low: sightline field)
    for r in range(7):
        y = 2600 + r * 260
        for c in range(3):
            x = -7600 + c * 700
            place("SM_SoilBlock", (x, y, -10), (0, 0, 0), (1.0, 0.9, 0.5), None, "Farm/VegRows")
            for k in range(4):
                place(random.choice(["SM_Plant02", "SM_Seedling"]), (x - 240 + k * 160, y, 20),
                      (0, random.uniform(0, 360), 0), random.uniform(0.6, 0.85), None, "Farm/VegRows",
                      collision=False)

    # --- corn maze (tall cards, corridors) north-east: big hide spot
    mx0, mx1, my0, my1 = -800.0, 4200.0, 5600.0, 8600.0
    corridors_y = (6300, 7600)
    corridors_x = (400, 2400, 3600)
    x = mx0
    while x < mx1:
        y = my0
        while y < my1:
            in_corr = any(abs(y - cy) < 160 for cy in corridors_y) or any(abs(x - cx) < 160 for cx in corridors_x)
            if not in_corr:
                s = random.uniform(1.1, 1.35)
                place("SM_WheatCard02_Combined", (x + random.uniform(-40, 40), y + random.uniform(-40, 40), 0),
                      (0, random.uniform(0, 360), 0), (s, s, s * 2.0), None, "Farm/CornMaze", collision=False)
            y += 210
        x += 210
    place("SM_Scarecrow", (2400, 7600, 0), (0, 200, 0), 1.6, "Scarecrow_Maze", "Farm/CornMaze")

    # --- wheat field west (wind VFX from the pack)
    wx0, wx1, wy0, wy1 = -10800.0, -6800.0, -1500.0, 1800.0
    x = wx0
    while x < wx1:
        y = wy0
        while y < wy1:
            s = random.uniform(1.0, 1.25)
            place("SM_WheatCard02_Combined", (x + random.uniform(-50, 50), y + random.uniform(-50, 50), 0),
                  (0, random.uniform(0, 360), 0), (s, s, s * 1.3), None, "Farm/Wheat", collision=False)
            y += 240
        x += 240
    for (x, y) in ((-9800, 0), (-7800, 0)):
        n = eas.spawn_actor_from_class(unreal.NiagaraActor, unreal.Vector(x, y, 150), unreal.Rotator(0, 0, 0))
        n.niagara_component.set_asset(load(EL + "/VFX/NS_Wheat"))
        n.set_folder_path("Farm/Wheat")

    # --- pumpkin patch south-west (prize pumpkin goods marker)
    ppx, ppy = -5600.0, -4400.0
    slab(ppx - 700, ppx + 700, ppy - 700, ppy + 700, 3, M["soil"], "PumpkinPatch_Soil", "Farm/PumpkinPatch", 10)
    for k in range(18):
        a = random.uniform(0, 2 * math.pi)
        r = random.uniform(150, 620)
        place("SM_Plant02", (ppx + math.cos(a) * r, ppy + math.sin(a) * r, 0), (0, random.uniform(0, 360), 0),
              random.uniform(0.9, 1.3), None, "Farm/PumpkinPatch", collision=False)
    for k in range(7):
        a = random.uniform(0, 2 * math.pi)
        r = random.uniform(200, 600)
        place("SM_Watermelon", (ppx + math.cos(a) * r, ppy + math.sin(a) * r, 25), (0, random.uniform(0, 360), 0),
              random.uniform(1.4, 1.9), None, "Farm/PumpkinPatch")
    place("SM_Scarecrow", (ppx + 450, ppy + 450, 0), (0, 225, 0), 1.6, "Scarecrow_Patch", "Farm/PumpkinPatch")
    fence_rect(ppx, ppy, 1600, 1600, "Farm/PumpkinPatch", {"E": ((0.4, 0.6),), "N": ((0.1, 0.3),)})
    marker(["HP_Goods_PrizePumpkin"], (ppx, ppy, 40), "HP_Goods_PrizePumpkin")

    # --- orchard south (trees = shade + hide spots + LOS breakers)
    for r, y in enumerate((-8000, -6800, -5600)):
        for c in range(5):
            x = -200 + c * 1150 + (500 if r % 2 else 0)
            place("SM_Tree01", (x + random.uniform(-80, 80), y + random.uniform(-80, 80), 0),
                  (0, random.uniform(0, 360), 0), random.uniform(0.85, 1.05), None, "Farm/Orchard")
    for k in range(5):
        place("SM_Harvest_Basket", (random.uniform(0, 5000), random.uniform(-8200, -5400), 0),
              (0, random.uniform(0, 360), 0), 1.3, None, "Farm/Orchard")

    # --- windmill landmark (west, visible from bridge down the main axis)
    wmx, wmy, wyaw = -8600.0, -3800.0, 25.0
    place("SM_Windmill", (wmx, wmy, 0), (0, wyaw, 0), 1.0, "Windmill", "Farm/Windmill")
    lx, ly, lz = -44.0, 547.0, 1542.0
    c, s = math.cos(math.radians(wyaw)), math.sin(math.radians(wyaw))
    place("SM_Windmill_Rotate", (wmx + lx * c - ly * s, wmy + lx * s + ly * c, lz), (5.19, wyaw + 180, -180),
          (0.741, 0.951, 0.741), "Windmill_Blades", "Farm/Windmill", movable=True)

    # --- bush hedge along west river bank: hide spots with critter-sized gaps
    y = MAP_Y0 + 800
    while y < MAP_Y1 - 600:
        if abs(y) > 900:
            place("SM_Bush03", (5550 + random.uniform(-120, 120), y, 0), (0, random.uniform(0, 360), 0),
                  random.uniform(0.6, 0.85), None, "Farm/Hedges")
        y += random.uniform(900, 1500)
    # boundary hedges / fences (north, south, west edges of farm zone)
    fence_line((-11600, 9000), (5000, 9000), "Farm/Boundary", gaps=((0.47, 0.5),), mesh_name="SM_WoodenFenceLong", seg=271)
    fence_line((-11600, -9200), (4400, -9200), "Farm/Boundary", gaps=((0.6, 0.64),), mesh_name="SM_WoodenFenceLong", seg=271)
    fence_line((-11600, -9200), (-11600, 9000), "Farm/Boundary", mesh_name="SM_WoodenFenceLong", seg=271)
    for k in range(14):
        place("SM_Bush03", (random.uniform(-11400, 4000), 9400 + random.uniform(-80, 80), 0),
              (0, random.uniform(0, 360), 0), random.uniform(0.7, 1.0), None, "Farm/Boundary")

    # --- ground scatter: grass tufts + flowers (no collision; shadows off for perf)
    avoid = ((-4000, -1000, -1500, 1500), (-11000, -6600, -1700, 2000), (-800, 4300, 5500, 8700),
             (BARN[0] + 700, BRIDGE_X0, -400, 400), (5900, 10100, MAP_Y0, MAP_Y1))
    scatter(["SM_GrassCard03_Moss", "SM_GrassCard"], 260, FARM_X0 + 400, FARM_X1 - 300, MAP_Y0 + 400, MAP_Y1 - 400,
            "Farm/Scatter", 1.0, 1.6, avoid, shadows=False)
    scatter(["SM_FlowerGroup", "SM_FlowerGroup02"], 45, FARM_X0 + 400, FARM_X1 - 300, MAP_Y0 + 400, MAP_Y1 - 400,
            "Farm/Scatter", 0.5, 0.9, avoid, shadows=False)
    scatter(["SM_Rock02", "SM_Rock09"], 18, FARM_X0 + 400, FARM_X1 - 600, MAP_Y0 + 400, MAP_Y1 - 400,
            "Farm/Scatter", 0.5, 1.0, avoid, collision=True)


def build_market(M):
    # entrance arch at the bridge head
    place("SM_Gate14", (10650, 0, 0), (0, 90, 0), (0.9, 1.0, 0.9), "Market_Arch", "Market/Structures")
    # sale pen (8 x 8 m), open toward the bridge (W) so players can lob goods in
    pcx, pcy = PEN_MARKET
    fence_rect(pcx, pcy, 800, 800, "Market/SalePen", {"W": ((0.3, 0.7),)})
    place("SM_Crate02", (pcx + 520, pcy + 300, 0), (0, 10, 0), 1.6, "SOLD_Crate", "Market/SalePen")
    place("SM_WoodenSignpost", (pcx - 500, pcy - 450, 0), (0, 160, 0), 1.4, "Sign_SalePen", "Market/SalePen")
    marker(["HP_MarketPen"], (pcx, pcy, 100), "HP_MarketPen", box=(8.0, 8.0, 2.0))

    # three stalls: Butcher (N), Greengrocer (S), Oddities (E)
    def stall(cx, cy, yaw, label, awning, wares):
        place("SM_Table01", (cx, cy, 0), (0, yaw, 0), 1.4, label + "_Counter", "Market/Stalls")
        c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        for lx, ly in ((-190, -110), (190, -110), (-190, 160), (190, 160)):
            px, py = cx + lx * c - ly * s, cy + lx * s + ly * c
            place("SM_WoodenPillar03", (px, py, 0), (90, 0, 0), (0.42, 0.9, 0.9), None, "Market/Stalls",
                  mat=M["wood"])
        place(CUBE, (cx + 25 * s, cy - 25 * c, 300), (0, yaw, -8), (4.6, 3.4, 0.12), label + "_Awning",
              "Market/Stalls", mat=awning)
        for k, w in enumerate(wares):
            lx = -120 + k * 80
            place(w, (cx + lx * c, cy + lx * s, 98), (0, random.uniform(0, 360), 0), 1.2, None, "Market/Stalls",
                  collision=False)
        place("SM_WoodenSignpost", (cx + 260 * c + 150 * s, cy + 260 * s - 150 * c, 0), (0, yaw, 0), 1.2,
              label + "_Sign", "Market/Stalls")

    stall(13300, 1900, 180, "Stall_Butcher", M["roof_brick"], ["SM_RopeCoil", "SM_Bushel", "SM_Crate"])
    stall(13300, -1900, 0, "Stall_Greengrocer", M["awning_cream"],
          ["SM_Carrot", "SM_Eggplant", "SM_Radish", "SM_Onion"])
    stall(15200, 0, 90, "Stall_Oddities", M["awning_gold"], ["SM_Honey", "SM_Mushroom", "SM_SquarePot"])
    # town bell (sundown -60 s critter magnet) + lanterns
    place("SM_BellPost", (13800, 0, 0), (0, 0, 0), 1.8, "TownBell", "Market/Structures")
    for (x, y) in ((11000, 2600), (11000, -2600), (16000, 2600), (16000, -2600), (13800, 2700), (13800, -2700)):
        place("SM_LanternOutdoor", (x, y, 0), (0, random.uniform(0, 360), 0), 1.0, None, "Market/Structures")
    # Muddlebury Feed & Seed (plaster barn) closes the square
    place("SM_Barn", (17100, 0, 1), (0, 90, 0), 1.1, "TownHall_FeedAndSeed", "Market/Structures", mat=M["plaster"])
    place("SM_Coop", (16200, 5200, 0), (0, 200, 0), 1.2, "Town_Shed", "Market/Structures")
    place("SM_Windmill", (15500, -6500, 0), (0, -40, 0), 0.8, "Town_Windmill", "Market/Structures")
    for k in range(10):
        place(random.choice(["SM_Crate", "SM_Crate02", "SM_HayBalesRectangular", "SM_EmptySack", "SM_Bushel"]),
              (random.uniform(11200, 16000), random.choice((-1, 1)) * random.uniform(2300, 2700), 0),
              (0, random.uniform(0, 360), 0), 1.25, None, "Market/Props")
    for k in range(6):
        place("SM_Tree01", (random.uniform(11000, 17000), random.choice((-1, 1)) * random.uniform(4200, 8500), 0),
              (0, random.uniform(0, 360), 0), random.uniform(0.85, 1.05), None, "Market/Trees")
    scatter(["SM_GrassCard03_Moss", "SM_GrassCard"], 70, TOWN_X0 + 300, TOWN_X1 - 300, MAP_Y0 + 400, MAP_Y1 - 400,
            "Market/Scatter", 1.0, 1.6, ((10000, 16400, -3000, 3000),), shadows=False)


def build_backdrop(M):
    # ring of stylized hills beyond the playable bounds (no collision; outside walls)
    cx, cy = (FARM_X0 + TOWN_X1) / 2, (MAP_Y0 + MAP_Y1) / 2
    for k in range(12):
        a = 2 * math.pi * k / 12 + random.uniform(-0.1, 0.1)
        r = 24000 + random.uniform(0, 4000)
        place("SM_BackgroundTerrain01", (cx + math.cos(a) * r, cy + math.sin(a) * r, -150),
              (0, random.uniform(0, 360), 0), (3.2, 3.2, random.uniform(1.6, 2.6)), None, "Backdrop",
              mat=M["hills"], collision=False, shadows=False)
    # outer ground skirt so hills don't float
    slab(FARM_X0 - 30000, TOWN_X1 + 30000, MAP_Y0 - 30000, MAP_Y1 + 30000, -60, M["grass"], "Ground_OuterSkirt",
         "Backdrop", 50)
    # trees just outside the fence line for a framed horizon
    for k in range(18):
        a = 2 * math.pi * k / 18
        place("SM_Tree01", (cx + math.cos(a) * 18500, cy + math.sin(a) * 15500, -40), (0, random.uniform(0, 360), 0),
              random.uniform(1.0, 1.4), None, "Backdrop", collision=False)


def build_lighting():
    folder = "Lighting"
    sun = eas.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 3000),
                                     unreal.Rotator(roll=0, pitch=-38, yaw=-125))
    sun.set_actor_label("Sun")
    sun.set_folder_path(folder)
    lc = sun.light_component
    lc.set_mobility(unreal.ComponentMobility.MOVABLE)  # day-loop drives sun angle
    lc.set_intensity(7000.0)  # lux. Paired with manual EV100 = 11 (see PP below)
    lc.set_light_color(unreal.LinearColor(1.0, 0.886, 0.722, 1.0))  # #FFE2B8 golden afternoon
    lc.set_editor_property("atmosphere_sun_light", True)
    lc.set_editor_property("dynamic_shadow_distance_movable_light", 12000.0)

    atm = eas.spawn_actor_from_class(unreal.SkyAtmosphere, unreal.Vector(0, 0, 0), unreal.Rotator(0, 0, 0))
    atm.set_actor_label("SkyAtmosphere")
    atm.set_folder_path(folder)

    sky = eas.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 800), unreal.Rotator(0, 0, 0))
    sky.set_actor_label("SkyLight")
    sky.set_folder_path(folder)
    sc = sky.light_component
    sc.set_mobility(unreal.ComponentMobility.MOVABLE)
    sc.set_editor_property("real_time_capture", True)
    sc.set_editor_property("intensity", 1.0)
    sc.set_editor_property("lower_hemisphere_is_black", False)
    sc.set_editor_property("lower_hemisphere_color", unreal.LinearColor(0.35, 0.3, 0.2, 1))

    fog = eas.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector(0, 0, -300), unreal.Rotator(0, 0, 0))
    fog.set_actor_label("HeightFog")
    fog.set_folder_path(folder)
    fc = fog.component
    fc.set_editor_property("fog_density", 0.012)
    fc.set_editor_property("fog_height_falloff", 0.25)
    fc.set_editor_property("fog_inscattering_luminance", unreal.LinearColor(0.95, 0.85, 0.65, 1))
    fc.set_editor_property("start_distance", 4000.0)
    fc.set_editor_property("enable_volumetric_fog", False)  # art bible: volumetric only on High

    ppv = eas.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0), unreal.Rotator(0, 0, 0))
    ppv.set_actor_label("PostProcess_Day")
    ppv.set_folder_path(folder)
    ppv.set_editor_property("unbound", True)
    s = ppv.settings

    def ov(name, val):
        try:
            s.set_editor_property("override_" + name, True)
        except Exception:
            pass
        s.set_editor_property(name, val)

    # Exposure: Manual, physical camera -> EV100 = log2(N^2 / t) at ISO 100 = log2(5.84^2 * 60) ~= 11.0
    ov("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    ov("auto_exposure_apply_physical_camera_exposure", True)
    ov("camera_iso", 100.0)
    ov("camera_shutter_speed", 60.0)
    ov("depth_of_field_fstop", 5.84)
    ov("auto_exposure_bias", 0.0)
    # Grade (art bible 6): Sat 1.1, Contrast 1.05, Filmic toe 0.6, golden-afternoon white balance
    ov("color_saturation", unreal.Vector4(1.1, 1.1, 1.1, 1.0))
    ov("color_contrast", unreal.Vector4(1.05, 1.05, 1.05, 1.0))
    ov("film_toe", 0.6)
    ov("white_temp", 6900.0)
    ov("motion_blur_amount", 0.0)
    ov("film_grain_intensity", 0.0)
    ov("vignette_intensity", 0.25)
    ov("bloom_intensity", 0.5)
    ppv.settings = s


def build_volumes():
    folder = "Volumes"
    X0, X1, Y0, Y1 = FARM_X0, TOWN_X1, MAP_Y0, MAP_Y1

    def box_volume(cls, center, size, label):
        v = eas.spawn_actor_from_class(cls, unreal.Vector(*center), unreal.Rotator(0, 0, 0))
        origin, ext = v.get_actor_bounds(False)
        base = max(ext.x, 1.0) * 2.0  # default factory brush is a 200 uu cube
        v.set_actor_scale3d(unreal.Vector(size[0] / base, size[1] / base, size[2] / base))
        v.set_actor_label(label)
        v.set_folder_path(folder)
        return v

    nav = box_volume(unreal.NavMeshBoundsVolume, ((X0 + X1) / 2, (Y0 + Y1) / 2, -100),
                     (X1 - X0 + 400, Y1 - Y0 + 400, 2400), "NavMeshBounds_Playable")
    nav.tags = [unreal.Name("HP_NavBounds")]
    H = 6000.0
    T = 400.0
    walls = (
        (((X0 + X1) / 2, Y1 + T / 2, H / 2 - 800), (X1 - X0 + 2 * T, T, H), "Block_North"),
        (((X0 + X1) / 2, Y0 - T / 2, H / 2 - 800), (X1 - X0 + 2 * T, T, H), "Block_South"),
        ((X0 - T / 2, (Y0 + Y1) / 2, H / 2 - 800), (T, Y1 - Y0 + 2 * T, H), "Block_West"),
        ((X1 + T / 2, (Y0 + Y1) / 2, H / 2 - 800), (T, Y1 - Y0 + 2 * T, H), "Block_East"),
    )
    for c, sz, lab in walls:
        v = box_volume(unreal.BlockingVolume, c, sz, lab)
        v.tags = [unreal.Name("HP_WorldEdge")]


def build_cameras():
    """Editor-only shot cameras used by the screenshot verifier (tag HP_ShotCam)."""
    shots = (
        ("Shot_BridgeApproach", (4300, -900, 260), (-6, 8, 0)),
        ("Shot_FarmOverview", (9000, -9500, 4200), (-24, 140, 0)),
        ("Shot_BarnSpawn", (BARN[0] + 1500, -150, 190), (-2, 0, 0)),
        ("Shot_PigPen", (2400, 1600, 400), (-14, 120, 0)),
        ("Shot_Market", (10300, -2200, 450), (-10, 25, 0)),
        ("Shot_BridgeSide", (8000, -2600, -150), (6, 90, 0)),
        ("Shot_TopDown", (2700, -2200, 30000), (-89, 0, 0)),
    )
    for label, loc, rot in shots:
        c = eas.spawn_actor_from_class(unreal.CameraActor, unreal.Vector(*loc),
                                       unreal.Rotator(roll=rot[2], pitch=rot[0], yaw=rot[1]))
        c.set_actor_label(label)
        c.set_folder_path("Editor/ShotCams")
        c.tags = [unreal.Name("HP_ShotCam")]
        c.camera_component.set_editor_property("field_of_view", 80.0 if "Top" not in label else 70.0)


def main():
    log("building materials")
    M = build_materials()
    if EAL.does_asset_exist(MAP_PATH):
        EAL.delete_asset(MAP_PATH)
    EAL.make_directory("/Game/HarvestPanic/Maps")
    ok = les.new_level(MAP_PATH, False)
    if not ok:
        raise RuntimeError("new_level failed")
    log("ground");     build_ground(M)
    log("bridge");     build_bridge(M)
    log("river");      build_river_dressing(M)
    log("farm");       build_farm(M)
    log("market");     build_market(M)
    log("backdrop");   build_backdrop(M)
    log("lighting");   build_lighting()
    log("volumes");    build_volumes()
    log("cameras");    build_cameras()
    try:
        unreal.EditorLevelLibrary.build_navigation() if hasattr(unreal.EditorLevelLibrary, "build_navigation") else None
    except Exception as e:
        log("nav build skipped: %s" % e)
    les.save_current_level()
    total = len(eas.get_all_level_actors())
    log("saved %s, %d actors" % (MAP_PATH, total))
    for k in sorted(STATS):
        log("  %-28s %d" % (k, STATS[k]))


main()
