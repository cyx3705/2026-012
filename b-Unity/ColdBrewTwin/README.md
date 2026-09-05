# 咖啡冷萃机数字孪生模拟版

Unity **2022.3.62f3c1**，Windows 64 位，内置渲染管线。当前范围为纯离线模拟，不枚举串口、不连接 ESP32、不读写原 WPF 上位机数据。

## 运行

- 最新外观版运行 `Builds/Studio/ColdBrewTwin.exe`。淡蓝玻璃杯体、红色磨砂盖座、蓝色电池、黑色橡胶垫和硅胶按钮；电机材质沿用。加入灯箱环境反射、柔和阴影、展示台、8x MSAA 与半透明 UI。
- 剖视默认关闭，左侧“剖视”开关可随时切换；剖视时杯体与内胆自动换成不透明浅灰材质，关闭后恢复淡蓝玻璃。液面、水柱和水滴为咖啡色，剖切面显示实际宽度的流道截面。切换视角和启动演示不会改动剖视选择。外观截图见 `Docs/StudioAppearance.png`，流道剖视见 `Docs/CoffeeChannel.png`，动画见 `Docs/StudioFlow.gif`。
- 新水流演示运行 `Builds/WaterFlow/ColdBrewTwin.exe`，保留同目录下的全部构建文件。`Builds/Windows` 为之前的模拟版。
- 默认打开“水流”页，点击“开始水流演示”：加压上液 12 秒、停留 2 秒、粉饼回流 12 秒、停留 2 秒。运行中可调整上液气压和降液真空度。实际录帧见 `Docs/WaterFlow.gif`。
- 或用 Unity 打开本目录，打开 `Assets/Scenes/ColdBrewTwin.unity`，点击 Play。
- 应打开内层 `b-Unity/ColdBrewTwin`，外层 `b-Unity` 不是本次应用工程。
- 冷启动为待机，两泵关闭，初始 0.5 L 液体全部位于下仓。场景水源不再自动注水。

## 已实现

- 手动上液、降液、软件停止；到达标定时间自动停止，保留液量。
- 四阶段循环：上液、上部停留、降液、下部停留。默认 30 / 5 / 30 / 5 秒、3 次；快速联调 3 / 1 / 3 / 1 秒、1 次。
- 暂停冻结计时与液量，两泵关闭；继续保留阶段进度；终止记录结果并保持停止位置。
- 降液前段自然回流，降液泵关闭；后段负压加速，降液泵开启。分界时间在设置中配置，液量由压力、水头与阻力计算。
- 20 ms 固定模拟步长，渲染与模拟时钟分离；0.5 / 1 / 2 / 5 / 10 倍速度。
- 原整机 CAD、上下仓实体液体与波纹、环形流道上升水柱和弯月面、顶部溢流水幕、穿粉饼回流水柱、碰撞水滴与粉饼湿润颜色。已移除虚拟线条和移动流点。
- 五个预设视角，左键旋转、右键或中键平移、滚轮缩放、复位、剖视、减少动态效果。
- 命名配方保存、载入、重命名、删除；运行时参数锁定。保存同名配方会更新该配方。
- 独立 JSON 配方、运行历史、模拟 GPIO 与阶段事件；保留最近 500 条历史和 500 条事件，界面各显示最近 80 条。
- 原子替换写入和备份；文件损坏时保留损坏文件并尝试恢复备份。
- 所有关键液量明确标为模拟估算；泵和液量均由一个 `TwinRuntimeState` 驱动。

修改设置后点击“应用并重置液量”才会应用标定与液量配置。先载入配方，再到配方页修改名称并点击“重命名”。运行中可暂停、继续、停止，或调整气压、模拟速度和视角。

## 几何与模拟边界

1 Unity 单位 = 1 米。复用原 `ColdBrewMachine.fbx`，整机高约 0.4 m。

下仓保留原 CAD 轮廓，容积约 **1.5317 L**。上仓由 `Tools/measure_upper.py` 对 FBX 的 `4-3` 内胆逐毫米、32 个方向射线量测。采用 0.253 至 0.374 m 的有效区域，避开冰盖支承件并留 0.4 mm 径向间隙，结果见 `Assets/Resources/UpperCavity.json` 和 `Docs/geometry-measurements.json`。

总液量上限取上下仓有效容积的较小值。下仓、上仓、环形流道三者总液量守恒。流道先充液，到顶后才向上仓溢流；进入降液时，模拟将外侧流道残液即时归入下仓，外侧流道保持无液，仅显示穿粉饼回流。这是离线演示的阶段切换简化。

气压使用一阶响应，流量依据压差、水头与阻力的简化模型计算。低压可停在顶部以下，提高气压会继续上升；阶段结束不会强制转移剩余液量。这不是 CFD，气动响应、流阻和粉饼阻力尚待硬件标定。未模拟冰块位移或粉饼吸水量。泵编号未作为真实接线确认。

`Tools/measure_annular_profile.py` 从原 FBX 采样 32 个方位，生成 `Assets/Resources/AnnularChannel.json` 的 697 个截面。流道沿杯肩和收口的外侧边界变径，中部半径约 59–60 mm。液体厚度限制为 0.63 mm，避免把中间空腔及杯肩整体当成储液环。网格与液量计算共用该截面，采用环形台体积分和体积反算液柱高度；本版流道估算存液约 87.1 mL，修正此前过厚流道造成的 379.3 mL 高估。

杯段在 16,256 个截面方向采样中未穿入内胆或杯壁，检查包括网格的圆周折线误差；底部入口与顶部翻转溢流路径也做了 CAD 面相交抽查。结果见 `Docs/channel-alignment-validation.json`。剖视杯体、流道使用同一剖切平面，流道切面在原间隙内生成，不向外放大。

中部 CAD 存在开口和反向面，连接段按可见边界与密封圈位置作有限近似，尚未验证水密连通。87.1 mL 是本版可视化流道的几何估算，并非实测通道容量；入口和溢流薄片仍属于展示效果，未作为额外液量计算。

## 模型映射

| CAD 编号 / 对象 | 模拟职责 |
| --- | --- |
| `2-1 / 2-3` | 下仓外壳与内胆，参与剖视 |
| `LowerTank / Water` | 下仓量测内腔与液体 |
| `4-3` | 上仓内腔量测来源 |
| `UpperTank_Measured / UpperWater` | 运行时创建的上仓独立液体 |
| `3-3` | 原粉饼 CAD，保持实际几何与位置 |
| `6-1-水泵` | 模拟上液泵 / GPIO15 |
| `6-1-水泵_1` | 模拟降液泵 / GPIO16 |
| `1-1 / 5-1` | 红色磨砂玻璃上下盖座 |
| `1-2 / 5-2` | 最下、最上的黑色圆形橡胶垫 |
| `2-1 / 4-1` | 淡蓝半透明玻璃杯体 |
| `3-6 / 6-4` | 黑色硅胶按钮 / 蓝色电池 |
| `AnnularLiquid_CADGap / AnnularMeniscus` | 环形实体水柱与上升前沿 |
| `UpperOverflowSheet` | 顶部弯折溢流水幕 |
| `PowderReturnWater` | 粉饼与中心阀回流水柱 |

## 代码结构

| 文件 | 职责 |
| --- | --- |
| `Scripts/Control/SequenceRunner.cs` | 纯 C# 四阶段编排与单调时钟 |
| `Scripts/Control/BrewSimulation.cs` | 纯 C# 液量守恒、自然回流、泵互锁 |
| `Scripts/Control/HydraulicModel.cs` | 气压响应、水头、流阻与三处液量 |
| `Scripts/Control/AnnularChannelProfile.cs` | 变截面流道积分、液位反算与渲染共用几何 |
| `Scripts/Control/TwinTypes.cs` | 状态、配方和模拟设置 |
| `Scripts/Control/TwinPersistence.cs` | Unity JSON 存储、备份和恢复 |
| `Scripts/Twin/TwinApplication.cs` | 场景启动与应用命令 |
| `Scripts/Twin/MachineBinder.cs` | CAD 材质、液体、流向、观察相机 |
| `Scripts/Twin/LiquidFlowAnimator.cs` | 实体流道、溢流、回流与碰撞水滴 |
| `Scripts/Twin/TwinStudio.cs` | 展示台、灯光、静态灯箱反射环境 |
| `Scripts/Twin/TwinPanel.cs` | 中文 uGUI 界面 |
| `Scripts/Twin/SimulationSmoke.cs` | 显式命令行启用的播放器验证 |
| `Editor/SimulationBuild.cs` | Windows 构建入口 |

原有协议类型仅作为既存的数据定义保留，没有实现或启用任何硬件传输。

## 数据与构建

运行数据：
`%USERPROFILE%/AppData/LocalLow/OneHistory/ColdBrewTwin/simulation-v1.json`

数据结构升级为 schema 2，自动迁移 schema 1，保留原文件名。在 Unity 选择 `Cold Brew > Build Water Flow Demo`。也可在编辑器关闭时执行：

```powershell
& 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe' -batchmode -quit -projectPath . -executeMethod OneHistory.ColdBrewTwin.Editor.SimulationBuild.BuildWaterFlow -logFile Builds/build.log
```

外观版使用 `Cold Brew > Build Studio Appearance`，批处理入口为 `OneHistory.ColdBrewTwin.Editor.SimulationBuild.BuildStudio`，输出到 `Builds/Studio`。

## 验证

纯 C# 行为验证，不依赖 Unity 或硬件：

```powershell
dotnet run --project Tests/ColdBrewTwin.Tests.csproj
```

安装 Unity 且本工程已完成导入时，可额外检查全部 C# 与 Unity 程序集兼容性：

```powershell
dotnet build Tests/UnityCompile.csproj
```

播放器画面与 UI 验证：

```powershell
& './Builds/WaterFlow/ColdBrewTwin.exe' --flow-smoke -logFile './Builds/flow-player.log'
```

该模式使用隔离数据，检查按钮命中、气压响应、液量守恒、暂停像素冻结、回流与停止。1440×900、1280×720 截图、141 帧动画及报告写入 `Builds/FlowSmoke`。用 Pillow 运行 `Tools/make_flow_preview.py` 可生成 GIF。报告见 `Docs/VALIDATION.md`。

外观版用 `Builds/Studio/ColdBrewTwin.exe --appearance-smoke` 验证，输出到 `Builds/AppearanceSmoke`；用 `Tools/make_flow_preview.py --studio` 生成对应 GIF。额外检查完整外观、剖视开关、视角切换保留剖视状态和材质支持。MSAA 透明叠层的暂停像素比较允许 1 个色阶的舍入误差。

累计 8 小时模拟测试采用加速执行，不等于已经完成 8 小时实际渲染长稳验收。实机通信、只读回放和硬件联调均不在本次范围。

CAD 转换流程与原导入说明保留在 [CAD 导入基线](Docs/CAD-BASELINE.md)。
