"""Generates /Game/ThirdPerson/Maps/Lvl_SliceSmoke with tagged marker actors (docs/engine/marker-contract.md).
Run: UnrealEditor-Cmd <uproject> -run=pythonscript -script=<this file> -nullrhi
"""
import unreal

LEVEL = "/Game/ThirdPerson/Maps/Lvl_SliceSmoke"
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
les.new_level(LEVEL)

def spawn(cls, loc, tag=None, scale=(1, 1, 1), mesh=None, rot=(0, 0, 0)):
    a = eas.spawn_actor_from_class(cls, unreal.Vector(*loc), unreal.Rotator(*rot))
    a.set_actor_scale3d(unreal.Vector(*scale))
    if tag:
        a.tags = [tag]
    if mesh and hasattr(a, "static_mesh_component"):
        a.static_mesh_component.set_static_mesh(unreal.load_asset(mesh))
    return a

cube = "/Engine/BasicShapes/Cube.Cube"
spawn(unreal.StaticMeshActor, (0, 0, -50), None, (40, 40, 1), cube)  # floor
spawn(unreal.TargetPoint, (0, 0, 100), "HP_PlayerStart")
spawn(unreal.StaticMeshActor, (1200, 0, 50), "HP_MarketPen", (6, 6, 3), cube)
spawn(unreal.TargetPoint, (-600, 600, 100), "HP_CritterSpawner_Pig:2")
spawn(unreal.TargetPoint, (-600, -600, 100), "HP_CritterSpawner_Chicken:4")
spawn(unreal.TargetPoint, (400, 300, 100), "HP_Goods_PrizePumpkin")
spawn(unreal.TargetPoint, (400, -300, 100), "HP_Goods_Crate")
spawn(unreal.TargetPoint, (0, 1500, 100), "HP_BarnRespawn")
spawn(unreal.StaticMeshActor, (0, 3000, 0), "HP_River", (30, 8, 2), cube)
spawn(unreal.TargetPoint, (1400, 3000, 100), "HP_RiverPile")
for i in range(4):
    spawn(unreal.StaticMeshActor, (-150 + i * 100, 3000, 20), "HP_BridgePlank", (0.9, 3, 0.1), cube)
spawn(unreal.DirectionalLight, (0, 0, 500))
les.save_current_level()
