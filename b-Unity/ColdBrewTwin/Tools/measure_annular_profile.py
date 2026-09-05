"""Sample radial CAD intersections without skipping thin connector faces."""
import bpy
import json
import math
import sys
from functools import lru_cache
from mathutils import Vector
from mathutils.bvhtree import BVHTree

source, output = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source)
parts = []
for obj in bpy.data.objects:
    if obj.type != 'MESH' or not obj.name.startswith(('2-1', '2-3', '2-4', '2-5', '3-1', '4-1', '4-3', '4-5', '4-6', '6-2')):
        continue
    mesh = obj.data
    mesh.calc_loop_triangles()
    vertices = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    tree = BVHTree.FromPolygons(vertices, [tuple(t.vertices) for t in mesh.loop_triangles], all_triangles=True)
    parts.append((obj.name, tree))

def intersections(tree, height, angle):
    direction = Vector((math.cos(angle), math.sin(angle), 0))
    origin = Vector((0, 0, height))
    cursor = 0
    hits = []
    for _ in range(40):
        point, normal, face, distance = tree.ray_cast(origin + direction * cursor, direction, .09 - cursor)
        if point is None:
            break
        radius = point.xy.length
        hits.append([round(radius * 1000, 4), round(normal.dot(direction), 3)])
        cursor = radius + .0000002
        if cursor >= .09:
            break
    return hits

rows = []
for height in [.025, .13, .14, .15, .1514, .1516, .1524, .1526, .153, .154, .155, .16, .18, .24, .242, .244, .25, .26, .27, .373, .375, .377, .3775, .378, .3785, .379, .38, .381]:
    row = {'height': height, 'angles': []}
    for angle in [0, .371, math.pi / 2, math.pi]:
        row['angles'].append({'angle': angle, 'parts': {name: intersections(tree, height, angle) for name, tree in parts}})
    rows.append(row)
with open(output, 'w', encoding='utf8') as file:
    json.dump(rows, file, indent=2, ensure_ascii=False)

# Use the intersection envelope of 32 meridians, leaving 60 microns to either wall.
@lru_cache(maxsize=None)
def envelope(height, prefix, entering, minimum):
    values = []
    for name, tree in parts:
        if not name.startswith(prefix):
            continue
        for index in range(32):
            hits = intersections(tree, height, index * math.tau / 32 + .017)
            candidates = [r / 1000 for r, normal in hits if r / 1000 > minimum and (normal < 0 if entering else normal > 0)]
            if candidates:
                values.append(min(candidates) if entering else max(candidates))
    return (min(values) if entering else max(values)) if values else None

def profile_at(height):
    if height <= .15 or height >= .2445:
        lower = height <= .15
        inside = envelope(height, '2-3' if lower else '4-3', False, .04)
        outside = envelope(height, '2-1' if lower else '4-1', True, .06)
        if outside is None:
            outside = envelope(height, '3-1', True, .059)
        if inside is None or outside is None:
            raise RuntimeError(f'Missing cup wall at {height}')
        # Keep the connecting stream inside the seal bore before its axial shoulder.
        taper = max(0, min(1, (height - .147) / .003 if lower else (.2475 - height) / .003))
        outside = min(outside, outside * (1 - taper) + .0606 * taper)
        inside += .00006
        outside -= .00006
    else:
        # Connector CAD has open/reversed faces. The sampled clear corridor is bounded
        # by the inner sleeve and the 59.9 mm outer face; avoid the end seal rings.
        near_seal = height <= .1545 or height >= .239
        inside = .0581 if near_seal else .0541
        outside = .05985
    if outside <= inside:
        raise RuntimeError(f'Closed channel at {height}: {inside}, {outside}')
    # A clearance envelope is not a liquid reservoir. Continue the cup's 0.63 mm
    # wet gap along the outer boundary, including the shoulders and connector.
    # This subset stays within the sampled corridor; connector CAD is still open.
    inside = max(inside, outside - .00063)
    return [round(height, 9), round(inside, 7), round(outside, 7)]

def clearance(a, b):
    height = (a[0] + b[0]) * .5
    if .1505 < height < .2445:
        return None
    lower = height < .2
    inner_wall = envelope(height, '2-3' if lower else '4-3', False, .04)
    outer_wall = envelope(height, '2-1' if lower else '4-1', True, .06)
    if outer_wall is None:
        outer_wall = envelope(height, '3-1', True, .059)
    if inner_wall is None or outer_wall is None:
        return None
    inner_clearance = (a[1] + b[1]) * .5 * math.cos(math.pi / 128) - inner_wall
    outer_clearance = outer_wall - (a[2] + b[2]) * .5
    return inner_clearance, outer_clearance

profile = [profile_at(round(.025 + step * .0005, 7)) for step in range(697)]
for iteration in range(8):
    additions = []
    for a, b in zip(profile, profile[1:]):
        distance = clearance(a, b)
        if distance is not None and min(distance) < .000005:
            additions.append(profile_at((a[0] + b[0]) * .5))
    if not additions:
        break
    profile = sorted(profile + additions)
distances = [distance for a, b in zip(profile, profile[1:]) if (distance := clearance(a, b)) is not None]
minimum_inner = min(d[0] for d in distances)
minimum_outer = min(d[1] for d in distances)
checked = len(distances) * 32
capacity = sum(math.pi * (b[0] - a[0]) / 3 *
               (a[2]**2 + a[2]*b[2] + b[2]**2 - a[1]**2 - a[1]*b[1] - b[1]**2) * 1000
               for a, b in zip(profile, profile[1:]))
print('Annular profile sections:', len(profile), 'capacity L:', capacity)
from pathlib import Path
project = Path(source).resolve().parents[2]
validation = {'cup_ray_samples': checked, 'min_inner_clearance_mm': minimum_inner * 1000,
              'min_outer_clearance_mm': minimum_outer * 1000, 'channel_capacity_litres': capacity,
              'max_wet_gap_mm': max(p[2] - p[1] for p in profile) * 1000,
              'connector_boundary': 'CAD contains open/reversed faces; a 0.63 mm wet gap follows the outer clearance boundary, not the entire connector cavity.'}
lip = [(.373, (profile[-1][1] + profile[-1][2]) * .5), (.375, .06527), (.377, .0633),
       (.3775, .06235), (.378, .0617), (.3784, .05925), (.378, .0563), (.3775, .05612),
       (.377, .05518), (.375, .05323), (.373, .05236), (.371, .0515)]
inlet = [(.394 - y, radius) for y, radius in reversed(lip)] + [(.025, (profile[0][1] + profile[0][2]) * .5)]
collisions = []
for label, route in [('upper_lip', lip), ('lower_inlet', inlet)]:
    for index in range(32):
        angle = index * math.tau / 32 + .017
        path = [Vector((radius * math.cos(angle), radius * math.sin(angle), y)) for y, radius in route]
        for a, b in zip(path, path[1:]):
            for name, tree in parts:
                point, normal, face, distance = tree.ray_cast(a, (b - a).normalized(), (b - a).length)
                if point is not None:
                    collisions.append({'route': label, 'part': name, 'height_mm': round(point.z * 1000, 3)})
validation['lip_segment_collisions'] = collisions
with open(project / 'Docs/channel-alignment-validation.json', 'w', encoding='utf8') as file:
    json.dump(validation, file, indent=2)
print(json.dumps(validation))
if minimum_inner < 0 or minimum_outer < 0:
    raise RuntimeError('Interpolated channel crosses a sampled cup wall.')
if collisions:
    raise RuntimeError('A lip route segment crosses a CAD face.')
with open(project / 'Assets/Resources/AnnularChannel.json', 'w', encoding='utf8') as file:
    json.dump({'profile': [{'x': y, 'y': inner, 'z': outer} for y, inner, outer in profile]}, file, indent=2)
