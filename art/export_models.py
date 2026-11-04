"""Export approved source models without requiring Blender on Unity consumers.

Run from repository root: Blender --background --factory-startup --python art/export_models.py
Local Unity axes: +Z output/front, +X right, +Y up. Sources use the same axis mapping.
"""
import bpy, bmesh, pathlib, re
from mathutils import Vector
ROOT = pathlib.Path(__file__).resolve().parent.parent
for path in sorted((ROOT/'art/models').glob('*.blend')):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    scene=bpy.data.scenes[path.stem];bpy.context.window.scene=scene
    meshes=[o for o in scene.objects if o.type=='MESH']
    for obj in meshes:
        obj.name = re.sub(r'\.\d{3}$', '', obj.name)
        # Bake authored geometry and align the input/output axis with Unity +Z.
        world=obj.matrix_world.copy()
        for v in obj.data.vertices:
            p=world @ v.co;v.co=Vector((p.y,p.x,p.z))
        bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
        obj.matrix_world.identity()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:obj.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Assets/Models'/f'{path.stem}.fbx'),
        use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',
        use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',
        use_custom_props=False)
    print('EXPORTED',path.stem,flush=True)
