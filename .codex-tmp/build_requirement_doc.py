from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_ALIGN_VERTICAL, WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK, WD_LINE_SPACING
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Inches, Pt, RGBColor


ROOT = Path(r"C:\OneHistory\HistoryClio\2026-012-咖啡冷萃机")
OUTPUT = ROOT / "b-Office" / "咖啡冷萃机Unity数字孪生控制需求书.docx"

FONT_CN = "Microsoft YaHei"
FONT_BODY = "SimSun"
NAVY = "20354A"
BLUE = "DCEAF4"
PALE = "F4F7F9"
BORDER = "D9D9D9"
TEXT = RGBColor(0, 0, 0)
MUTED = RGBColor(92, 102, 112)


def set_run_font(run, name=FONT_BODY, size=10.5, bold=False, color=TEXT):
    run.font.name = name
    run._element.get_or_add_rPr().rFonts.set(qn("w:eastAsia"), name)
    run._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), "Aptos")
    run._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), "Aptos")
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.color.rgb = color


def set_cell_shading(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)


def set_cell_margins(cell, top=90, start=110, bottom=90, end=110):
    tc = cell._tc
    tc_pr = tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for margin, value in (("top", top), ("start", start), ("bottom", bottom), ("end", end)):
        node = tc_mar.find(qn(f"w:{margin}"))
        if node is None:
            node = OxmlElement(f"w:{margin}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def set_table_borders(table, color=BORDER, size=6):
    tbl_pr = table._tbl.tblPr
    borders = tbl_pr.first_child_found_in("w:tblBorders")
    if borders is None:
        borders = OxmlElement("w:tblBorders")
        tbl_pr.append(borders)
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        tag = qn(f"w:{edge}")
        element = borders.find(tag)
        if element is None:
            element = OxmlElement(f"w:{edge}")
            borders.append(element)
        element.set(qn("w:val"), "single")
        element.set(qn("w:sz"), str(size))
        element.set(qn("w:space"), "0")
        element.set(qn("w:color"), color)


def set_repeat_table_header(row):
    tr_pr = row._tr.get_or_add_trPr()
    tbl_header = OxmlElement("w:tblHeader")
    tbl_header.set(qn("w:val"), "true")
    tr_pr.append(tbl_header)


def set_cell_width(cell, width_inches):
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_w = tc_pr.find(qn("w:tcW"))
    if tc_w is None:
        tc_w = OxmlElement("w:tcW")
        tc_pr.append(tc_w)
    tc_w.set(qn("w:w"), str(int(width_inches * 1440)))
    tc_w.set(qn("w:type"), "dxa")


def set_keep_with_next(paragraph, value=True):
    p_pr = paragraph._p.get_or_add_pPr()
    keep = p_pr.find(qn("w:keepNext"))
    if value and keep is None:
        p_pr.append(OxmlElement("w:keepNext"))
    elif not value and keep is not None:
        p_pr.remove(keep)


def set_keep_together(paragraph):
    p_pr = paragraph._p.get_or_add_pPr()
    if p_pr.find(qn("w:keepLines")) is None:
        p_pr.append(OxmlElement("w:keepLines"))


def add_field(run, instruction):
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = instruction
    separate = OxmlElement("w:fldChar")
    separate.set(qn("w:fldCharType"), "separate")
    text = OxmlElement("w:t")
    text.text = "1"
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    run._r.extend([begin, instr, separate, text, end])


def configure_styles(doc):
    styles = doc.styles
    normal = styles["Normal"]
    normal.font.name = FONT_BODY
    normal._element.rPr.rFonts.set(qn("w:eastAsia"), FONT_BODY)
    normal.font.size = Pt(10.5)
    normal.font.color.rgb = TEXT
    normal.paragraph_format.line_spacing = 1.35
    normal.paragraph_format.space_after = Pt(5.5)
    normal.paragraph_format.widow_control = True

    title = styles["Title"]
    title.font.name = FONT_CN
    title._element.rPr.rFonts.set(qn("w:eastAsia"), FONT_CN)
    title.font.size = Pt(25)
    title.font.bold = True
    title.font.color.rgb = TEXT
    title.paragraph_format.space_after = Pt(16)
    title_p_pr = title._element.get_or_add_pPr()
    title_border = title_p_pr.find(qn("w:pBdr"))
    if title_border is not None:
        title_p_pr.remove(title_border)

    for name, size, before, after in (
        ("Heading 1", 16, 18, 8),
        ("Heading 2", 12.5, 13, 5),
        ("Heading 3", 11, 9, 4),
    ):
        style = styles[name]
        style.font.name = FONT_CN
        style._element.rPr.rFonts.set(qn("w:eastAsia"), FONT_CN)
        style.font.color.rgb = TEXT
        style.font.bold = True
        style.font.size = Pt(size)
        style.paragraph_format.space_before = Pt(before)
        style.paragraph_format.space_after = Pt(after)
        style.paragraph_format.keep_with_next = True

    styles["List Bullet"].font.name = FONT_BODY
    styles["List Bullet"]._element.rPr.rFonts.set(qn("w:eastAsia"), FONT_BODY)
    styles["List Bullet"].font.size = Pt(10.5)
    styles["List Number"].font.name = FONT_BODY
    styles["List Number"]._element.rPr.rFonts.set(qn("w:eastAsia"), FONT_BODY)
    styles["List Number"].font.size = Pt(10.5)


def add_para(doc, text="", bold_lead=None, style=None, align=None):
    p = doc.add_paragraph(style=style)
    if bold_lead and text.startswith(bold_lead):
        first = p.add_run(bold_lead)
        set_run_font(first, FONT_BODY, 10.5, True)
        rest = p.add_run(text[len(bold_lead):])
        set_run_font(rest)
    else:
        r = p.add_run(text)
        set_run_font(r)
    if align is not None:
        p.alignment = align
    set_keep_together(p)
    return p


def add_bullets(doc, items):
    for item in items:
        p = doc.add_paragraph(style="List Bullet")
        r = p.add_run(item)
        set_run_font(r)
        p.paragraph_format.space_after = Pt(2.5)
        set_keep_together(p)


def add_numbered(doc, items):
    for index, item in enumerate(items, start=1):
        p = doc.add_paragraph()
        p.paragraph_format.left_indent = Cm(0.75)
        p.paragraph_format.first_line_indent = Cm(-0.55)
        number = p.add_run(f"{index}. ")
        set_run_font(number)
        text = p.add_run(item)
        set_run_font(text)
        p.paragraph_format.space_after = Pt(2.5)
        set_keep_together(p)


def add_table(doc, headers, rows, widths=None, font_size=9.2, center_cols=None):
    table = doc.add_table(rows=1, cols=len(headers))
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    set_table_borders(table)
    center_cols = set(center_cols or [])

    for idx, header in enumerate(headers):
        cell = table.rows[0].cells[idx]
        set_cell_shading(cell, NAVY)
        set_cell_margins(cell)
        cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_after = Pt(0)
        r = p.add_run(str(header))
        set_run_font(r, FONT_CN, font_size, True, RGBColor(255, 255, 255))
        if widths:
            set_cell_width(cell, widths[idx])
    set_repeat_table_header(table.rows[0])

    for row_idx, values in enumerate(rows):
        cells = table.add_row().cells
        if row_idx % 2 == 1:
            for cell in cells:
                set_cell_shading(cell, PALE)
        for col_idx, value in enumerate(values):
            cell = cells[col_idx]
            set_cell_margins(cell)
            cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
            if widths:
                set_cell_width(cell, widths[col_idx])
            p = cell.paragraphs[0]
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER if col_idx in center_cols else WD_ALIGN_PARAGRAPH.LEFT
            p.paragraph_format.space_after = Pt(0)
            p.paragraph_format.line_spacing = 1.15
            r = p.add_run(str(value))
            set_run_font(r, FONT_BODY, font_size)
    spacer = doc.add_paragraph()
    spacer.paragraph_format.space_after = Pt(1)
    return table


def add_heading(doc, text, level=1):
    p = doc.add_paragraph(style=f"Heading {level}")
    r = p.add_run(text)
    set_run_font(r, FONT_CN, {1: 16, 2: 12.5, 3: 11}[level], True)
    set_keep_with_next(p)
    return p


def add_page_break(doc):
    p = doc.add_paragraph()
    p.add_run().add_break(WD_BREAK.PAGE)


doc = Document()
section = doc.sections[0]
section.page_width = Inches(8.5)
section.page_height = Inches(11)
section.top_margin = Inches(0.72)
section.bottom_margin = Inches(0.7)
section.left_margin = Inches(0.78)
section.right_margin = Inches(0.72)
section.header_distance = Inches(0.3)
section.footer_distance = Inches(0.3)

configure_styles(doc)

# Cover
p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(76)
p.paragraph_format.space_after = Pt(8)
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = p.add_run("咖啡冷萃机")
set_run_font(r, FONT_CN, 15, True, MUTED)

p = doc.add_paragraph(style="Title")
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = p.add_run("Unity 数字孪生控制需求书")
set_run_font(r, FONT_CN, 25, True)

p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(44)
r = p.add_run("面向独立控制 真机通信 仿真演示和研发验收")
set_run_font(r, FONT_CN, 12, False, MUTED)

meta_rows = [
    ("文档版本", "V1.0"),
    ("目标平台", "Unity 2022.3.62f3c1  Windows"),
    ("适用阶段", "数字孪生首版开发及真机联调"),
    ("编制日期", "2026 年 9 月 5 日"),
    ("项目目录", "2026-012 咖啡冷萃机"),
]
table = doc.add_table(rows=0, cols=2)
table.alignment = WD_TABLE_ALIGNMENT.CENTER
table.autofit = False
set_table_borders(table)
for i, (label, value) in enumerate(meta_rows):
    cells = table.add_row().cells
    set_cell_width(cells[0], 1.55)
    set_cell_width(cells[1], 4.7)
    set_cell_shading(cells[0], BLUE)
    for cell in cells:
        set_cell_margins(cell, 120, 140, 120, 140)
        cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
    p0 = cells[0].paragraphs[0]
    p0.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r0 = p0.add_run(label)
    set_run_font(r0, FONT_CN, 10, True)
    p1 = cells[1].paragraphs[0]
    r1 = p1.add_run(value)
    set_run_font(r1, FONT_BODY, 10)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(54)
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = p.add_run("本文件定义 Unity 数字孪生的控制边界 数据映射 视觉行为和验收标准")
set_run_font(r, FONT_BODY, 9.5, False, MUTED)

add_page_break(doc)

# Header/footer from page 2 onward, shared section.
header = section.header
hp = header.paragraphs[0]
hp.text = "咖啡冷萃机 Unity 数字孪生控制需求书"
hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
for run in hp.runs:
    set_run_font(run, FONT_CN, 8.5, False, MUTED)
footer = section.footer
fp = footer.paragraphs[0]
fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
fr = fp.add_run("第 ")
set_run_font(fr, FONT_BODY, 8.5, False, MUTED)
add_field(fr, "PAGE")
fr2 = fp.add_run(" 页  共 ")
set_run_font(fr2, FONT_BODY, 8.5, False, MUTED)
add_field(fr2, "NUMPAGES")
fr3 = fp.add_run(" 页")
set_run_font(fr3, FONT_BODY, 8.5, False, MUTED)

add_heading(doc, "1 文档目的和结论", 1)
add_para(doc, "本需求书用于指导咖啡冷萃机 Unity 数字孪生的设计、开发、联调和验收。读者包括 Unity 开发人员、嵌入式开发人员和设备测试人员。首版数字孪生应复用现有三维整机模型，在 Unity 内新建完整控制界面、业务状态机、配方编排、历史记录和串口通信，并同时支持离线模拟与 ESP32 真机控制。")
add_para(doc, "首版架构结论：", bold_lead="首版架构结论：")
add_bullets(doc, [
    "Unity 独立承担控制界面、配方编排、真机通信和三维表现，直接独占 ESP32 串口。运行时不依赖现有 WPF 上位机。",
    "现有上位机软件只提供协议字段、参数范围、安全规则和交互流程参考；Unity 不复用其代码、界面组件、进程间接口或持久化文件。",
    "设备 state 快照决定真机模式下的泵和流向显示。ack 只表示设备处理了命令，不能直接驱动完成态动画。",
    "由于现机没有液位、温度、压力和门盖传感器，液位只能由标定时间估算，并必须持续标注为估算值。",
    "任何断连、超时、非法报文或 GPIO15 与 GPIO16 同时有效的状态都必须停止动态效果并进入明确的异常显示。",
])

add_heading(doc, "2 文档控制", 1)
add_table(doc, ["项目", "内容"], [
    ("需求基线", "串口协议 proto 1、上位机 0.4.0 M4 的行为参考、产品结构汇报、现有 ColdBrewTwin Unity 工程"),
    ("需求状态", "首版开发基线"),
    ("变更规则", "通信字段、控制权限、安全规则和验收指标变更时必须更新版本记录"),
    ("优先级", "P0 为首版必须完成，P1 为联调后增强，P2 为后续扩展"),
], widths=[1.45, 5.0], font_size=9.5, center_cols=[0])

add_page_break(doc)

add_heading(doc, "3 术语", 1)
add_table(doc, ["术语", "定义"], [
    ("上液", "GPIO15 有效，下部液仓加压，液体沿环形流道进入上部冰块仓"),
    ("降液", "GPIO16 有效，上部液体经粉饼回流至下部液仓；气泵抽气形成负压以加速下流"),
    ("参考上位机", "现有 C# WPF AppShell 应用，仅用于提取已验证的协议和控制行为，不参与 Unity 运行"),
    ("数字孪生", "Unity 中与设备结构、状态和控制行为对应的实时三维模型及操作界面"),
    ("设备状态", "ESP32 通过 state 报文返回的 mode、GPIO、序号、运行时间和可选故障码"),
    ("估算液位", "按标定转移时间和运行阶段推算的液量，不代表传感器实测"),
], widths=[1.35, 5.1], font_size=9.2, center_cols=[0])

add_heading(doc, "4 产品过程和范围", 1)
add_heading(doc, "4.1 产品物理过程", 2)
add_numbered(doc, [
    "上液阶段：气泵向下部液仓加压，液体沿机身环形流道向上进入冰块仓。液体全部上升后，上部空间对外连通并降低压力。",
    "自然回流阶段：上部冰块仓与下部液仓连通，液体在重力作用下穿过咖啡粉饼向下回流，直至液面到达结构连通边界。",
    "负压加速阶段：当液面到达边界后，降液泵抽取下部液仓空气，使下部形成负压并加快剩余液体下流。液体全部回到下部液仓后完成一次循环。",
])
add_para(doc, "Unity 应表现这三个连续物理阶段。控制状态机把一次循环抽象为上液、上液停留、降液、降液停留四个阶段；Unity 需要在这一控制抽象内呈现自然回流和负压加速的视觉差异。")

add_heading(doc, "4.2 首版范围", 2)
add_table(doc, ["范围", "首版要求", "优先级"], [
    ("三维模型", "整机、上部冰块仓、中部电器仓、下部液仓、粉仓、两泵和环形流道可识别", "P0"),
    ("液体表现", "上液、自然回流、负压加速、停留和停止状态可视化", "P0"),
    ("控制", "手动上液、手动降液、停止、固定循环、暂停、继续和终止", "P0"),
    ("真机通信", "Unity 直接枚举、连接和控制 ESP32 串口，并处理设备状态快照", "P0"),
    ("记录", "Unity 独立保存配方、运行历史、GPIO 事件和故障记录", "P1"),
    ("演示", "脱离设备时可运行确定性的模拟模式", "P0"),
], widths=[1.15, 4.65, 0.65], font_size=9.2, center_cols=[0, 2])

add_heading(doc, "4.3 首版排除项", 2)
add_bullets(doc, [
    "不把估算值描述为液位、温度、压力或流量传感器实测。",
    "不由 Unity 直接写 GPIO，也不修改 ESP32 固件协议。",
    "不实现尚未确认的温控、压力控制、门盖检测和电池管理控制。",
    "不以视觉动画替代硬件互锁、硬件急停或设备端故障保护。",
])

add_heading(doc, "5 系统架构和数据权威", 1)
add_heading(doc, "5.1 目标架构", 2)
add_para(doc, "Unity 采用单进程控制链。控制界面只产生操作意图，Unity 内部控制器判断连接、配方锁定、互锁和命令合法性后，再通过串口协议向 ESP32 下发。ESP32 state 快照进入统一状态仓库并驱动全部三维表现和界面。")
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_before = Pt(8)
p.paragraph_format.space_after = Pt(10)
r = p.add_run("Unity 控制界面  ->  CommandController  ->  DeviceProtocolClient  ->  SerialTransport  ->  ESP32\nESP32 state  ->  SerialTransport  ->  DeviceProtocolClient  ->  TwinStateStore  ->  Unity 模型与界面")
set_run_font(r, "Consolas", 9.5, False)

add_heading(doc, "5.2 组件职责", 2)
add_table(doc, ["组件", "职责", "禁止事项"], [
    ("Unity Control UI", "提供连接、手动控制、固定循环、配方、历史、诊断和视角操作", "不得直接写串口或直接修改 GPIO 显示"),
    ("CommandController", "执行参数校验、互锁、编排、暂停、终止和命令状态跟踪", "不得把按钮点击认定为设备已动作"),
    ("TwinStateStore", "维护连接、模式、阶段、进度、GPIO、估算液位和故障的唯一状态", "不得让不同面板各自维护状态"),
    ("DeviceProtocolClient", "处理 proto 1 消息、握手、心跳、命令关联、快照序号和设备重启", "不得把 ack 当作实际输出状态"),
    ("SerialTransport", "独占 COM 口并在后台收发 115200 UTF 8 JSON Lines", "不得在 Unity 主线程阻塞读写"),
    ("ESP32", "执行泵控制、方向切换和设备端安全保护，返回实际 GPIO 快照", "不得依赖 Unity 动画保证安全"),
], widths=[1.4, 3.05, 2.0], font_size=8.8, center_cols=[0])

add_heading(doc, "5.3 运行模式", 2)
add_table(doc, ["模式", "数据来源", "允许操作", "界面标识"], [
    ("离线模拟", "Unity 本地模拟器", "全部模拟控制和配方编排", "始终显示 模拟数据"),
    ("真机控制", "ESP32 state 快照和 Unity 编排", "仅在线且状态可信时允许", "显示 设备 在线 COMx"),
    ("回放", "历史事件文件或会话缓存", "只读，不允许控制", "显示 历史回放"),
], widths=[1.15, 1.95, 2.25, 1.1], font_size=9.0, center_cols=[0, 3])

add_heading(doc, "6 状态模型和数据规则", 1)
add_heading(doc, "6.1 状态集合", 2)
add_table(doc, ["类别", "状态", "Unity 行为"], [
    ("连接", "Offline Connecting Online Timeout Disconnected Error", "更新状态栏；非 Online 时阻断真机动作"),
    ("设备", "Idle Up Down Paused Complete Fault", "驱动泵、流向、颜色、动效和按钮可用性"),
    ("循环", "None Up UpDwell Down DownDwell Complete Aborted", "驱动当前步骤、循环序号、时间和液位估算"),
    ("请求", "Idle Pending Acknowledged Applied Rejected TimedOut", "区分已发送、已确认和已生效"),
    ("数据质量", "Unknown Estimated Fresh Stale Faulted", "决定数值标签和降级显示"),
], widths=[1.0, 2.75, 2.7], font_size=9.0, center_cols=[0])

add_heading(doc, "6.2 状态优先级", 2)
add_numbered(doc, [
    "真机模式下，最新且序号有效的 state 快照优先于 Unity 预测、按钮状态和 ack。",
    "Unity SequenceRunner 决定当前循环、步骤、已用时间和剩余时间；真机 GPIO 状态仍由设备快照决定。",
    "协议没有液位字段时，Unity 可按标定时间估算液位，但显示标签必须为估算。",
    "当状态超过 3 秒未更新时，Unity 把设备数据标记为过期并停止继续推算真机运动。",
    "收到新的 hello 或检测到 uptime 回退时，Unity 清除旧命令关联和旧序号，等待新快照。",
])

add_heading(doc, "6.3 数据一致性", 2)
add_bullets(doc, [
    "理想模拟中，上部与下部液量之和保持不变；数值限于 0 至配置容量。",
    "GPIO15 与 GPIO16 不得同时激活。等待、暂停、停止、完成和离线时两路均应关闭。",
    "Unity 每帧插值仅用于视觉平滑，不能回写或篡改权威状态。",
    "新消息的 sequence 小于或等于已应用 sequence 时应丢弃；设备重启场景按 hello 或 uptime 重置处理。",
    "阶段与剩余时间采用 Unity 单调计时；系统时钟变化不得让剩余时间倒退或跳增。",
])

add_heading(doc, "7 三维模型和视觉行为", 1)
add_heading(doc, "7.1 几何与坐标", 2)
add_table(doc, ["编号", "要求", "验收依据"], [
    ("VIS 01", "沿用现有 ColdBrewMachine 整机 FBX，1 Unity 单位等于 1 米，整机高度约 0.4 米", "场景包围盒与 CAD 基线一致"),
    ("VIS 02", "上部液仓、中部电器仓、下部液仓保持轴向对称与真实相对位置", "正视和剖视对照产品汇报"),
    ("VIS 03", "液体只出现在容器内腔与封闭流道，不填充电器仓", "任一运行状态无穿模和错误灌注"),
    ("VIS 04", "现有下部 TankCavity 的满仓容积 1.5317 L 作为当前几何基线", "同一输入体积生成稳定液面"),
    ("VIS 05", "上部内腔和粉饼区域应建立独立权威几何，不能仅按外壳包围盒生成液体", "不同液位下液体均位于内壁内"),
], widths=[0.85, 4.05, 1.55], font_size=8.9, center_cols=[0])

add_heading(doc, "7.2 运行状态表现", 2)
add_table(doc, ["状态", "模型行为", "颜色与文字"], [
    ("上液", "下部液位下降，上部液位上升；环形流道由下向上运动；上液泵运行", "黄色 上液 下舱到上舱"),
    ("上液停留", "泵停止；上部保持液位；冰块与咖啡接触过程保持可观察", "琥珀色 上部停留"),
    ("降液前段", "液体从上部穿过粉饼自然向下；降液泵可按控制状态延后表现", "蓝色 自然回流"),
    ("降液后段", "降液泵运行；下部负压效果加速剩余回流；下部液位上升", "蓝色 负压加速"),
    ("暂停", "流动和计时冻结，两泵关闭，保留当前位置", "琥珀色 已暂停"),
    ("完成", "两泵关闭，短暂完成反馈后返回待机", "绿色 循环完成"),
    ("故障", "停止流动动效；保留最后可信液位；突出故障部位", "红色 故障摘要"),
    ("离线", "停止真机预测，降低非关键信息饱和度", "设备未连接"),
], widths=[1.0, 3.95, 1.5], font_size=8.7, center_cols=[0])

add_heading(doc, "7.3 观察与交互", 2)
add_bullets(doc, [
    "提供整机、上部液仓、粉饼与流道、中部泵组、下部液仓五个预设视角。",
    "支持鼠标旋转、平移、缩放和一键复位视角，操作不改变设备状态。",
    "支持外壳透明度或剖切模式，使液体、粉饼和管路在不隐藏关键结构的情况下可观察。",
    "提供减少动态效果选项。关闭粒子或流光后，方向箭头、液位和状态文字仍应完整。",
])

add_heading(doc, "8 控制和编排需求", 1)
add_heading(doc, "8.1 手动控制", 2)
add_table(doc, ["控件", "前置条件", "执行结果", "禁用条件"], [
    ("上液", "模拟模式或真机 Online 且无活动编排", "请求 up；等待 state 后显示实际上液", "故障、请求中、参数锁定或真机离线"),
    ("降液", "模拟模式或真机 Online 且无活动编排", "请求 down；等待 state 后显示实际降液", "故障、请求中、参数锁定或真机离线"),
    ("停止", "任何非待机状态", "请求 stop；立即停止 Unity 预测动效并等待 state", "仅可在已确认安全待机后弱化"),
    ("故障复位", "故障原因已消除且两路关闭", "清除 Unity 锁定并请求最新 state", "任一 GPIO 仍激活或通讯不可信"),
], widths=[0.9, 2.0, 2.25, 1.3], font_size=8.7, center_cols=[0])

add_heading(doc, "8.2 固定循环", 2)
add_table(doc, ["参数", "有效范围", "首选值", "说明"], [
    ("上液时间", "1 至 3600 秒", "30 秒", "下部到上部的标定运行时间"),
    ("上液停留", "0 至 3600 秒", "5 秒", "上液完成后的停留时间"),
    ("降液时间", "1 至 3600 秒", "30 秒", "上部到下部的标定运行时间"),
    ("降液停留", "0 至 3600 秒", "5 秒", "一次循环结束前的停留时间"),
    ("循环次数", "1 至 999 次", "3 次", "每次包含四个阶段"),
], widths=[1.2, 1.45, 1.0, 2.8], font_size=9.0, center_cols=[0, 1, 2])
add_para(doc, "默认值与现有 M4 默认冷萃配方保持一致。快速联调配方为 3 秒、1 秒、3 秒、1 秒、1 次。运行开始后 Unity 锁定本次配方快照，外部参数修改只影响下一次运行。")

add_heading(doc, "8.3 编排行为", 2)
add_numbered(doc, [
    "启动后按上液、上液停留、降液、降液停留顺序执行并重复指定次数。",
    "暂停时冻结阶段计时，关闭两路输出，并保留当前循环和阶段已用时间。",
    "继续时从暂停阶段和已用时间恢复，不重复已完成步骤。",
    "终止时关闭两路输出，标记 Aborted，记录原因并返回安全待机。",
    "设备模式运行期间若连接不再为 Online，Unity 立即终止编排、关闭预测动效并进入通信异常状态。",
])

add_heading(doc, "8.4 配方和历史", 2)
add_bullets(doc, [
    "Unity 独立提供命名配方的创建、保存、载入、重命名、删除和运行，不读取或修改现有上位机的配方文件。",
    "Unity 本地最多保留 500 条历史，界面显示最近 80 条；存储文件放入 Unity 应用数据目录并支持损坏恢复。",
    "历史事件至少包含时间、类型、状态、GPIO15、GPIO16、配方名和结果摘要。",
])

add_heading(doc, "9 通信接口需求", 1)
add_heading(doc, "9.1 设备串口基线", 2)
add_table(doc, ["项目", "要求"], [
    ("链路", "USB CDC 或 UART 串口，115200 波特率，UTF 8"),
    ("帧", "每行一个 JSON 对象，以换行符结束"),
    ("版本", "proto 1"),
    ("心跳", "Unity 每 1 秒发送 ping；连续 3 秒未收到设备数据则进入 Timeout"),
    ("请求", "hello、ping、state、set mode up down stop"),
    ("响应", "hello、pong、ack、state、error"),
], widths=[1.25, 5.2], font_size=9.3, center_cols=[0])

add_heading(doc, "9.2 Unity 串口协议实现", 2)
add_para(doc, "Unity 独立实现 proto 1 客户端。打开串口后先清空收发缓冲区并发送 hello，握手成功后请求完整 state。接收器应按换行符组帧，保留未完成半包，并对每个完整 JSON 对象做字段和类型校验。")
add_table(doc, ["方向", "消息", "最低字段", "Unity 处理"], [
    ("Unity 到设备", "hello", "id cmd app proto", "app 使用 cold-brew-unity，proto 为 1"),
    ("Unity 到设备", "ping", "id cmd", "在线时每 1 秒发送"),
    ("Unity 到设备", "state", "id cmd", "握手、重连和人工刷新后请求"),
    ("Unity 到设备", "set", "id cmd mode", "mode 仅允许 up down stop"),
    ("设备到 Unity", "hello", "type id device proto", "校验版本后进入 Online 并请求 state"),
    ("设备到 Unity", "pong", "type id", "更新时间，不改变实际 GPIO"),
    ("设备到 Unity", "ack", "type id cmd ok", "更新命令确认状态，不直接播放泵动作"),
    ("设备到 Unity", "state", "type seq mode gpio15 gpio16 uptime", "更新权威设备状态"),
    ("设备到 Unity", "error", "type id code message", "关联命令并显示设备拒绝原因"),
], widths=[1.25, 0.9, 2.3, 2.0], font_size=8.2, center_cols=[0, 1])

add_heading(doc, "9.3 状态字段映射", 2)
add_table(doc, ["来源字段", "Unity 字段", "显示或行为"], [
    ("connectionStatus", "connection", "Offline Connecting Online Timeout Disconnected Error"),
    ("runtimeMode", "sourceMode", "simulation hardware playback"),
    ("mode", "machineMode", "Idle Up Down Paused Complete Fault"),
    ("phase", "cyclePhase", "None Up UpDwell Down DownDwell Complete Aborted"),
    ("gpio15", "pumpUpActive", "上液泵、黄色状态、上行流向"),
    ("gpio16", "pumpDownActive", "降液泵、蓝色状态、下行流向"),
    ("seq uptime", "deviceSequence deviceUptimeMs", "去重并识别设备重启"),
    ("upperLevel lowerLevel", "estimatedFill", "仅在来源标记为 Estimated 时显示百分比"),
], widths=[2.0, 2.1, 2.35], font_size=8.7, center_cols=[0, 1])

add_heading(doc, "9.4 连接与恢复", 2)
add_bullets(doc, [
    "Unity 启动后先显示离线，不恢复任何危险动作。连接成功后请求完整快照。",
    "串口意外断开后采用受控重连。重新握手成功前禁止真机控制，不沿用旧 Pending 命令。",
    "非法 JSON、未知消息类型和不兼容版本只记录诊断，不得导致主线程崩溃。",
    "所有网络或管道接收在后台执行，状态应用回到 Unity 主线程；每帧不得阻塞等待消息。",
])

add_page_break(doc)

add_heading(doc, "10 安全 故障和降级", 1)
add_heading(doc, "10.1 安全不变量", 2)
add_table(doc, ["编号", "不变量", "Unity 响应"], [
    ("SAFE 01", "GPIO15 与 GPIO16 不得同时有效", "立即进入 Fault，停止所有流动动效，显示互锁故障"),
    ("SAFE 02", "方向切换必须经过两路关闭状态", "未观察到停止快照前保持请求中，不提前反向播放"),
    ("SAFE 03", "真机动作必须来自最新可信 state", "ack 到达而 state 未到达时不点亮执行态"),
    ("SAFE 04", "断连、超时或程序退出不得自动恢复输出", "进入离线或超时，停止预测并清空未确认控制"),
    ("SAFE 05", "故障不能被上液或降液命令覆盖", "仅保留停止、请求状态和满足条件后的复位"),
    ("SAFE 06", "软件停止不等同于硬件急停", "界面使用 软件停止 文案，不宣称切断电源"),
], widths=[0.9, 3.4, 2.15], font_size=8.8, center_cols=[0])

add_heading(doc, "10.2 故障显示", 2)
add_table(doc, ["故障", "触发条件", "恢复条件"], [
    ("互锁故障", "gpio15 和 gpio16 同时为 true", "设备回报两路关闭，操作者确认后复位"),
    ("通信超时", "3 秒未收到设备数据", "重新握手并取得新 state"),
    ("命令未确认", "命令发出后未在限定时间取得 ack 或对应 state", "收到匹配结果或操作者停止"),
    ("协议错误", "JSON 非法、字段类型错误、版本不支持", "忽略坏帧；连续错误超过阈值时重连"),
    ("状态过期", "串口仍打开但全量状态未更新", "收到序号更新的完整快照"),
    ("模型异常", "液位越界、几何缺失或映射对象丢失", "停止相应动画并写入 Unity 诊断日志"),
], widths=[1.3, 2.95, 2.2], font_size=8.8, center_cols=[0])

add_heading(doc, "10.3 日志要求", 2)
add_bullets(doc, [
    "记录连接变化、命令编号、命令名、参数摘要、接收时间、确认时间、最终状态和拒绝原因。",
    "生产显示不输出完整高频报文；诊断模式可查看原始消息，但不得阻塞渲染。",
    "日志时间使用本地可读时间并保留毫秒，状态关联使用 sequence、command id 和 session。",
])

add_heading(doc, "11 非功能需求", 1)
add_table(doc, ["类别", "要求", "验收指标"], [
    ("性能", "普通办公显卡上保持流畅交互，消息处理与动画解耦", "目标 60 FPS；标准验收场景不低于 30 FPS"),
    ("时延", "Unity 在收到本机状态后及时更新关键状态", "95% 状态更新在 100 ms 内可见"),
    ("稳定性", "连续运行固定循环与重连测试期间无崩溃和状态漂移", "8 小时演示运行无未处理异常"),
    ("确定性", "同一模拟种子、配方和初始液量产生一致过程", "重复 10 次终态和事件顺序一致"),
    ("可配置", "接口地址、端口、标定时间、容量和动效强度配置化", "无需重新编译即可修改"),
    ("可测试", "状态映射、互锁、序号处理和液量计算可单元测试", "核心逻辑脱离场景对象执行"),
    ("可用性", "颜色之外提供文字、图标和方向；按钮状态可预测", "色觉模拟下仍可识别所有关键状态"),
    ("可维护", "通信、状态、控制、模型表现和 UI 分层", "无 MonoBehaviour 直接读串口或持有双重真相"),
    ("兼容", "与现有 Unity 2022.3.62f3c1 和内置渲染管线兼容", "工程无升级提示并可正常构建"),
], widths=[1.0, 3.6, 1.85], font_size=8.7, center_cols=[0])

add_heading(doc, "12 Unity 软件结构", 1)
add_table(doc, ["模块", "建议类型", "职责"], [
    ("TwinState", "纯 C# 数据对象", "保存连接、模式、阶段、GPIO、进度、液量、质量和故障"),
    ("TwinStateReducer", "纯 C# 服务", "校验版本和序号，把输入消息归并为唯一状态"),
    ("TwinTransport", "接口及实现", "SerialTransport、SimulationTransport、PlaybackTransport"),
    ("DeviceProtocolClient", "服务", "发送带 id 的设备命令并跟踪 Pending 到终态"),
    ("BrewSimulation", "纯 C# 服务", "按配方、标定时间和容量生成确定性模拟"),
    ("MachineBinder", "MonoBehaviour", "把 TwinState 映射到泵、材质、粒子、文字和视角"),
    ("LiquidSystem", "组件组", "管理上下液仓网格、流道和液量守恒"),
    ("TwinPanel", "UI Toolkit 或 uGUI", "显示状态、控制、配方、进度、告警和数据来源"),
    ("TwinDiagnostics", "服务及面板", "记录协议、帧率、状态序号、丢帧和模型映射错误"),
], widths=[1.5, 1.55, 3.4], font_size=8.8, center_cols=[0, 1])

add_heading(doc, "13 功能需求清单", 1)
functional_rows = [
    ("FR 01", "启动", "加载 ColdBrewTwin 场景和整机模型，默认不激活任何泵", "P0"),
    ("FR 02", "模式", "支持离线模拟、真机控制和只读回放", "P0"),
    ("FR 03", "连接", "Unity 可枚举串口并显示设备连接、握手、超时和错误状态", "P0"),
    ("FR 04", "状态", "使用单一 TwinState 驱动全部视图和模型行为", "P0"),
    ("FR 05", "上液", "GPIO15 或模拟 up 状态驱动上行液体与上液泵", "P0"),
    ("FR 06", "降液", "GPIO16 或模拟 down 状态驱动回流与降液泵", "P0"),
    ("FR 07", "停留", "两路关闭时保持液位并显示相应停留阶段", "P0"),
    ("FR 08", "停止", "停止操作始终可达并清除 Unity 预测运动", "P0"),
    ("FR 09", "互锁", "检测双路同时有效并锁定为故障", "P0"),
    ("FR 10", "请求", "显示 Pending、Acknowledged、Applied、Rejected 和 TimedOut", "P0"),
    ("FR 11", "编排", "执行并显示四阶段固定循环和当前循环序号", "P0"),
    ("FR 12", "暂停", "冻结时间和动效且保持恢复位置", "P0"),
    ("FR 13", "终止", "关闭输出、记录原因并回到安全待机", "P0"),
    ("FR 14", "参数", "执行与 M4 一致的范围校验和运行中锁定", "P0"),
    ("FR 15", "配方", "列出、载入和运行命名配方", "P1"),
    ("FR 16", "液位", "按上下内腔重建液体并保持模拟液量守恒", "P0"),
    ("FR 17", "来源", "每个液位与关键状态显示设备、估算、模拟或回放来源", "P0"),
    ("FR 18", "视角", "提供预设视角、自由观察和复位", "P0"),
    ("FR 19", "剖视", "支持观察粉饼、流道、泵组和内部液体", "P1"),
    ("FR 20", "降级", "超时和断连时停止预测并保留最后可信液位", "P0"),
    ("FR 21", "重连", "重连后以完整快照覆盖旧状态并清除旧命令", "P0"),
    ("FR 22", "历史", "Unity 独立保存并显示运行、GPIO、通信和故障事件", "P1"),
    ("FR 23", "诊断", "显示协议版本、会话、最新序号、延迟和错误计数", "P1"),
    ("FR 24", "可访问", "关闭动效后仍通过文字、图标和方向识别状态", "P0"),
]
add_table(doc, ["编号", "主题", "需求", "级别"], functional_rows, widths=[0.75, 0.9, 4.25, 0.55], font_size=8.2, center_cols=[0, 1, 3])

add_page_break(doc)

add_heading(doc, "14 验收场景", 1)
acceptance_rows = [
    ("AT 01", "冷启动", "不连接设备启动 Unity", "显示离线和模拟入口；两泵关闭；无自动动作"),
    ("AT 02", "模拟上液", "初始液量有效时执行上液", "下部下降、上部上升、上行流道可见且总液量守恒"),
    ("AT 03", "模拟降液", "执行降液", "上部下降、下部上升，自然回流和负压加速阶段可区分"),
    ("AT 04", "固定循环", "运行默认冷萃配方 3 次", "12 步顺序正确，最终两路关闭，状态返回待机"),
    ("AT 05", "暂停继续", "在任一运行阶段暂停 5 秒后继续", "液位和计时冻结，继续后不重复前一步"),
    ("AT 06", "终止", "运行中点击终止", "两路关闭，显示已终止并记录原因"),
    ("AT 07", "真机上液", "发送 up，先收到 ack 后收到 state up", "ack 阶段只显示已确认；state 后才播放上液"),
    ("AT 08", "命令拒绝", "收到 ok false 或 error", "不改变实际泵状态，显示拒绝原因"),
    ("AT 09", "互锁", "注入 gpio15 true 和 gpio16 true 的快照", "一帧内停止流动并进入 Fault"),
    ("AT 10", "超时", "真机运行时 3 秒无设备数据", "显示 Timeout，停止预测，不显示正常完成"),
    ("AT 11", "设备重启", "uptime 回退并收到新 hello", "清除旧序号和未确认命令，等待新 state"),
    ("AT 12", "旧包", "重复或乱序发送 state", "不回退已应用状态；诊断记录丢弃数量"),
    ("AT 13", "坏包", "连续发送半包和非法 JSON", "程序不崩溃；坏包记日志；后续正确包可恢复"),
    ("AT 14", "视觉", "检查 0%、50%、100% 液位和全部视角", "无穿模、悬空、液体越界或错误填充电器仓"),
    ("AT 15", "性能", "标准场景连续运行 30 分钟", "不低于 30 FPS，无持续增长的内存或消息积压"),
    ("AT 16", "长稳", "模拟或设备回放连续运行 8 小时", "无未处理异常、状态漂移和错误自动恢复"),
]
add_table(doc, ["编号", "场景", "步骤或输入", "预期结果"], acceptance_rows, widths=[0.75, 1.0, 2.25, 2.45], font_size=8.2, center_cols=[0, 1])

add_page_break(doc)

add_heading(doc, "15 分阶段交付", 1)
add_table(doc, ["阶段", "交付内容", "退出条件"], [
    ("U0 状态骨架", "TwinState、模拟传输、命令模型、诊断日志和基础 UI", "无模型时可用自动测试验证状态映射和互锁"),
    ("U1 三维表现", "整机绑定、上下液仓、流道、两泵、粉饼区域和预设视角", "手动上下液与四阶段循环可完整演示"),
    ("U2 直接串口", "串口枚举、版本握手、状态快照、命令结果、超时和重连", "Unity 可独立完成 proto 1 通信并保持主线程流畅"),
    ("U3 真机联调", "ESP32 实机快照、超时、重启、互锁和异常包测试", "AT 07 至 AT 13 全部通过"),
    ("U4 验收打包", "配置、运行说明、测试报告、Windows 构建和已知问题", "P0 需求关闭且长稳测试通过"),
], widths=[1.0, 3.25, 2.2], font_size=8.9, center_cols=[0])

add_heading(doc, "16 待确认事项", 1)
add_table(doc, ["编号", "事项", "建议基线", "影响"], [
    ("OD 01", "Unity 的 Windows 串口实现库", "封装在 ISerialTransport 后，先验证 Unity 2022 Windows 构建兼容性", "依赖选择与部署"),
    ("OD 02", "上部液仓精确有效容积和内腔轮廓", "由 CAD 量测后固化为权威几何", "液量换算与满仓位置"),
    ("OD 03", "自然回流与负压加速的阶段分界", "增加降液阶段内的可配置比例或事件", "物理表现准确性"),
    ("OD 04", "上液和降液完整转移标定时间", "由操作者输入并保存到配方或设备配置", "估算液位和动画速度"),
    ("OD 05", "Unity 配方与历史文件格式", "独立 JSON 文件，带 schemaVersion 和自动备份", "升级和损坏恢复"),
    ("OD 06", "发布形态", "独立 Unity Windows 应用", "窗口、串口权限和安装包"),
], widths=[0.75, 2.25, 2.35, 1.1], font_size=8.8, center_cols=[0])

add_heading(doc, "17 交付物", 1)
add_bullets(doc, [
    "可运行的 Unity 2022.3.62f3c1 工程和 Windows 构建。",
    "Unity 串口传输与 proto 1 协议客户端实现。",
    "模型对象与状态字段映射表。",
    "自动测试、验收记录、性能结果和已知问题清单。",
    "运行与联调说明，包括串口连接、协议诊断、模拟模式和故障恢复。",
])

add_heading(doc, "18 需求依据", 1)
add_table(doc, ["资料", "采用内容"], [
    ("咖啡冷萃机上位机用户手册 0.4.0 M4", "仅参考操作流程、配方、历史、控制指令和日常验收"),
    ("咖啡冷萃机上位机产品与工程规划", "仅参考状态模型、控制原则、三舱体表现和 GPIO 定义"),
    ("咖啡冷萃机串口通讯协议 M3", "115200 JSON Lines、proto 1、hello ping state set ack 和故障规则"),
    ("上位机 CoffeeMachine 源码", "仅参考已验证的参数范围、1 秒心跳、3 秒超时和状态枚举，不复用代码"),
    ("便携式自动冷萃咖啡机报告", "产品结构、轴向外观、环形流道和三阶段工作原理"),
    ("ColdBrewTwin Unity 工程说明", "模型坐标、下部液仓轮廓、1.5317 L 容积和现有水体组件"),
], widths=[2.6, 3.85], font_size=8.8, center_cols=[0])

# Core properties and final save.
doc.core_properties.title = "咖啡冷萃机 Unity 数字孪生控制需求书"
doc.core_properties.subject = "咖啡冷萃机数字孪生控制与验收需求"
doc.core_properties.author = "项目组"
doc.core_properties.keywords = "咖啡冷萃机, Unity, 数字孪生, 串口控制, ESP32"

# Prevent rows from splitting where possible without fixing row height.
for table in doc.tables:
    for row in table.rows:
        tr_pr = row._tr.get_or_add_trPr()
        cant_split = OxmlElement("w:cantSplit")
        tr_pr.append(cant_split)

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
doc.save(OUTPUT)
print(OUTPUT)
