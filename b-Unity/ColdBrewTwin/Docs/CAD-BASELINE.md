# ColdBrewTwin — 咖啡冷萃机数字孪生

Unity **2022.3.62f3c1**（与 `2026-017-UnityLearn/b-Unity-Teach1` 同版本，内置渲染管线，Linear 色彩空间）。

本轮范围只有两件事：把整机 CAD 模型搬进 Unity，并在**下层液仓**里做出水源。
其余机构（中间冲泡腔、上层冰仓、按钮、电气）本轮不碰。

打开场景：`Assets/Scenes/ColdBrewTwin.unity`

## 场景结构

```
ColdBrewTwin
├── Main Camera            取景对准下层液仓
├── Directional Light
├── ColdBrewMachine        整机模型（FBX 预制体实例，位于世界原点）
└── Twin
    └── LowerTank          TankCavity —— 下层液仓内腔的权威几何
        ├── Water          WaterBody  —— 水体（按水量实时重建网格）
        └── WaterSource    WaterSource —— 进水口，位于仓顶 y = 0.1525 m
```

`LowerTank` 与模型是**平级**的，不是模型的子物体。这样即使 FBX 的导入缩放被改动，
水体仍按 CAD 实测尺寸站在原地，错位一眼就能看出来。

## 坐标与单位

* 1 Unity 单位 = 1 米，与 CAD 原点、朝向逐一对应：CAD +Z（高度）→ Unity +Y。
* 整机包围盒：X/Z 方向 ±0.075 m，高度 y ∈ [-0.006, 0.400]（-0.006 是脚垫）。
* 打开工程后先核对这个高度。若整机不是 0.4 单位高，说明 FBX 导入缩放被改过，
  在 `ColdBrewMachine.fbx` 的 Model 导入设置里把 Scale Factor 调回 1、Convert Units 保持勾选。

## 下层液仓与水源

内腔轮廓由 `Tools/measure_cavity.py` 从 CAD 射线量测得到，硬编码在
`TankCavity.MeasuredProfile` 里（局部坐标，米）：

| 量 | 值 |
| --- | --- |
| 仓底 | y = 0.02250 m |
| 仓顶（2-4 / 2-5 垫圈密封面） | y = 0.15250 m |
| 直筒段 | y ∈ [0.035, 0.128]，半径 62.98 mm |
| 顶部收口 | 半径 62.98 → 50.98 mm |
| 满仓容积 | **1.5317 L** |
| 灌到直筒段顶 | 1.3024 L |

内腔各方位半径离散度小于 0.1 mm，是标准回转体，所以用 (高度, 半径) 折线描述无损。

三个组件的分工：

* `TankCavity` —— 只管几何：`RadiusAt(y)`、`VolumeBelow(y)`、`LevelForVolume(litres)`、
  `BuildLiquidMesh(...)`。选中时会用 Gizmo 画出内腔轮廓，可直接和模型比对。
* `WaterBody` —— 水体。改 `volumeLitres` 就按轮廓重建一块贴合内腔的回转网格，
  `[ExecuteAlways]`，编辑模式下拖数值即可看到液面升降。对外给出
  `FillRatio`、`SurfaceY`、`SurfaceWorldY`、`IsFull`。
* `WaterSource` —— 水源本身。`flowLitresPerMinute` 定流量，`stopAtLitres` 定目标水量
  （-1 表示注满），`Open()` / `Close()` / `FillTo(l)` / `FillToCapacity()` 供外部调用。

场景里的默认值：初始 0.5 L，流量 2 L/min，`openOnStart` 打开。进入 Play 后约 31 秒注满。

水体材质 `Assets/Materials/M_Water.mat` 是半透明的，但液仓外面的 `2-1-玻璃杯` 和
`2-3-内胆` 目前是不透明的（CAD 模型没带材质）。想直接看见水，先在 Hierarchy 里
临时隐藏这两个零件，或给它们换个透明材质。

## 预览图

Unity 这台机器上没有可用的编辑器授权，无法用 batchmode 跑一遍导入自检，
所以改用 Blender 按同一份 FBX 和同一组轮廓数值离线渲了两张图（`Tools/render_preview.py`）：

| 图 | 内容 |
| --- | --- |
| [`Docs/整机-Blender预览.png`](Docs/整机-Blender预览.png) | 整机，确认层级、朝向、0.4 m 高度都对 |
| [`Docs/下层液仓水体-0.5L.png`](Docs/下层液仓水体-0.5L.png) | 隐藏 2-1 / 2-2 / 2-3 后的水体，0.5 L，液面 y = 0.06361 m |

这两张只是离线核对用的，Unity 里的实际观感由 `M_Water.mat` 决定。

## 模型是怎么来的

源文件：`../../b-Module-GE/0-咖啡冷萃机-除板子外.stp`（Solid Edge 导出的 AP214，38 个 PRODUCT 定义、55 个零件实例）。

Unity 不认 STEP，走两步转换，脚本都在 `Tools/`：

```bash
pip install cascadio trimesh rtree
python Tools/step_to_glb.py ../../b-Module-GE/0-咖啡冷萃机-除板子外.stp coldbrew.glb
"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup \
    --python Tools/glb_to_fbx.py -- coldbrew.glb Assets/Models/ColdBrewMachine.fbx Assets/Models/PartManifest.csv
```

镶嵌精度 0.05 mm 弦高，共 640 054 个三角面、62 个物体（6 个子装配 + 55 个零件），
层级与 CAD 装配树一致。

## 已知问题：STEP 里的零件名是坏的

Solid Edge 这次是按**原始字节**写零件名的，而且在多字节字符中间截断了。
结果是名字既不是合法 UTF-8 也不是完整 GBK，无法可靠还原——例如整机名
`0-咖啡冷萃机` 在文件里只剩 `0-咖啡冷` 加一个半截字节。

处理办法：零件编号（`2-3`、`6-6`、`l-…GBT70.2`）是纯 ASCII，完好无损，因此**以编号为准**；
中文名只在能干净解码时才补上，补不出来的就保留裸编号（`3-4`、`6-4`、`6-5`）。
每个物体的原始名都留在 `Assets/Models/PartManifest.csv` 里备查。

要拿到干净的名字，请在 Solid Edge 里重新导出，勾选"以 Unicode 格式写出非 ASCII 字符"。

## 本轮没做的事

* 中间冲泡腔、上层冰仓的液体
* 冲泡流程、泵与阀的行为、按钮交互
* 玻璃/不锈钢材质，UI，任何数据接入
