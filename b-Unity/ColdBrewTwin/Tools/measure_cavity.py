"""量测下层液仓内腔轮廓。

沿装配轴线逐层向外发射射线，取首个实体交点的半径，得到 (高度, 半径) 折线；
再沿轴线向下打一条射线定位仓底。输出可直接粘进 TankCavity.MeasuredProfile。

    pip install trimesh rtree numpy
    python measure_cavity.py <model.glb>
"""
import math
import sys

import numpy as np
import trimesh

FLOOR_PROBE_Z = 0.060   # m，一定落在腔内的探测高度
CEILING_Z = 0.15250     # m，2-4 / 2-5 垫圈密封面
RAYS = 32
COLLINEAR_TOL = 2e-4    # m，折线简化阈值


def load_world_mesh(path):
    scene = trimesh.load(path, process=False)
    parts = []
    for node in scene.graph.nodes_geometry:
        matrix, name = scene.graph[node]
        mesh = scene.geometry[name].copy()
        mesh.apply_transform(matrix)
        parts.append(mesh)
    return trimesh.util.concatenate(parts)


def free_radius(mesh, z, dirs):
    origins = np.tile([0.0, 0.0, z], (len(dirs), 1))
    hits, _, _ = mesh.ray.intersects_location(origins, dirs, multiple_hits=False)
    if len(hits) == 0:
        return None
    return float(np.median(np.hypot(hits[:, 0], hits[:, 1])))


def volume_litres(profile, top):
    total = 0.0
    for (y0, r0), (y1, r1) in zip(profile, profile[1:]):
        if y0 >= top:
            break
        y = min(y1, top)
        r = r0 + (r1 - r0) * ((y - y0) / (y1 - y0)) if y1 > y0 else r1
        total += math.pi * (y - y0) * (r0 * r0 + r0 * r + r * r) / 3.0
    return total * 1000.0


def main(path):
    mesh = load_world_mesh(path)
    angles = np.linspace(0, 2 * np.pi, RAYS, endpoint=False)
    dirs = np.stack([np.cos(angles), np.sin(angles), np.zeros_like(angles)], 1)

    hits, _, _ = mesh.ray.intersects_location(
        np.array([[0.0, 0.0, FLOOR_PROBE_Z]]), np.array([[0.0, 0.0, -1.0]]),
        multiple_hits=True)
    floor = float(np.sort(hits[:, 2])[-1])

    heights = sorted(set(
        list(np.arange(floor, 0.0345, 0.0005)) +
        list(np.arange(0.035, 0.1275, 0.005)) +
        list(np.arange(0.1275, CEILING_Z, 0.0005))))

    raw = []
    for z in heights:
        r = free_radius(mesh, z + 1e-5, dirs)
        if r is not None:
            raw.append((round(z, 5), round(r, 5)))
    raw.append((CEILING_Z, raw[-1][1]))

    profile = [raw[0]]
    for i in range(1, len(raw) - 1):
        y0, r0 = profile[-1]
        y1, r1 = raw[i]
        y2, r2 = raw[i + 1]
        if y2 == y0:
            continue
        predicted = r0 + (r2 - r0) * (y1 - y0) / (y2 - y0)
        if abs(predicted - r1) > COLLINEAR_TOL:
            profile.append(raw[i])
    profile.append(raw[-1])

    print("floor  = %.5f m" % floor)
    print("ceiling= %.5f m" % CEILING_Z)
    print("capacity to ceiling      = %.4f L" % volume_litres(profile, CEILING_Z))
    print("capacity to straight bore= %.4f L" % volume_litres(profile, 0.128))
    print()
    for y, r in profile:
        print("            new Vector2(%.5ff, %.5ff)," % (y, r))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    main(sys.argv[1])
