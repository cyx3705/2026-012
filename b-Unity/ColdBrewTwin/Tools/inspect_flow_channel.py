"""Inspect CAD radial wall intersections for the annular flow route."""
import bpy
import json
import math
import sys
from mathutils import Vector, Matrix

source, output = sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source)
parts = [o for o in bpy.data.objects if o.type == 'MESH']
if max((o.matrix_world @ Vector(c)).z for o in parts for c in o.bound_box) < .01:
    for root in [o for o in bpy.data.objects if o.parent is None]:
        root.matrix_world = Matrix.Rotation(math.pi,4,'X') @ root.matrix_world
    bpy.context.view_layer.update()

result = []
direction = Vector((math.cos(.371), math.sin(.371), 0))
for z in [.025,.030,.035,.06,.12,.14,.15,.16,.18,.20,.22,.24,.25,.26,.28,.32,.35,.36,.37,.375,.38]:
    row = {'height': z, 'walls': {}}
    for obj in parts:
        if not obj.name.startswith(('2-1','2-3','3-1','4-1','4-3','5-1','5-2')): continue
        inv = obj.matrix_world.inverted()
        radii = []
        cursor = 0.0
        for count in range(20):
            ok, point, normal, face = obj.ray_cast(inv @ (direction*cursor+Vector((0,0,z))),(inv.to_3x3() @ direction).normalized())
            if not ok: break
            world = obj.matrix_world @ point
            radius = math.hypot(world.x,world.y)
            if radius < cursor - 1e-5 or radius > .10: break
            world_normal = obj.matrix_world.to_3x3() @ normal
            radii.append({'radius':round(radius,6),'normal':round(world_normal.normalized().dot(direction),3)}); cursor = radius + .00003
        if radii: row['walls'][obj.name] = radii
    result.append(row)
with open(output,'w',encoding='utf8') as file: json.dump(result,file,indent=2,ensure_ascii=False)
print(json.dumps(result,ensure_ascii=False))
