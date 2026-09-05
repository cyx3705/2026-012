using System;

namespace OneHistory.ColdBrewTwin
{
    // Lumped pneumatic reservoir plus Bernoulli head/loss model, in L, m, s and kPa.
    // Conductance and air response require hardware calibration; this is not a CFD solver.
    public sealed class HydraulicModel
    {
        public const double ChannelFloor = .025;
        public const double ChannelRoof = .373;
        readonly AnnularChannelProfile channel;
        public double Lower { get; private set; }
        public double Upper { get; private set; }
        public double Channel { get; private set; }
        public double Pressure { get; private set; }
        public double UpFlow { get; private set; }
        public double OutletFlow { get; private set; }
        public double ReturnFlow { get; private set; }
        public double DrainFlow { get; private set; }
        public double HeadKpa { get; private set; }
        public double FrontY => channel.LevelForVolume(Channel);
        public double Throughput { get; private set; }
        readonly Func<double, double> lowerLevel;
        readonly Func<double, double> upperLevel;

        public HydraulicModel(AnnularChannelProfile channel,Func<double,double> lowerLevel = null, Func<double,double> upperLevel = null)
        {
            this.channel=channel ?? throw new ArgumentNullException(nameof(channel));
            this.lowerLevel = lowerLevel ?? (volume => .0225 + .13 * Math.Min(1,volume/1.5317));
            this.upperLevel = upperLevel ?? (volume => .253 + .121 * Math.Min(1,volume/1.4399));
        }
        public void Reset(double litres)
        {
            Lower = litres; Upper = Channel = Throughput = 0; Halt();
        }
        public void Halt()
        {
            Pressure = HeadKpa = UpFlow = OutletFlow = ReturnFlow = DrainFlow = 0;
        }
        public void FreezeFlow() { UpFlow = OutletFlow = ReturnFlow = DrainFlow = 0; }
        public void EnterPhase(CyclePhase phase)
        {
            FreezeFlow();
            if (phase != CyclePhase.Down) return;
            // The offline model settles riser holdup into the lower tank at down-entry.
            Lower += Channel;
            Channel = 0;
        }
        public void Step(double dt, CyclePhase phase, bool natural, SimulationSettings settings)
        {
            if (dt<=0 || double.IsNaN(dt) || double.IsInfinity(dt)) return;
            bool up = phase == CyclePhase.Up, down = phase == CyclePhase.Down;
            double target = up ? settings.upPressureKpa : down && !natural ? -settings.vacuumKpa : 0;
            Pressure = target + (Pressure-target) * Math.Exp(-dt/settings.pressureResponseSeconds);
            FreezeFlow();
            if (up)
            {
                HeadKpa = 9.80665 * Math.Max(0,FrontY-lowerLevel(Lower));
                double rate = .0175 * Math.Sqrt(Math.Max(0,Pressure-HeadKpa)) / Math.Sqrt(settings.flowResistance);
                double taken = Math.Min(Lower,rate*dt);
                double prime = Math.Min(taken,Math.Max(0,channel.Capacity-Channel));
                Lower -= taken; Channel += prime; Upper += taken-prime;
                UpFlow = taken/dt; OutletFlow = (taken-prime)/dt;
                Throughput += taken;
            }
            else if (down)
            {
                HeadKpa = 9.80665 * Math.Max(0,upperLevel(Upper)-lowerLevel(Lower));
                double drive = Math.Max(0,HeadKpa-Pressure);
                double rate = .022 * Math.Sqrt(drive) / Math.Sqrt(settings.powderResistance);
                double taken = Math.Min(Upper,rate*dt);
                Upper -= taken; Lower += taken;
                ReturnFlow = taken/dt;
                Throughput += taken;
            }
        }
    }
}
