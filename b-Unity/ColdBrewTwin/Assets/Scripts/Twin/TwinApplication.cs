using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneHistory.ColdBrewTwin
{
    [DefaultExecutionOrder(-100)]
    public sealed class TwinApplication : MonoBehaviour
    {
        public BrewSimulation Simulation { get; private set; }
        public TwinPersistence Storage { get; private set; }
        public MachineBinder Machine { get; private set; }
        public BrewRecipe Recipe { get; private set; }
        public SimulationSettings Settings => Storage.Data.settings;
        public float Speed { get; set; } = 1;
        public string Notice { get; set; } = string.Empty;
        BrewHistoryEntry activeRun;
        double savedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name == "ColdBrewTwin" && FindObjectOfType<TwinApplication>() == null)
                new GameObject("TwinApplication").AddComponent<TwinApplication>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            foreach (var source in FindObjectsOfType<WaterSource>()) { source.Close(); source.enabled = false; }
            bool smoke = Array.IndexOf(Environment.GetCommandLineArgs(), "--twin-smoke") >= 0 ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "--flow-smoke") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "--appearance-smoke") >= 0;
            Storage = new TwinPersistence(smoke ? System.IO.Path.Combine(Application.temporaryCachePath, "smoke", Guid.NewGuid().ToString("N")) : Application.persistentDataPath);
            Recipe = Storage.Data.lastRecipe.Copy();
            Machine = gameObject.AddComponent<MachineBinder>();
            Machine.Initialize();
            Settings.Clamp(Machine.TransferCapacity);
            Simulation = new BrewSimulation(Settings,Machine.Channel,Machine.LowerLevel,Machine.UpperLevel);
            Simulation.Reset(Settings, Machine.TransferCapacity);
            Simulation.EventRaised += Record;
            Simulation.RunFinished += FinishRun;
            gameObject.AddComponent<TwinPanel>().Initialize(this);
            Notice = Storage.Warning;
            Record("startup", "模拟启动，两泵关闭");
            if (smoke) gameObject.AddComponent<SimulationSmoke>();
        }

        void Update()
        {
            Simulation.Tick(Time.unscaledDeltaTime * Numeric.Clamp(Speed, .25f, 10));
            Machine.Apply(Simulation.State, Simulation.Clock);
            if (Time.realtimeSinceStartupAsDouble - savedAt > 5)
            {
                Storage.Save();
                savedAt = Time.realtimeSinceStartupAsDouble;
            }
        }

        public void LoadRecipe(BrewRecipe recipe)
        {
            if (Simulation.Busy) return;
            Recipe = recipe.Copy();
            Recipe.Clamp();
            Storage.Data.lastRecipe = Recipe.Copy();
        }
        public void SaveRecipe()
        {
            if (Simulation.Busy) return;
            Recipe.Clamp();
            var list = Storage.Data.recipes;
            int index = list.FindIndex(r => r.name == Recipe.name);
            if (index < 0) list.Add(Recipe.Copy()); else list[index] = Recipe.Copy();
            Storage.Data.lastRecipe = Recipe.Copy();
            Storage.Save();
            Notice = "配方已保存";
        }
        public void DeleteRecipe(string name)
        {
            if (Simulation.Busy) return;
            Storage.Data.recipes.RemoveAll(r => r.name == name);
            Storage.Save();
        }
        public void RenameRecipe(string oldName, string newName)
        {
            if (Simulation.Busy || string.IsNullOrWhiteSpace(newName)) return;
            var existing = Storage.Data.recipes.Find(r => r.name == oldName);
            if (existing == null) { SaveRecipe(); return; }
            if (Storage.Data.recipes.Exists(r => r != existing && r.name == newName)) { Notice = "配方名已存在"; return; }
            existing.name = newName.Trim();
            Recipe.name = existing.name;
            Storage.Save();
        }
        public void StartRun()
        {
            if (Simulation.Busy) return;
            Recipe.Clamp();
            activeRun = new BrewHistoryEntry { startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), recipeName = Recipe.name };
            if (!Simulation.Start(Recipe)) activeRun = null;
            Storage.Data.lastRecipe = Recipe.Copy();
        }
        public void ResetLiquid()
        {
            if (Simulation.Busy) return;
            Settings.Clamp(Machine.TransferCapacity);
            Simulation.Reset(Settings, Machine.TransferCapacity);
            Storage.Save();
        }
        public void SetPressure(float up,float vacuum)
        {
            Settings.upPressureKpa=Numeric.Clamp(up,0,25);
            Settings.vacuumKpa=Numeric.Clamp(vacuum,0,20);
            Simulation.SetPressure(Settings.upPressureKpa,Settings.vacuumKpa);
        }
        public void StartFlowDemo()
        {
            if(Simulation.Busy) return;
            ResetLiquid();
            LoadRecipe(new BrewRecipe { name="气压水流演示",upSeconds=12,upDwellSeconds=2,downSeconds=12,downDwellSeconds=2,cycles=1 });
            Machine.SetView(0);
            StartRun();
        }
        void FinishRun(bool ok, string reason, int completed)
        {
            if (activeRun == null) return;
            activeRun.endedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            activeRun.result = ok ? "完成" : "终止";
            activeRun.reason = reason;
            activeRun.cyclesCompleted = completed;
            Storage.Data.history.Add(activeRun);
            activeRun = null;
            Storage.Save();
        }
        void Record(string type, string summary)
        {
            var s = Simulation.State;
            Storage.Data.events.Add(new TwinEvent { time = DateTime.Now.ToString("HH:mm:ss.fff"),
                simulationSeconds = Simulation.Clock, type = type, summary = summary, recipe = activeRun?.recipeName ?? Recipe.name,
                gpio15 = s.Gpio15, gpio16 = s.Gpio16, lowerLitres = s.LowerLitres, upperLitres = s.UpperLitres,
                channelLitres=s.ChannelLitres,pressureKpa=s.PressureKpa });
            if (Storage.Data.events.Count > 500) Storage.Data.events.RemoveAt(0);
        }
        void OnApplicationQuit()
        {
            Simulation?.Stop("程序退出");
            Storage?.Save();
        }
    }
}
