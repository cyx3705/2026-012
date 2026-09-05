"""Measure the imported CAD upper liner, powder region and flow annulus in metres."""
import bpy
import json
import math
import sys
from mathutils import Vector, Matrix

source, destination, *resource = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
if max((o.matrix_world @ Vector(c)).z for o in meshes for c in o.bound_box) < .01:
    for root in [o for o in bpy.data.objects if o.parent is None]:
        root.matrix_world = Matrix.Rotation(math.pi, 4, 'X') @ root.matrix_world
    bpy.context.view_layer.update()

def hits(origin, direction, parts):
    result = []
    for obj in parts:
        inv = obj.matrix_world.inverted()
        ok, point, _, _ = obj.ray_cast(inv @ origin, (inv.to_3x3() @ direction).normalized())
        if ok:
            result.append(obj.matrix_world @ point)
    return result

bounds = {}
for obj in meshes:
    points = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    bounds[obj.name] = {'min': [min(p[i] for p in points) for i in range(3)],
                        'max': [max(p[i] for p in points) for i in range(3)]}
liner = [o for o in meshes if o.name.startswith('4-3')]
samples = []
for index in range(220, 381):
    z = index / 1000
    radii = []
    for angle in range(32):
        theta = angle * math.tau / 32
        points = hits(Vector((0, 0, z)), Vector((math.cos(theta), math.sin(theta), 0)), liner)
        if points:
            radii.append(min(math.hypot(p.x, p.y) for p in points))
    if len(radii) == 32:
        samples.append({'y': z, 'minRadius': min(radii), 'maxRadius': max(radii)})
floor_hits = hits(Vector((0, 0, .32)), Vector((0, 0, -1)), meshes)
result = {'source': 'Assets/Models/ColdBrewMachine.fbx', 'axis': 'CAD Z -> Unity Y',
          'axisFloorBelow032': sorted([p.z for p in floor_hits], reverse=True),
          'upperLinerSamples': samples, 'partBounds': bounds}
with open(destination, 'w', encoding='utf-8') as output:
    json.dump(result, output, indent=2, ensure_ascii=False)
if resource:
    # Keep above the ice support (top 0.252495 m), with radial and top clearance.
    profile = [{'x': row['y'], 'y': round(row['minRadius'] - .0004, 6)}
               for row in samples if .253 <= row['y'] <= .374]
    with open(resource[0], 'w', encoding='utf-8') as output:
        json.dump({'profile': profile}, output, indent=2)
print(json.dumps({'samples': samples[::10], 'floors': result['axisFloorBelow032'],
                  'parts': {k: v for k, v in bounds.items() if k.startswith(('3-', '4-', '6-1'))}}, ensure_ascii=False))
