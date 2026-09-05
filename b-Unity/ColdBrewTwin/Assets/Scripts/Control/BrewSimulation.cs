using System;

namespace OneHistory.ColdBrewTwin
{
    public sealed class BrewSimulation
    {
        public TwinRuntimeState State { get; } = new TwinRuntimeState();
        public SequenceRunner Sequence { get; } = new SequenceRunner();
        public event Action<string, string> EventRaised;
        public event Action<bool, string, int> RunFinished;
        SimulationSettings settings;
        readonly HydraulicModel hydraulic;
        double sequenceTime;
        bool manual, manualPaused;
        double manualElapsed, manualDuration, completionAge, accumulator;
        public double Clock { get; private set; }
        public bool Busy => Sequence.IsRunning || manual;
        public bool Paused => Sequence.IsPaused || manualPaused;

        public BrewSimulation(SimulationSettings initial, AnnularChannelProfile channel, Func<double,double> lowerLevel = null, Func<double,double> upperLevel = null)
        {
            hydraulic = new HydraulicModel(channel,lowerLevel,upperLevel);
            Reset(initial, 1.5317f);
            Sequence.PhaseChanged += phase =>
            {
                State.Phase = phase;
                hydraulic.EnterPhase(phase); PublishHydraulics();
                Sync();
                EventRaised?.Invoke("phase", phase.ToString());
            };
            Sequence.TimeAdvanced += () =>
            {
                double dt = Sequence.TotalElapsed-sequenceTime;
                sequenceTime = Sequence.TotalElapsed;
                ApplyTransfer(Sequence.Progress,dt); Sync();
            };
            Sequence.Finished += (ok, reason) =>
            {
                State.Phase = ok ? CyclePhase.Complete : CyclePhase.Aborted;
                hydraulic.Halt(); PublishHydraulics();
                State.Result = ok ? (State.UpperLitres+State.ChannelLitres>.001f ? "阶段结束，仍有余液" : "循环完成") : "已终止";
                completionAge = 0;
                Sync();
                EventRaised?.Invoke(ok ? "complete" : "abort", State.Result + " " + reason);
                RunFinished?.Invoke(ok, reason, ok ? Sequence.TotalCycles : Math.Max(0, Sequence.CurrentCycle - 1));
            };
        }

        public void Reset(SimulationSettings configuration, float capacity)
        {
            if (Busy) return;
            settings = configuration.Copy();
            settings.Clamp(capacity);
            State.TotalLitres = settings.totalLitres;
            State.EstimatedLowerFill = 1;
            hydraulic.Reset(settings.totalLitres);
            PublishHydraulics();
            State.Phase = CyclePhase.None;
            State.MachineMode = MachineMode.Idle;
            State.Gpio15 = State.Gpio16 = false;
            State.NaturalReturn = State.IsPaused = State.IsRunning = false;
            State.Result = State.Fault = string.Empty;
            State.ElapsedSeconds = State.RemainingSeconds = 0;
            State.PhaseElapsed = State.PhaseDuration = 0;
            State.CurrentCycle = State.TotalCycles = 0;
            accumulator = 0;
            EventRaised?.Invoke("reset", "重置液量");
        }

        public bool Start(BrewRecipe recipe)
        {
            if (Busy || State.IsFaulted || recipe == null) return false;
            State.Result = string.Empty;
            accumulator = 0;
            sequenceTime = 0;
            Sequence.Start(recipe);
            Sync();
            return true;
        }

        public bool Manual(MachineMode mode)
        {
            if (Busy || State.IsFaulted || (mode != MachineMode.Up && mode != MachineMode.Down)) return false;
            manual = true;
            manualPaused = false;
            manualElapsed = accumulator = 0;
            manualDuration = mode == MachineMode.Up ? settings.upSeconds : settings.downSeconds;
            State.Phase = mode == MachineMode.Up ? CyclePhase.Up : CyclePhase.Down;
            hydraulic.EnterPhase(State.Phase); PublishHydraulics();
            State.Result = string.Empty;
            State.CurrentCycle = State.TotalCycles = 0;
            Sync();
            EventRaised?.Invoke("manual", mode.ToString());
            return true;
        }

        public void Pause()
        {
            if (!Busy || Paused) return;
            if (manual) manualPaused = true; else Sequence.Pause();
            hydraulic.FreezeFlow(); PublishHydraulics();
            Sync();
            EventRaised?.Invoke("pause", "已暂停");
        }

        public void Resume()
        {
            if (!Paused) return;
            if (manual) manualPaused = false; else Sequence.Resume();
            Sync();
            EventRaised?.Invoke("resume", "继续");
        }

        public void Stop(string reason = "软件停止")
        {
            bool wasManual = manual;
            manual = manualPaused = false;
            if (Sequence.IsRunning) Sequence.Abort(reason);
            else if (wasManual)
            {
                State.Phase = CyclePhase.Aborted;
                State.Result = "已终止";
                Sync();
                EventRaised?.Invoke("stop", reason);
            }
            accumulator = 0;
            hydraulic.Halt(); PublishHydraulics();
            Sync();
        }

        public void Tick(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) || Paused) return;
            if (!Busy)
            {
                if (State.Phase == CyclePhase.Complete)
                {
                    completionAge += seconds;
                    if (completionAge >= 2) { State.Phase = CyclePhase.None; Sync(); }
                }
                return;
            }
            // A fixed simulation clock makes liquid transfer and event order independent of render FPS.
            accumulator += seconds;
            const double step = 0.02;
            while (accumulator + 1e-9 >= step && Busy && !Paused)
            {
                accumulator -= step;
                Clock += step;
                if (manual)
                {
                    double dt = Math.Min(step,manualDuration-manualElapsed);
                    manualElapsed += dt;
                    ApplyTransfer((float)(manualElapsed / manualDuration),dt);
                    Sync();
                    if (manualElapsed + 1e-9 >= manualDuration)
                    {
                        manual = false;
                        State.Phase = CyclePhase.None;
                        State.Result = "手动阶段结束";
                        hydraulic.Halt(); PublishHydraulics();
                        State.RemainingSeconds = 0;
                        Sync();
                        EventRaised?.Invoke("manual-complete", State.Result);
                    }
                }
                else Sequence.Tick(step);
                Sync();
            }
            if (!Busy) accumulator = 0;
        }

        public void SetPressure(float upKpa, float vacuumKpa)
        {
            settings.upPressureKpa = Numeric.Clamp(upKpa,0,25);
            settings.vacuumKpa = Numeric.Clamp(vacuumKpa,0,20);
        }
        void ApplyTransfer(float progress, double dt)
        {
            hydraulic.Step(dt,State.Phase,progress<settings.naturalTimeRatio,settings);
            PublishHydraulics();
        }
        void PublishHydraulics()
        {
            State.EstimatedLowerFill = State.TotalLitres>0 ? (float)(hydraulic.Lower/State.TotalLitres) : 0;
            State.ChannelLitres = (float)hydraulic.Channel;
            State.ChannelFrontY = (float)hydraulic.FrontY;
            State.PressureKpa = (float)hydraulic.Pressure;
            State.HeadKpa = (float)hydraulic.HeadKpa;
            State.UpFlowLps = (float)hydraulic.UpFlow;
            State.OutletFlowLps = (float)hydraulic.OutletFlow;
            State.ReturnFlowLps = (float)hydraulic.ReturnFlow;
            State.ChannelDrainLps = (float)hydraulic.DrainFlow;
            State.FlowTravel = hydraulic.Throughput;
        }

        void Sync()
        {
            bool oldUp = State.Gpio15, oldDown = State.Gpio16;
            State.IsRunning = Busy;
            State.IsPaused = Paused;
            if (Sequence.IsRunning || State.Phase == CyclePhase.Complete)
            {
                State.CurrentCycle = Sequence.CurrentCycle;
                State.TotalCycles = Sequence.TotalCycles;
            }
            if (Busy)
            {
                State.PhaseElapsed = manual ? (float)manualElapsed : Sequence.Elapsed;
                State.PhaseDuration = manual ? (float)manualDuration : Sequence.Duration;
                State.ElapsedSeconds = manual ? manualElapsed : Sequence.TotalElapsed;
                State.RemainingSeconds = manual ? manualDuration - manualElapsed : Sequence.Remaining;
            }
            else if (State.Phase == CyclePhase.Complete)
            {
                State.ElapsedSeconds = Sequence.TotalElapsed;
                State.RemainingSeconds = 0;
            }
            State.NaturalReturn = Busy && !Paused && State.Phase == CyclePhase.Down &&
                State.PhaseProgress < settings.naturalTimeRatio;
            State.Gpio15 = Busy && !Paused && State.Phase == CyclePhase.Up;
            State.Gpio16 = Busy && !Paused && State.Phase == CyclePhase.Down && !State.NaturalReturn;
            State.MachineMode = Paused ? MachineMode.Paused : State.Gpio15 ? MachineMode.Up :
                Busy && State.Phase == CyclePhase.Down ? MachineMode.Down :
                State.Phase == CyclePhase.Complete ? MachineMode.Complete : MachineMode.Idle;
            if (oldUp != State.Gpio15 || oldDown != State.Gpio16)
                EventRaised?.Invoke("gpio", "GPIO15=" + State.Gpio15 + " GPIO16=" + State.Gpio16);
        }
    }
}
