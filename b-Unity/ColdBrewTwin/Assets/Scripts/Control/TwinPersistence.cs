using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace OneHistory.ColdBrewTwin
{
    public sealed class TwinPersistence
    {
        readonly string path;
        internal PersistenceData Data { get; private set; } = new PersistenceData();
        public string Warning { get; private set; } = string.Empty;
        public string FilePath => path;
        public TwinPersistence(string directory)
        {
            path = Path.Combine(directory, "simulation-v1.json");
            if (!File.Exists(path)) return;
            try { Data = Read(path); }
            catch (Exception ex)
            {
                Warning = "记录文件损坏，已恢复默认值：" + ex.Message;
                try
                {
                    File.Copy(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
                    if (File.Exists(path + ".bak")) { Data = Read(path + ".bak"); Warning = "记录已从备份恢复"; }
                }
                catch (Exception recovery) { Debug.LogWarning(recovery.Message); }
            }
        }
        static PersistenceData Read(string file)
        {
            var data = JsonUtility.FromJson<PersistenceData>(File.ReadAllText(file, Encoding.UTF8));
            if (data == null || data.schemaVersion < 1 || data.schemaVersion > 2 || data.lastRecipe == null || data.settings == null ||
                data.recipes == null || data.history == null || data.events == null)
                throw new InvalidDataException("Invalid simulation schema");
            if (data.schemaVersion == 1)
            {
                data.settings.upPressureKpa = 12; data.settings.vacuumKpa = 6;
                data.settings.pressureResponseSeconds = .65f;
                data.settings.flowResistance = data.settings.powderResistance = 1;
                data.schemaVersion = 2;
            }
            data.recipes.RemoveAll(item => item == null);
            data.history.RemoveAll(item => item == null);
            data.events.RemoveAll(item => item == null);
            foreach (var recipe in data.recipes) recipe.Clamp();
            data.lastRecipe.Clamp();
            Trim(data);
            return data;
        }
        static void Trim(PersistenceData data)
        {
            if (data.history.Count > 500) data.history.RemoveRange(0, data.history.Count - 500);
            if (data.events.Count > 500) data.events.RemoveRange(0, data.events.Count - 500);
        }
        public void Save()
        {
            try
            {
                Trim(Data);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(Data, true), Encoding.UTF8);
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
                else File.Move(path + ".tmp", path);
                Warning = string.Empty;
            }
            catch (Exception ex) { Warning = "保存失败：" + ex.Message; Debug.LogWarning(Warning); }
        }
    }
}
