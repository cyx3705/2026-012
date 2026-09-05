using System;
using System.Collections.Generic;

namespace OneHistory.ColdBrewTwin
{
    public enum TwinSourceMode { Simulation, Hardware, Playback }
    public enum ConnectionStatus { Offline, Connecting, Online, Timeout, Disconnected, Error }
    public enum MachineMode { Idle, Up, Down, Paused, Complete, Fault }
    public enum CyclePhase { None, Up, UpDwell, Down, DownDwell, Complete, Aborted }
    public enum CommandRequestState { Idle, Pending, Acknowledged, Applied, Rejected, TimedOut }

    [Serializable]
    public sealed class BrewRecipe
    {
        public string name = "默认冷萃";
        public float upSeconds = 30f;
        public float upDwellSeconds = 5f;
        public float downSeconds = 30f;
        public float downDwellSeconds = 5f;
        public int cycles = 3;

        public BrewRecipe Copy()
        {
            return new BrewRecipe
            {
                name = name,
                upSeconds = upSeconds,
                upDwellSeconds = upDwellSeconds,
                downSeconds = downSeconds,
                downDwellSeconds = downDwellSeconds,
                cycles = cycles
            };
        }

        public void Clamp()
        {
            if (string.IsNullOrWhiteSpace(name)) name = "未命名配方";
            upSeconds = Numeric.Clamp(upSeconds, 1f, 3600f);
            upDwellSeconds = Numeric.Clamp(upDwellSeconds, 0f, 3600f);
            downSeconds = Numeric.Clamp(downSeconds, 1f, 3600f);
            downDwellSeconds = Numeric.Clamp(downDwellSeconds, 0f, 3600f);
            cycles = Math.Max(1, Math.Min(999, cycles));
        }

        public static BrewRecipe QuickTest()
        {
            return new BrewRecipe
            {
                name = "快速联调",
                upSeconds = 3f,
                upDwellSeconds = 1f,
                downSeconds = 3f,
                downDwellSeconds = 1f,
                cycles = 1
            };
        }
    }

    [Serializable]
    public sealed class BrewHistoryEntry
    {
        public string startedAt;
        public string endedAt;
        public string recipeName;
        public string result;
        public string reason;
        public int cyclesCompleted;
    }

    [Serializable]
    public sealed class ProtocolEnvelope
    {
        public string type;
        public int id;
        public string cmd;
        public bool ok;
        public string device;
        public int proto;
        public long seq;
        public string mode;
        public bool gpio15;
        public bool gpio16;
        public long uptime;
        public string error;
        public string code;
        public string message;
    }

    public sealed class TwinRuntimeState
    {
        public TwinSourceMode SourceMode { get; internal set; } = TwinSourceMode.Simulation;
        public ConnectionStatus Connection { get; internal set; } = ConnectionStatus.Offline;
        public MachineMode MachineMode { get; internal set; } = MachineMode.Idle;
        public CyclePhase Phase { get; internal set; } = CyclePhase.None;
        public CommandRequestState RequestState { get; internal set; } = CommandRequestState.Idle;
        public bool Gpio15 { get; internal set; }
        public bool Gpio16 { get; internal set; }
        public long DeviceSequence { get; internal set; } = -1;
        public long DeviceUptimeMs { get; internal set; } = -1;
        public int CurrentCycle { get; internal set; }
        public int TotalCycles { get; internal set; }
        public float PhaseElapsed { get; internal set; }
        public float PhaseDuration { get; internal set; }
        public float EstimatedLowerFill { get; internal set; } = 0.5f;
        public string Fault { get; internal set; } = string.Empty;
        public string PortName { get; internal set; } = string.Empty;

        public float TotalLitres { get; internal set; } = 0.5f;
        public float LowerLitres => TotalLitres * EstimatedLowerFill;
        public float UpperLitres => Math.Max(0, TotalLitres - LowerLitres - ChannelLitres);
        public float ChannelLitres { get; internal set; }
        public float ChannelFrontY { get; internal set; } = (float)HydraulicModel.ChannelFloor;
        public float PressureKpa { get; internal set; }
        public float HeadKpa { get; internal set; }
        public float UpFlowLps { get; internal set; }
        public float OutletFlowLps { get; internal set; }
        public float ReturnFlowLps { get; internal set; }
        public float ChannelDrainLps { get; internal set; }
        public double FlowTravel { get; internal set; }
        public bool NaturalReturn { get; internal set; }
        public bool IsPaused { get; internal set; }
        public bool IsRunning { get; internal set; }
        public double ElapsedSeconds { get; internal set; }
        public double RemainingSeconds { get; internal set; }
        public string Result { get; internal set; } = string.Empty;
        public float PhaseProgress => PhaseDuration <= 0f ? 0f : Numeric.Clamp(PhaseElapsed / PhaseDuration, 0f, 1f);
        public float EstimatedUpperFill => TotalLitres > 0 ? UpperLitres / TotalLitres : 0;
        public bool IsFaulted => MachineMode == MachineMode.Fault;
    }

    [Serializable]
    sealed class PersistenceData
    {
        public int schemaVersion = 2;
        public BrewRecipe lastRecipe = new BrewRecipe();
        public SimulationSettings settings = new SimulationSettings();
        public List<BrewRecipe> recipes = new List<BrewRecipe> { new BrewRecipe(), BrewRecipe.QuickTest() };
        public List<BrewHistoryEntry> history = new List<BrewHistoryEntry>();
        public List<TwinEvent> events = new List<TwinEvent>();
    }

    public static class Numeric
    {
        public static float Clamp(float value, float min, float max) =>
            float.IsNaN(value) || float.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
    }

    [Serializable]
    public sealed class SimulationSettings
    {
        public float totalLitres = 0.5f;
        public float upSeconds = 30f;
        public float downSeconds = 30f;
        public float naturalTimeRatio = 0.4f;
        public float naturalVolumeRatio = 0.28f;
        public float upPressureKpa = 12f;
        public float vacuumKpa = 6f;
        public float pressureResponseSeconds = .65f;
        public float flowResistance = 1f;
        public float powderResistance = 1f;
        public void Clamp(float capacity)
        {
            totalLitres = Numeric.Clamp(totalLitres, 0.01f, capacity);
            upSeconds = Numeric.Clamp(upSeconds, 1f, 3600f);
            downSeconds = Numeric.Clamp(downSeconds, 1f, 3600f);
            naturalTimeRatio = Numeric.Clamp(naturalTimeRatio, 0.05f, 0.9f);
            naturalVolumeRatio = Numeric.Clamp(naturalVolumeRatio, 0.01f, 0.9f);
            upPressureKpa = Numeric.Clamp(upPressureKpa, 0, 25);
            vacuumKpa = Numeric.Clamp(vacuumKpa, 0, 20);
            pressureResponseSeconds = Numeric.Clamp(pressureResponseSeconds,.1f,5);
            flowResistance = Numeric.Clamp(flowResistance,.2f,5);
            powderResistance = Numeric.Clamp(powderResistance,.2f,5);
        }
        public SimulationSettings Copy() => (SimulationSettings)MemberwiseClone();
    }

    [Serializable]
    public sealed class TwinEvent
    {
        public string time;
        public double simulationSeconds;
        public string type;
        public string summary;
        public string recipe;
        public bool gpio15;
        public bool gpio16;
        public float lowerLitres;
        public float upperLitres;
        public float channelLitres;
        public float pressureKpa;
    }
}
