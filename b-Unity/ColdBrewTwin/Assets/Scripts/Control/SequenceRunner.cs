using System;

namespace OneHistory.ColdBrewTwin
{
    public sealed class SequenceRunner
    {
        BrewRecipe recipe;
        CyclePhase phase = CyclePhase.None;
        double elapsed;
        double totalElapsed;
        int cycle;
        bool paused;

        public event Action<MachineMode> DesiredModeChanged;
        public event Action<CyclePhase> PhaseChanged;
        public event Action TimeAdvanced;
        public event Action<bool, string> Finished;

        public bool IsRunning => recipe != null && phase != CyclePhase.Complete && phase != CyclePhase.Aborted;
        public bool IsPaused => paused;
        public CyclePhase Phase => phase;
        public int CurrentCycle => cycle;
        public int TotalCycles => recipe != null ? recipe.cycles : 0;
        public float Elapsed => (float)elapsed;
        public double TotalElapsed => totalElapsed;
        public double TotalDuration => recipe == null ? 0 :
            ((double)recipe.upSeconds + recipe.upDwellSeconds + recipe.downSeconds + recipe.downDwellSeconds) * recipe.cycles;
        public double Remaining => Math.Max(0, TotalDuration - totalElapsed);
        public float Duration => DurationFor(phase);
        public float Progress => Duration <= 0f ? 1f : Numeric.Clamp((float)(elapsed / Duration), 0f, 1f);

        public void Start(BrewRecipe source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (IsRunning) throw new InvalidOperationException("A sequence is already running.");
            recipe = source.Copy();
            recipe.Clamp();
            cycle = 1;
            elapsed = 0f;
            totalElapsed = 0;
            paused = false;
            Enter(CyclePhase.Up);
        }

        public void Tick(double deltaTime)
        {
            if (!IsRunning || paused || deltaTime < 0 || double.IsNaN(deltaTime) || double.IsInfinity(deltaTime)) return;
            // Apply the exact interval in each phase before consuming the next phase.
            while (IsRunning && !paused)
            {
                double step = Math.Min(deltaTime, Math.Max(0, Duration - elapsed));
                elapsed += step;
                totalElapsed += step;
                deltaTime -= step;
                TimeAdvanced?.Invoke();
                if (elapsed + 1e-9 < Duration) break;
                Advance();
                if (deltaTime <= 0 && Duration > 0) break;
            }
        }

        public void Pause()
        {
            if (!IsRunning || paused) return;
            paused = true;
            DesiredModeChanged?.Invoke(MachineMode.Idle);
        }

        public void Resume()
        {
            if (!IsRunning || !paused) return;
            paused = false;
            DesiredModeChanged?.Invoke(ModeFor(phase));
        }

        public void Abort(string reason)
        {
            if (!IsRunning) return;
            paused = false;
            phase = CyclePhase.Aborted;
            DesiredModeChanged?.Invoke(MachineMode.Idle);
            Finished?.Invoke(false, string.IsNullOrEmpty(reason) ? "用户终止" : reason);
        }

        float DurationFor(CyclePhase value)
        {
            if (recipe == null) return 0f;
            switch (value)
            {
                case CyclePhase.Up: return recipe.upSeconds;
                case CyclePhase.UpDwell: return recipe.upDwellSeconds;
                case CyclePhase.Down: return recipe.downSeconds;
                case CyclePhase.DownDwell: return recipe.downDwellSeconds;
                default: return 0f;
            }
        }

        void Advance()
        {
            switch (phase)
            {
                case CyclePhase.Up: Enter(CyclePhase.UpDwell); break;
                case CyclePhase.UpDwell: Enter(CyclePhase.Down); break;
                case CyclePhase.Down: Enter(CyclePhase.DownDwell); break;
                case CyclePhase.DownDwell:
                    if (cycle >= recipe.cycles)
                    {
                        totalElapsed = TotalDuration;
                        phase = CyclePhase.Complete;
                        DesiredModeChanged?.Invoke(MachineMode.Idle);
                        Finished?.Invoke(true, string.Empty);
                    }
                    else
                    {
                        cycle++;
                        Enter(CyclePhase.Up);
                    }
                    break;
            }
        }

        void Enter(CyclePhase value)
        {
            phase = value;
            elapsed = 0f;
            PhaseChanged?.Invoke(value);
            DesiredModeChanged?.Invoke(ModeFor(value));
        }

        static MachineMode ModeFor(CyclePhase value)
        {
            if (value == CyclePhase.Up) return MachineMode.Up;
            if (value == CyclePhase.Down) return MachineMode.Down;
            return MachineMode.Idle;
        }
    }
}
