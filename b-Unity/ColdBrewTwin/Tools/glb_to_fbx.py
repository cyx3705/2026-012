import bpy, sys, os, math, re, io, json
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--")+1:]
SRC, DST, MANIFEST = argv[0], argv[1], argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)

def world_bbox():
    mn = Vector((1e9,)*3); mx = Vector((-1e9,)*3)
    for o in bpy.data.objects:
        if o.type != 'MESH':
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            for i in range(3):
                mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
    return mn, mx

# The glTF importer maps glTF (x,y,z) to Blender (x,-z,y); cascadio wrote the
# CAD frame unrotated, so the machine arrives lying along -Y. -90 deg about X
# puts CAD +Z back on Blender +Z, keeping CAD coordinates one-to-one.
roots = [o for o in bpy.data.objects if o.parent is None]
mn, mx = world_bbox()
ext = [mx[i]-mn[i] for i in range(3)]
if ext.index(max(ext)) == 1:
    for r in roots:
        r.matrix_world = Matrix.Rotation(math.radians(-90), 4, 'X') @ r.matrix_world
    bpy.context.view_layer.update()
mn, mx = world_bbox()
assert mx.z - mn.z > 0.39 and mn.z > -0.02, ("height not on +Z", tuple(mn), tuple(mx))
print("upright bbox min=%s max=%s" % (tuple(round(v,4) for v in mn), tuple(round(v,4) for v in mx)))

ASSEMBLY = {"0": "0-咖啡冷萃机", "1": "1-底部组件", "2": "2-下层液仓", "3": "3-中间组件",
            "4": "4-上层组件", "5": "5-顶部组件", "6": "6-电气组件"}
PART = {
    "1-1": "1-1-底座",      "1-2": "1-2-脚",         "1-3": "1-3-脚垫钉",
    "2-1": "2-1-玻璃杯",    "2-2": "2-2-内外连接环",  "2-3": "2-3-内胆",
    "2-4": "2-4-垫圈1",     "2-5": "2-5-垫圈1b",
    "3-1": "3-1-中间部分主体", "3-2": "3-2-单向阀",   "3-3": "3-3-粉饼",
    "3-5": "3-5-中间部分顶盖", "3-6": "3-6-按钮",     "3-8": "3-8-按钮PC板",
    "4-1": "4-1-玻璃杯2",   "4-2": "4-2-内外连接环2", "4-3": "4-3-内胆2",
    "4-4": "4-4-冰盖",      "4-5": "4-5-垫圈2",      "4-6": "4-6-垫圈2b",
    "5-1": "5-1-顶座",      "5-2": "5-2-顶盖",       "5-3": "5-3-盖子",
    "6-1": "6-1-水泵",      "6-2": "6-2-电气件",     "6-6": "6-6-管",
}
ID_RE = re.compile(r"^(\d+(?:-\d+)*)")

def clean_name(raw):
    if raw.startswith("l-"):
        m = re.search(r"(GBT[\d.]+)", raw)
        if m and m.group(1).startswith("GBT70"):
            return "l-内六角圆柱头螺钉-%s" % m.group(1)
        if m and m.group(1).startswith("GBT95"):
            return "l-平垫圈-%s" % m.group(1)
        return "l-标准件"
    m = ID_RE.match(raw)
    if not m:
        return raw
    pid = m.group(1)
    tail = raw[len(pid):].lstrip("-")
    if pid in PART:
        return PART[pid]
    if tail and tail.isascii() and tail.strip():        # part number survived intact
        return "%s-%s" % (pid, tail)
    return pid                                          # name was destroyed by the export

rows = []
for o in bpy.data.objects:
    raw = o.name
    o["cad_raw_name"] = raw
    if o.type == 'EMPTY':
        pid = ID_RE.match(raw)
        o.name = ASSEMBLY.get(pid.group(1) if pid else "", "0-咖啡冷萃机" if not pid else pid.group(1))
    else:
        o.name = clean_name(raw)
        o.data.name = o.name
    rows.append((o.name, raw, o.type))

# Blender's ".001" instance suffix reads badly in Unity; make it "_1".
for o in bpy.data.objects:
    m = re.match(r"^(.*)\.(\d{3})$", o.name)
    if m:
        o.name = "%s_%d" % (m.group(1), int(m.group(2)))
        if o.type == 'MESH':
            o.data.name = o.name

with io.open(MANIFEST, "w", encoding="utf-8") as f:
    f.write("name,cad_raw_name,type\n")
    for o in sorted(bpy.data.objects, key=lambda x: x.name):
        f.write('"%s","%s",%s\n' % (o.name, o.get("cad_raw_name", "").replace('"', "'"), o.type))

tri = 0
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.data.calc_loop_triangles(); tri += len(o.data.loop_triangles)
print("objects=%d triangles=%d" % (len(bpy.data.objects), tri))

bpy.ops.export_scene.fbx(
    filepath=DST, use_selection=False, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_NONE', global_scale=1.0,
    axis_forward='-Z', axis_up='Y', object_types={'EMPTY', 'MESH'},
    mesh_smooth_type='FACE', use_mesh_modifiers=True,
    bake_space_transform=False, use_custom_props=True, path_mode='COPY')
print("wrote", DST, os.path.getsize(DST))
