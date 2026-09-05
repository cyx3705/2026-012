import bpy, sys, math, bmesh
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--")+1:]
FBX, OUT, HIDE = argv[0], argv[1], argv[2] == "hide"

PROFILE = [(0.02250,0.05298),(0.02300,0.05362),(0.02350,0.05567),(0.02450,0.05789),
           (0.02800,0.06144),(0.03500,0.06298),(0.12800,0.06298),(0.13400,0.05705),
           (0.13500,0.05698),(0.14100,0.05098),(0.15000,0.05098),(0.15050,0.05398),
           (0.15250,0.05398)]
LITRES = 0.5

def radius_at(y):
    if y <= PROFILE[0][0]: return PROFILE[0][1]
    if y >= PROFILE[-1][0]: return PROFILE[-1][1]
    for i in range(1, len(PROFILE)):
        if y > PROFILE[i][0]: continue
        y0,r0 = PROFILE[i-1]; y1,r1 = PROFILE[i]
        return r0 + (r1-r0)*(y-y0)/(y1-y0)
    return PROFILE[-1][1]

def volume_below(top):
    v = 0.0
    for (y0,r0),(y1,r1) in zip(PROFILE, PROFILE[1:]):
        if y0 >= top: break
        y = min(y1, top)
        r = r0 + (r1-r0)*((y-y0)/(y1-y0)) if y1 > y0 else r1
        v += math.pi*(y-y0)*(r0*r0+r0*r+r*r)/3.0
    return v*1000.0

lo, hi = PROFILE[0][0], PROFILE[-1][0]
for _ in range(60):
    mid = 0.5*(lo+hi)
    if volume_below(mid) < LITRES: lo = mid
    else: hi = mid
LEVEL = 0.5*(lo+hi)
print("level y = %.5f m  (%.4f L, capacity %.4f L)" % (LEVEL, volume_below(LEVEL), volume_below(hi)))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)

# 把整机摆正：高度落到 +Z，底面贴 z=0
roots = [o for o in bpy.data.objects if o.parent is None]
def zrange():
    lo_, hi_ = 1e9, -1e9
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        for c in o.bound_box:
            z = (o.matrix_world @ Vector(c)).z
            lo_ = min(lo_, z); hi_ = max(hi_, z)
    return lo_, hi_
z0, z1 = zrange()
if z1 <= 0.01:
    for r in roots:
        r.matrix_world = Matrix.Rotation(math.radians(180), 4, 'X') @ r.matrix_world
    bpy.context.view_layer.update()
print("machine z range", zrange())

# 水体：把轮廓旋成回转面
verts, faces = [], []
SEG = 96
rings = [p for p in PROFILE if p[0] < LEVEL] + [(LEVEL, radius_at(LEVEL))]
for y, r in rings:
    for i in range(SEG):
        a = i/SEG*math.pi*2
        verts.append((math.cos(a)*r, math.sin(a)*r, y))
for ri in range(len(rings)-1):
    for i in range(SEG):
        j = (i+1) % SEG
        faces.append((ri*SEG+i, ri*SEG+j, (ri+1)*SEG+j, (ri+1)*SEG+i))
base = len(verts); verts.append((0,0,rings[0][0]))
for i in range(SEG): faces.append((base, (i+1) % SEG, i))
top = len(verts); verts.append((0,0,LEVEL))
off = (len(rings)-1)*SEG
for i in range(SEG): faces.append((top, off+i, off+((i+1) % SEG)))

me = bpy.data.meshes.new("Water"); me.from_pydata(verts, [], faces); me.validate(); me.update()
water = bpy.data.objects.new("Water", me); bpy.context.collection.objects.link(water)
for p in water.data.polygons: p.use_smooth = True

def mat(name, rgba, rough, transmission=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = rgba
    b.inputs["Roughness"].default_value = rough
    if "Transmission Weight" in b.inputs:
        b.inputs["Transmission Weight"].default_value = transmission
    if transmission > 0 and "IOR" in b.inputs:
        b.inputs["IOR"].default_value = 1.33
    return m

body = mat("Body", (0.62, 0.63, 0.66, 1), 0.35)
watm = mat("WaterMat", (0.10, 0.42, 0.78, 1), 0.08, 0.0)
for o in bpy.data.objects:
    if o.type != 'MESH' or o is water: continue
    o.data.materials.clear(); o.data.materials.append(body)
water.data.materials.clear(); water.data.materials.append(watm)

if HIDE:
    for o in bpy.data.objects:
        if o.name.startswith(("2-1-", "2-2-", "2-3-")):
            o.hide_render = True

cam_data = bpy.data.cameras.new("Cam"); cam_data.lens = 38 if not HIDE else 42
cam = bpy.data.objects.new("Cam", cam_data); bpy.context.collection.objects.link(cam)
target = Vector((0, 0, 0.19)) if not HIDE else Vector((0, 0, 0.065))
cam.location = Vector((0.50, -0.55, 0.34)) if not HIDE else Vector((0.26, -0.30, 0.17))
d = target - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
bpy.context.scene.camera = cam

sun_d = bpy.data.lights.new("Sun", 'SUN'); sun_d.energy = 4.0; sun_d.angle = 0.3
sun = bpy.data.objects.new("Sun", sun_d); bpy.context.collection.objects.link(sun)
sun.rotation_euler = (math.radians(50), 0, math.radians(35))
fill_d = bpy.data.lights.new("Fill", 'AREA'); fill_d.energy = 30; fill_d.size = 1.5
fill = bpy.data.objects.new("Fill", fill_d); bpy.context.collection.objects.link(fill)
fill.location = (-0.6, -0.5, 0.6)
fill.rotation_euler = (math.radians(55), 0, math.radians(-50))

world = bpy.data.worlds.new("W"); world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.055, 0.07, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
bpy.context.scene.world = world

sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x, sc.render.resolution_y = 1100, 1300
sc.render.film_transparent = False
sc.render.filepath = OUT
try:
    sc.eevee.taa_render_samples = 64
except Exception:
    pass
bpy.ops.render.render(write_still=True)
print("rendered", OUT)
