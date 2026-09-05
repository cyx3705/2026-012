"""STEP -> glTF/GLB。

用 cascadio（OpenCASCADE 绑定）把 Solid Edge 导出的 AP214 装配镶嵌成三角网格，
保留装配层级。OpenCASCADE 走 ANSI 路径，含中文的路径会直接报 "Cannot open input
file"，所以先把源文件拷到纯 ASCII 的临时路径。

    pip install cascadio
    python step_to_glb.py <input.stp> <output.glb>
"""
import os
import shutil
import sys
import tempfile

import cascadio

TOL_LINEAR = 0.05   # mm，弦高偏差
TOL_ANGULAR = 0.25  # rad


def main(src, dst):
    with tempfile.TemporaryDirectory() as tmp:
        ascii_src = os.path.join(tmp, "source.stp")
        shutil.copyfile(src, ascii_src)
        ascii_dst = os.path.join(tmp, "out.glb")
        cascadio.step_to_glb(ascii_src, ascii_dst,
                             tol_linear=TOL_LINEAR,
                             tol_angular=TOL_ANGULAR,
                             merge_primitives=False)
        shutil.copyfile(ascii_dst, dst)
    print("wrote %s (%.2f MB)" % (dst, os.path.getsize(dst) / 1e6))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
