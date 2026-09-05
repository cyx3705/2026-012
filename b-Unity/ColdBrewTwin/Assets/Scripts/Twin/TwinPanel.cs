using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneHistory.ColdBrewTwin
{
    public sealed class TwinPanel : MonoBehaviour
    {
        static readonly Color Ink = new Color(.10f,.15f,.18f);
        static readonly Color Muted = new Color(.32f,.40f,.43f);
        static readonly Color Teal = new Color(.025f,.42f,.40f);
        static readonly Color Line = new Color(.54f,.65f,.68f,.30f);
        static readonly Color Paper = new Color(.97f,.985f,1,.78f);
        static readonly Color QuietButton = new Color(1,1,1,.57f);
        TwinApplication app;
        Font font;
        Canvas canvas;
        RectTransform root, content;
        Text status, lowerText, upperText, pumpUp, pumpDown, elapsed, remaining, cycle, footer, notice, progressText;
        Text pressureReading,flowReading,channelReading,channelState;
        Image lowerBar, upperBar, progressBar;
        readonly List<Selectable> locked = new List<Selectable>();
        readonly List<Text> phaseLabels = new List<Text>();
        readonly List<Button> viewButtons = new List<Button>();
        readonly List<Button> tabButtons = new List<Button>();
        Button upButton, downButton, startButton, pauseButton;
        Text pauseText;
        Toggle section;
        int tab=4;
        string selectedRecipe;
        bool oldBusy;
        float refreshAt, fps;

        public void Initialize(TwinApplication application)
        {
            app = application;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Arial" }, 18);
            var go = new GameObject("TwinControlPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root = (RectTransform)go.transform;
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440,900); scaler.matchWidthOrHeight = 1;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            BuildHeader(); BuildStatus(); BuildControls();
            var bottom = Band(root, "Footer", new Color(.09f,.13f,.15f,.86f));
            Anchor(bottom, new Vector2(0,0), new Vector2(1,0), new Vector2(0,0), new Vector2(0,36));
            footer = Label(bottom, "", 18, 6, 1250, 24, 12, Color.white);
        }

        void BuildHeader()
        {
            var header = Band(root, "Header", Paper);
            Anchor(header, new Vector2(0,1), Vector2.one, new Vector2(0,-60), Vector2.zero);
            Label(header, "咖啡冷萃机", 22, 11, 270, 32, 24, Ink, FontStyle.Bold);
            Label(header, "数字孪生  /  模拟数据", 315, 19, 300, 26, 14, Teal);
            var stop = Button(header, "软件停止", 0, 10, 136, 40, () => app.Simulation.Stop(), new Color(.76f,.20f,.20f), Color.white);
            stop.GetComponent<RectTransform>().anchorMin = stop.GetComponent<RectTransform>().anchorMax = new Vector2(1,1);
            stop.GetComponent<RectTransform>().anchoredPosition = new Vector2(-154,-10);
        }

        void BuildStatus()
        {
            var left = Band(root, "State", Paper);
            Anchor(left, new Vector2(0,0), new Vector2(0,1), new Vector2(0,36), new Vector2(296,-60));
            Label(left, "运行状态", 22, 20, 246, 25, 14, Muted);
            status = Label(left, "待机", 22, 49, 246, 42, 28, Ink, FontStyle.Bold);
            cycle = Label(left, "", 22, 94, 250, 24, 13, Muted);
            Divider(left, 22, 131, 250);
            Label(left, "上部液仓 · 模拟估算", 22, 150, 248, 22, 14, Muted);
            upperText = Label(left, "", 22, 177, 248, 32, 22, Ink, FontStyle.Bold);
            upperBar = Bar(left, 22, 218, 250, 6, new Color(.19f,.52f,.80f));
            Label(left, "下部液仓 · 模拟估算", 22, 248, 248, 22, 14, Muted);
            lowerText = Label(left, "", 22, 275, 248, 32, 22, Ink, FontStyle.Bold);
            lowerBar = Bar(left, 22, 316, 250, 6, Teal);
            channelState=Label(left,"",22,326,250,18,11,Muted);
            pumpUp = Label(left, "", 22, 348, 260, 25, 14, Ink);
            pumpDown = Label(left, "", 22, 381, 260, 25, 14, Ink);
            Divider(left, 22, 425, 250);
            Label(left, "观察视角", 22, 443, 245, 25, 14, Muted);
            for (int i = 0; i < MachineBinder.ViewNames.Length; i++)
            {
                int index = i;
                viewButtons.Add(Button(left, MachineBinder.ViewNames[i], 22, 479 + i * 40, 250, 33,
                    () => { app.Machine.SetView(index); section.SetIsOnWithoutNotify(app.Machine.Section); }, Color.white, Ink));
            }
            section = Toggle(left, "剖视", 22, 697, app.Machine.Section, value => app.Machine.Section = value);
            Toggle(left, "减少动态效果", 22, 737, false, value => app.Machine.ReducedMotion = value);
            Button(left, "↺", 224, 691, 48, 36, () => app.Machine.SetView(0), Color.white, Ink);
        }

        void BuildControls()
        {
            var right = Band(root, "Controls", Paper);
            Anchor(right, new Vector2(1,0), Vector2.one, new Vector2(-334,36), new Vector2(0,-60));
            string[] names = { "控制", "配方", "记录", "设置", "水流" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                tabButtons.Add(Button(right, names[i], 14 + i * 62, 14, 58, 36, () => { tab = index; BuildTab(); }, Color.white, Ink));
            }
            Divider(right, 14, 61, 306);
            var scrollRoot = Band(right, "Scroll", Color.clear);
            Anchor(scrollRoot, Vector2.zero, Vector2.one, new Vector2(14,72), new Vector2(-14,-72));
            scrollRoot.gameObject.AddComponent<RectMask2D>();
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            content = Rect(scrollRoot, "Content", 0, 0, 306, 730);
            content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
            content.sizeDelta = new Vector2(0,730);
            scroll.content = content; scroll.viewport = scrollRoot;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            notice = Label(right, "", 18, 0, 298, 52, 12, Muted);
            Anchor(notice.rectTransform, Vector2.zero, new Vector2(1,0), new Vector2(18,8), new Vector2(-18,64));
            BuildTab();
        }

        void BuildTab()
        {
            for(int i=0;i<tabButtons.Count;i++)
            {
                tabButtons[i].image.color=i==tab?Teal:QuietButton;
                tabButtons[i].GetComponentInChildren<Text>().color=i==tab?Color.white:Ink;
            }
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            locked.Clear(); phaseLabels.Clear();
            upButton = downButton = startButton = pauseButton = null;
            elapsed = remaining = progressText = null; progressBar = null;
            pressureReading=flowReading=channelReading=null;
            content.anchoredPosition = Vector2.zero;
            if (tab == 0) BuildRun();
            else if (tab == 1) BuildRecipes();
            else if (tab == 2) BuildHistory();
            else if(tab==3) BuildSettings();
            else BuildWaterFlow();
            UpdateLocks();
        }

        void BuildRun()
        {
            Label(content, "手动转移", 2, 0, 300, 25, 15, Ink, FontStyle.Bold);
            upButton = Button(content, "↑  上液", 2, 38, 144, 43, () => app.Simulation.Manual(MachineMode.Up), new Color(.97f,.81f,.40f), Ink);
            downButton = Button(content, "↓  降液", 158, 38, 144, 43, () => app.Simulation.Manual(MachineMode.Down), new Color(.77f,.88f,.97f), Ink);
            locked.Add(upButton); locked.Add(downButton);
            Divider(content, 2, 101, 300);
            Label(content, "固定循环", 2, 121, 300, 25, 15, Ink, FontStyle.Bold);
            TextField(content, "配方名称", app.Recipe.name, 158, value => app.Recipe.name = value);
            NumberField("上液 / 秒", app.Recipe.upSeconds, 215, 1, 3600, value => app.Recipe.upSeconds = value);
            NumberField("上液停留 / 秒", app.Recipe.upDwellSeconds, 256, 0, 3600, value => app.Recipe.upDwellSeconds = value);
            NumberField("降液 / 秒", app.Recipe.downSeconds, 297, 1, 3600, value => app.Recipe.downSeconds = value);
            NumberField("降液停留 / 秒", app.Recipe.downDwellSeconds, 338, 0, 3600, value => app.Recipe.downDwellSeconds = value);
            NumberField("循环次数", app.Recipe.cycles, 379, 1, 999, value => app.Recipe.cycles = Mathf.RoundToInt(value), true);
            var quick = Button(content, "快速联调", 2, 430, 144, 34, () => { app.LoadRecipe(BrewRecipe.QuickTest()); BuildTab(); }, Color.white, Ink);
            var defaults = Button(content, "默认冷萃", 158, 430, 144, 34, () => { app.LoadRecipe(new BrewRecipe()); BuildTab(); }, Color.white, Ink);
            locked.Add(quick); locked.Add(defaults);
            startButton = Button(content, "开始循环", 2, 481, 193, 43, app.StartRun, Teal, Color.white); locked.Add(startButton);
            pauseButton = Button(content, "暂停", 207, 481, 95, 43, () =>
            {
                if (app.Simulation.Paused) app.Simulation.Resume(); else app.Simulation.Pause();
            }, Color.white, Ink);
            pauseText = pauseButton.GetComponentInChildren<Text>();
            elapsed = Label(content, "", 2, 534, 144, 25, 13, Muted);
            remaining = Label(content, "", 153, 534, 149, 25, 13, Muted);
            progressBar = Bar(content, 2, 565, 300, 5, Teal);
            progressText = Label(content, "", 2, 577, 300, 24, 13, Muted);
            string[] names = { "1  上液", "2  上部停留", "3  降液", "4  下部停留" };
            for (int i = 0; i < 4; i++) phaseLabels.Add(Label(content, names[i], 6 + i % 2 * 151, 610 + i / 2 * 26, 145, 22, 13, Muted));
            content.sizeDelta = new Vector2(0,660);
        }

        void BuildRecipes()
        {
            Label(content, "命名配方", 2, 0, 300, 25, 15, Ink, FontStyle.Bold);
            TextField(content, "配方名称", app.Recipe.name, 39, value => app.Recipe.name = value);
            locked.Add(Button(content, "保存当前", 2, 103, 94, 36, () => { app.SaveRecipe(); BuildTab(); }, Teal, Color.white));
            locked.Add(Button(content, "重命名", 106, 103, 94, 36, () => { app.RenameRecipe(selectedRecipe, app.Recipe.name); BuildTab(); }, Color.white, Ink));
            locked.Add(Button(content, "删除", 210, 103, 92, 36, () => { app.DeleteRecipe(selectedRecipe); selectedRecipe = null; BuildTab(); }, Color.white, Ink));
            float y = 165;
            foreach (var recipe in app.Storage.Data.recipes)
            {
                var captured = recipe;
                locked.Add(Button(content, recipe.name, 2, y, 300, 36, () =>
                { selectedRecipe = captured.name; app.LoadRecipe(captured); tab = 0; BuildTab(); }, Color.white, Ink));
                Label(content, $"{recipe.upSeconds:g} / {recipe.upDwellSeconds:g} / {recipe.downSeconds:g} / {recipe.downDwellSeconds:g} 秒  ·  {recipe.cycles} 次", 8, y+40, 292, 23, 12, Muted);
                y += 80;
            }
            content.sizeDelta = new Vector2(0,Mathf.Max(400,y));
        }

        void BuildHistory()
        {
            Label(content, "运行历史", 2, 0, 300, 25, 15, Ink, FontStyle.Bold);
            float y = 40;
            var history = app.Storage.Data.history;
            if (history.Count == 0) { Label(content, "暂无运行记录", 2, y, 300, 26, 14, Muted); y += 44; }
            for (int i = history.Count-1; i >= Math.Max(0,history.Count-80); i--)
            {
                var item = history[i];
                Label(content, item.recipeName + "  ·  " + item.result, 2, y, 300, 26, 14, Ink);
                Label(content, item.startedAt + "\n完成 " + item.cyclesCompleted + " 次  " + item.reason, 2, y+28, 300, 45, 11, Muted);
                Divider(content, 2, y+80, 300); y += 96;
            }
            Label(content, "模拟事件", 2, y, 300, 26, 15, Ink, FontStyle.Bold); y += 40;
            var events = app.Storage.Data.events;
            for (int i = events.Count-1; i >= Math.Max(0,events.Count-80); i--)
            {
                var item = events[i];
                Label(content, item.time + "  " + item.type + "\n" + item.summary, 2, y, 300, 46, 11, Muted);
                y += 56;
            }
            content.sizeDelta = new Vector2(0,y+20);
        }

        void BuildSettings()
        {
            Label(content, "模拟参数", 2, 0, 300, 25, 15, Ink, FontStyle.Bold);
            NumberField("总液量 / L", app.Settings.totalLitres, 44, .01f, app.Machine.TransferCapacity, value => app.Settings.totalLitres = value);
            NumberField("手动上液 / 秒", app.Settings.upSeconds, 88, 1, 3600, value => app.Settings.upSeconds = value);
            NumberField("手动降液 / 秒", app.Settings.downSeconds, 132, 1, 3600, value => app.Settings.downSeconds = value);
            NumberField("自然回流时间占比", app.Settings.naturalTimeRatio, 176, .05f, .9f, value => app.Settings.naturalTimeRatio = value);
            NumberField("粉饼流阻倍率", app.Settings.powderResistance, 220, .2f, 5f, value => app.Settings.powderResistance = value);
            locked.Add(Button(content, "应用并重置液量", 2, 276, 300, 42, app.ResetLiquid, Teal, Color.white));
            Label(content, "模拟速度", 2, 356, 300, 26, 14, Ink);
            float[] speeds = { .5f, 1, 2, 5, 10 };
            for (int i = 0; i < speeds.Length; i++)
            {
                float speed = speeds[i];
                Button(content, speed.ToString("g") + "×", 2+i*61, 395, 55, 36, () => app.Speed = speed, Color.white, Ink);
            }
            Divider(content, 2, 457, 300);
            Label(content, "几何容积", 2, 480, 300, 26, 14, Ink);
            Label(content, app.Machine.GeometryStatus.Replace("  /  ", "\n"), 2, 519, 300, 52, 13, Muted);
            content.sizeDelta = new Vector2(0,620);
        }

        void BuildWaterFlow()
        {
            Label(content,"气压水流",2,0,300,27,18,Ink,FontStyle.Bold);
            Label(content,"下仓表压 · 模拟估算",2,38,300,22,12,Muted);
            pressureReading=Label(content,"",2,65,300,42,30,Teal,FontStyle.Bold);
            flowReading=Label(content,"",2,116,300,25,14,Ink);
            channelReading=Label(content,"",2,151,300,23,12,Muted);
            Divider(content,2,192,300);
            PressureSlider("上液目标 / kPa",220,app.Settings.upPressureKpa,25,
                value=>app.SetPressure(value,app.Settings.vacuumKpa));
            PressureSlider("降液真空度 / kPa",306,app.Settings.vacuumKpa,20,
                value=>app.SetPressure(app.Settings.upPressureKpa,value));
            startButton=Button(content,"开始水流演示",2,400,193,44,app.StartFlowDemo,Teal,Color.white); locked.Add(startButton);
            pauseButton=Button(content,"暂停",207,400,95,44,()=>
            { if(app.Simulation.Paused) app.Simulation.Resume(); else app.Simulation.Pause(); },Color.white,Ink);
            pauseText=pauseButton.GetComponentInChildren<Text>();
            upButton=Button(content,"↑  上液",2,460,94,36,()=>app.Simulation.Manual(MachineMode.Up),new Color(.97f,.81f,.40f),Ink);
            downButton=Button(content,"↓  降液",106,460,94,36,()=>app.Simulation.Manual(MachineMode.Down),new Color(.77f,.88f,.97f),Ink);
            locked.Add(upButton); locked.Add(downButton);
            locked.Add(Button(content,"重置",210,460,92,36,app.ResetLiquid,Color.white,Ink));
            elapsed=Label(content,"",2,519,145,25,13,Muted);
            remaining=Label(content,"",153,519,149,25,13,Muted);
            progressBar=Bar(content,2,556,300,5,Teal);
            progressText=Label(content,"",2,577,300,24,13,Muted);
            string[] phases={"1  加压上液","2  上部停留","3  粉饼回流","4  下部停留"};
            for(int i=0;i<4;i++) phaseLabels.Add(Label(content,phases[i],6+i%2*151,610+i/2*26,145,22,13,Muted));
            content.sizeDelta=new Vector2(0,660);
        }
        void PressureSlider(string caption,float y,float value,float max,Action<float> changed)
        {
            Label(content,caption,2,y,211,24,13,Muted);
            var reading=Label(content,value.ToString("F1"),234,y,68,24,16,Ink,FontStyle.Bold);
            var rect=Rect(content,"PressureSlider",2,y+35,300,26);
            var slider=rect.gameObject.AddComponent<Slider>(); slider.minValue=0; slider.maxValue=max;
            var track=Band(rect,"Track",Line); Place(track,8,10,284,6);
            var fill=Band(track,"Fill",Teal); Anchor(fill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            slider.fillRect=fill;
            var handleArea=Rect(rect,"HandleArea",8,0,284,26);
            var handle=Band(handleArea,"Handle",Teal); Place(handle,0,1,14,24);
            handle.pivot=new Vector2(.5f,.5f); slider.handleRect=handle;
            slider.targetGraphic=handle.GetComponent<Image>(); slider.value=value;
            slider.onValueChanged.AddListener(number=> {reading.text=number.ToString("F1"); changed(number);});
        }

        void Update()
        {
            if (app == null) return;
            var s = app.Simulation.State;
            status.text = Status(s);
            status.color = s.IsPaused ? new Color(.70f,.43f,.07f) : s.Gpio15 ? new Color(.67f,.43f,.04f) :
                s.MachineMode == MachineMode.Down ? new Color(.10f,.43f,.73f) : Teal;
            cycle.text = s.TotalCycles > 0 ? $"循环 {s.CurrentCycle} / {s.TotalCycles}  ·  {app.Recipe.name}" : "手动模式  ·  模拟数据";
            upperText.text = $"{s.UpperLitres:F3} L   {s.UpperLitres/app.Machine.UpperCapacity:P0}";
            lowerText.text = $"{s.LowerLitres:F3} L   {s.LowerLitres/app.Machine.LowerCapacity:P0}";
            channelState.text=$"环形流道  {s.ChannelLitres*1000:F1} mL";
            if(pressureReading!=null) pressureReading.text=$"{s.PressureKpa:+0.00;-0.00;0.00} kPa";
            if(flowReading!=null) flowReading.text=$"液体流量  {(s.UpFlowLps+s.ReturnFlowLps+s.ChannelDrainLps)*1000:F1} mL/s";
            if(channelReading!=null) channelReading.text=$"水柱前沿  {s.ChannelFrontY*1000:F0} mm  ·  水头 {s.HeadKpa:F2} kPa";
            SetFill(upperBar, s.UpperLitres/app.Machine.UpperCapacity); SetFill(lowerBar, s.LowerLitres/app.Machine.LowerCapacity);
            pumpUp.text = "↑  上液泵   " + (s.Gpio15 ? "运行" : "关闭") + "  ·  GPIO15";
            pumpDown.text = "↓  降液泵   " + (s.Gpio16 ? "运行" : "关闭") + "  ·  GPIO16";
            if (elapsed != null) elapsed.text = "已用 " + TimeText(s.ElapsedSeconds);
            if (remaining != null) remaining.text = "剩余 " + TimeText(s.RemainingSeconds);
            if (progressBar != null) SetFill(progressBar, s.PhaseProgress);
            if (progressText != null) progressText.text = $"当前阶段 {s.PhaseElapsed:F1} / {s.PhaseDuration:g} 秒";
            for (int i = 0; i < phaseLabels.Count; i++) phaseLabels[i].color = (int)s.Phase == i+1 ? Teal : Muted;
            if (pauseButton != null) { pauseButton.interactable = app.Simulation.Busy; pauseText.text = s.IsPaused ? "继续" : "暂停"; }
            if (oldBusy != app.Simulation.Busy) { oldBusy = app.Simulation.Busy; UpdateLocks(); }
            for (int i = 0; i < viewButtons.Count; i++)
                viewButtons[i].image.color = app.Machine.SelectedView == i ? new Color(.70f,.86f,.83f,.88f) : QuietButton;
            section.SetIsOnWithoutNotify(app.Machine.Section);
            fps = Mathf.Lerp(fps, 1/Mathf.Max(.001f,Time.unscaledDeltaTime), .04f);
            footer.text = $"SIMULATION    模拟数据  ·  液位为估算      总液量 {s.TotalLitres:F3} L      {app.Speed:g}×      {fps:F0} FPS";
            notice.text = !string.IsNullOrEmpty(app.Storage.Warning) ? app.Storage.Warning :
                !string.IsNullOrEmpty(app.Notice) ? app.Notice : s.Result;
            if (tab == 2 && Time.unscaledTime > refreshAt && content.anchoredPosition.y < 1)
            { refreshAt = Time.unscaledTime+3; BuildTab(); }
        }
        void UpdateLocks() { foreach (var item in locked) if (item != null) item.interactable = !app.Simulation.Busy; }
        public static string Status(TwinRuntimeState s)
        {
            if (s.IsPaused) return "已暂停";
            if (s.MachineMode == MachineMode.Complete) return s.UpperLitres+s.ChannelLitres>.001f?"阶段结束":"循环完成";
            if (s.Phase == CyclePhase.UpDwell && s.IsRunning) return "上部停留";
            if (s.Phase == CyclePhase.DownDwell && s.IsRunning) return "下部停留";
            if (s.Gpio15) return s.UpFlowLps<.0001f && s.LowerLitres>.001f ? "建立压差" : s.OutletFlowLps>.0001f ? "顶部溢流" : "水柱上升";
            if (s.NaturalReturn) return "↓  自然回流";
            if (s.Gpio16) return "↓  负压加速";
            return "待机";
        }
        static string TimeText(double seconds) => $"{(int)seconds/60:00}:{(int)seconds%60:00}";

        void NumberField(string caption, float value, float y, float min, float max, Action<float> changed, bool integer = false)
        {
            Label(content, caption, 2, y+5, 178, 26, 13, Muted);
            var input = Input(content, value.ToString("0.###", CultureInfo.InvariantCulture), 183, y, 119, 35);
            input.contentType = InputField.ContentType.DecimalNumber;
            input.onEndEdit.AddListener(text =>
            {
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) parsed = value;
                parsed = Numeric.Clamp(parsed,min,max); if (integer) parsed = Mathf.Round(parsed);
                changed(parsed); input.SetTextWithoutNotify(parsed.ToString("0.###",CultureInfo.InvariantCulture));
            });
            locked.Add(input);
        }
        void TextField(RectTransform parent, string caption, string value, float y, Action<string> changed)
        {
            Label(parent, caption, 2, y, 300, 20, 12, Muted);
            var input = Input(parent, value, 2, y+23, 300, 33);
            input.characterLimit = 32;
            input.onEndEdit.AddListener(changed.Invoke); locked.Add(input);
        }
        InputField Input(RectTransform parent, string value, float x, float y, float width, float height)
        {
            var rect = Band(parent,"Input",new Color(1,1,1,.75f)); Place(rect,x,y,width,height);
            var input = rect.gameObject.AddComponent<InputField>();
            var text = Label(rect,value,9,3,width-18,height-6,14,Ink);
            text.supportRichText = false;
            input.textComponent = text; input.text = value; input.targetGraphic = rect.GetComponent<Image>();
            return input;
        }
        Toggle Toggle(RectTransform parent, string label, float x, float y, bool value, Action<bool> changed)
        {
            var rect = Rect(parent,label,x,y,190,30); var toggle = rect.gameObject.AddComponent<Toggle>();
            var background = Band(rect,"Box",Color.white); Place(background,0,5,20,20);
            var tick = Band(background,"Check",Teal); Place(tick,4,4,12,12);
            toggle.targetGraphic = background.GetComponent<Image>(); toggle.graphic = tick.GetComponent<Image>();
            Label(rect,label,31,1,159,28,14,Ink);
            toggle.isOn = value; toggle.onValueChanged.AddListener(changed.Invoke); return toggle;
        }
        Button Button(RectTransform parent, string text, float x, float y, float width, float height, Action clicked, Color color, Color textColor)
        {
            var rect = Band(parent,text,color==Color.white?QuietButton:color); Place(rect,x,y,width,height);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(.86f,.94f,.91f); colors.pressedColor = new Color(.69f,.82f,.77f);
            colors.disabledColor = new Color(.76f,.79f,.78f,.55f); button.colors = colors;
            var label = Label(rect,text,6,0,width-12,height,14,textColor); label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => { clicked(); UpdateLocks(); }); return button;
        }
        Image Bar(RectTransform parent, float x, float y, float width, float height, Color color)
        {
            var background = Band(parent,"Track",Line); Place(background,x,y,width,height);
            var fill = Band(background,"Fill",color); Anchor(fill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            return fill.GetComponent<Image>();
        }
        static void SetFill(Image image, float fraction) => image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction),1);
        void Divider(RectTransform parent, float x, float y, float width) { var line = Band(parent,"Divider",Line); Place(line,x,y,width,1); }
        Text Label(RectTransform parent, string value, float x, float y, float width, float height, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var rect = Rect(parent,"Text",x,y,width,height); var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.fontStyle = style; text.text = value; text.color = color;
            text.raycastTarget = false; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        static RectTransform Band(RectTransform parent, string name, Color color)
        {
            var rect = Rect(parent,name,0,0,10,10); rect.gameObject.AddComponent<Image>().color = color; return rect;
        }
        static RectTransform Rect(RectTransform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false); Place(rect,x,y,width,height); return rect;
        }
        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height);
        }
        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
        void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); if (font != null) Destroy(font); }
    }
}
