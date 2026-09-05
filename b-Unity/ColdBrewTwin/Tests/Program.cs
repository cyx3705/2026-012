using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OneHistory.ColdBrewTwin;

static class Program
{
    static int passed;
    static AnnularChannelProfile channel;
    static BrewSimulation Create(SimulationSettings settings) => new BrewSimulation(settings,channel);
    static void Check(bool value,string message) { if(!value) throw new Exception(message); passed++; }
    static void Near(double a,double b,string message) => Check(Math.Abs(a-b)<.00001,message+$": {a} != {b}");
    static void Conserved(BrewSimulation sim)
    {
        var s=sim.State;
        Near(s.LowerLitres+s.UpperLitres+s.ChannelLitres,s.TotalLitres,"Three-volume conservation");
        Check(s.LowerLitres>=0 && s.UpperLitres>=0 && s.ChannelLitres>=0,"Nonnegative volumes");
        Check(s.ChannelLitres<=channel.Capacity+.00001,"Channel bounded");
        if(s.Phase==CyclePhase.Down) Near(s.ChannelLitres,0,"Down keeps outer channel empty");
        Check(!(s.Gpio15 && s.Gpio16),"Pump interlock");
    }
    static void Main()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"AnnularChannel.json")));
        channel=new AnnularChannelProfile(json.RootElement.GetProperty("profile").EnumerateArray().Select(p=>
            new AnnularChannelProfile.Slice(p.GetProperty("x").GetDouble(),p.GetProperty("y").GetDouble(),p.GetProperty("z").GetDouble())).ToArray());
        for(int i=0;i<=1000;i++)
        {
            double v=channel.Capacity*i/1000;
            Near(channel.VolumeBelow(channel.LevelForVolume(v)),v,"Measured profile volume-height inversion");
        }
        Check(channel.At(.18).Outer<.061 && channel.At(.035).Inner>.065,"Connector narrows away from cup walls");
        for(int i=0;i<channel.Count;i++)
            Check(channel[i].Outer-channel[i].Inner<=.0006301,"Wet gap never expands into connector or shoulder cavity");
        Check(channel.VolumeBelow(.2445)-channel.VolumeBelow(.15)<.025,"Connector cannot store a large liquid reservoir");
        Check(channel.Capacity<.1,"Narrow channel capacity below 100 mL");
        var settings=new SimulationSettings();
        var sim=Create(settings);
        Check(!sim.Busy && !sim.State.Gpio15 && !sim.State.Gpio16,"Cold start idle");
        Near(sim.State.LowerLitres,.5,"Initial lower volume");
        sim.Manual(MachineMode.Up); sim.Tick(.5);
        Check(sim.State.ChannelLitres>0 && sim.State.UpperLitres<.00001,"Prime channel before upper delivery");
        Check(sim.State.ChannelFrontY<HydraulicModel.ChannelRoof,"Rising front below rim");
        sim.Tick(8.5);
        Check(sim.State.UpperLitres>0 && sim.State.OutletFlowLps>0,"Overflow after channel primed");
        Conserved(sim);
        float held=sim.State.LowerLitres, heldChannel=sim.State.ChannelLitres, pressure=sim.State.PressureKpa;
        double elapsed=sim.State.ElapsedSeconds;
        sim.Pause(); sim.Tick(5);
        Near(sim.State.LowerLitres,held,"Pause freezes volume");
        Near(sim.State.ChannelLitres,heldChannel,"Pause freezes column");
        Near(sim.State.PressureKpa,pressure,"Pause freezes pressure");
        Near(sim.State.ElapsedSeconds,elapsed,"Pause freezes time");
        Check(!sim.State.Gpio15 && !sim.State.Gpio16 && sim.State.OutletFlowLps==0,"Pause stops flow and pumps");
        sim.Resume(); sim.Tick(27);
        Near(sim.State.LowerLitres,0,"Up empties lower with sufficient pressure");
        Near(sim.State.ChannelLitres,channel.Capacity,"Channel holdup retained");
        Conserved(sim);
        held=sim.State.LowerLitres; heldChannel=sim.State.ChannelLitres;
        float upperHeld=sim.State.UpperLitres;
        sim.Manual(MachineMode.Down);
        Near(sim.State.ChannelLitres,0,"Manual down entry immediately clears annulus");
        Near(sim.State.LowerLitres,held+heldChannel,"Riser holdup returns to lower at entry");
        Near(sim.State.UpperLitres,upperHeld,"Down entry retains upper liquid for powder return");
        sim.Pause(); sim.Tick(1); Conserved(sim);
        Near(sim.State.ChannelLitres,0,"Pause immediately after down entry keeps annulus empty");
        sim.Resume(); sim.Tick(.6);
        Check(sim.State.NaturalReturn && !sim.State.Gpio16,"Natural return pump off");
        sim.Tick(13);
        Check(sim.State.Gpio16 && !sim.State.NaturalReturn,"Vacuum stage pump on");
        sim.Tick(20);
        Near(sim.State.LowerLitres,.5,"Powder and annulus return to lower");
        Conserved(sim);

        var partial=Create(settings);
        partial.Manual(MachineMode.Up); partial.Tick(.5); partial.Stop();
        Check(partial.State.ChannelLitres>0,"Partial priming fixture");
        partial.Manual(MachineMode.Down); Conserved(partial);
        Near(partial.State.LowerLitres,partial.State.TotalLitres,"Partial column settles without lost volume");
        var boundary=Create(settings);
        boundary.Start(new BrewRecipe {upSeconds=12,upDwellSeconds=2,downSeconds=12,downDwellSeconds=2,cycles=1});
        boundary.Tick(13.98);
        Check(boundary.State.ChannelLitres>0,"Upper dwell retains annulus");
        boundary.Tick(.02);
        Check(boundary.State.Phase==CyclePhase.Down,"Exact automatic down boundary");
        Near(boundary.State.ChannelLitres,0,"Automatic boundary clears annulus before next tick");
        Conserved(boundary);

        var lowSettings=new SimulationSettings {upPressureKpa=.8f};
        var low=Create(lowSettings); low.Manual(MachineMode.Up); low.Tick(12);
        Near(low.State.UpperLitres,0,"Insufficient pressure cannot reach rim");
        Check(low.State.ChannelFrontY<.20 && low.State.UpFlowLps<.001,"Low pressure stalls at hydrostatic head");
        low.SetPressure(12,6); low.Tick(10);
        Check(low.State.UpperLitres>.1,"Live pressure increase restarts flow");
        Conserved(low);
        var zero=Create(new SimulationSettings {upPressureKpa=0});
        zero.Manual(MachineMode.Up); zero.Tick(10);
        Near(zero.State.LowerLitres,.5,"Zero pressure does not magically transfer");
        zero.Stop(); zero.Tick(double.NaN); zero.Tick(double.PositiveInfinity);
        Check(!zero.Busy,"Invalid ticks ignored after stop");

        var runner=new SequenceRunner(); var phases=new List<CyclePhase>();
        runner.PhaseChanged+=phases.Add; runner.Start(new BrewRecipe()); runner.Tick(210);
        Check(phases.Count==12 && runner.Phase==CyclePhase.Complete,"Default four-stage three-cycle sequence");
        Near(runner.TotalElapsed,210,"Large tick time preserved");
        var many=BrewRecipe.QuickTest(); many.upDwellSeconds=many.downDwellSeconds=0; many.cycles=999;
        runner.Start(many); runner.Tick(5994);
        Check(!runner.IsRunning,"999 zero-dwell cycles terminate");
        var snapshot=BrewRecipe.QuickTest(); runner.Start(snapshot); snapshot.upSeconds=3600;
        runner.Tick(8); Check(!runner.IsRunning,"Recipe snapshot locked");

        string baseline=null;
        for(int run=0;run<10;run++)
        {
            sim=Create(settings); var events=new List<string>();
            sim.EventRaised+=(kind,text)=>events.Add(kind+":"+text);
            sim.Start(new BrewRecipe {upSeconds=12,upDwellSeconds=2,downSeconds=12,downDwellSeconds=2,cycles=1});
            while(sim.Busy) { sim.Tick(run%2==0?1.0/30:1.0/144); Conserved(sim); }
            string trace=string.Join("|",events); baseline??=trace;
            Check(trace==baseline,"Frame independent hydraulic event sequence");
            Near(sim.State.LowerLitres,.5,"Water demonstration final volume");
            sim.Tick(2.1); Check(sim.State.MachineMode==MachineMode.Idle,"Completion returns idle");
        }
        sim=Create(settings); sim.Start(new BrewRecipe()); sim.Tick(5); sim.Stop();
        held=sim.State.LowerLitres; heldChannel=sim.State.ChannelLitres;
        sim.Tick(10); Near(sim.State.LowerLitres,held,"Stop holds tank");
        Near(sim.State.ChannelLitres,heldChannel,"Stop holds riser");
        Check(sim.State.UpFlowLps==0 && sim.State.PressureKpa==0,"Stop cuts pressure and discharge");

        sim=Create(settings);
        for(int run=0;run<3600;run++)
        {
            sim.Start(BrewRecipe.QuickTest()); sim.Tick(8);
            Check(!sim.Busy,"Eight simulated hours complete each sequence");
            Conserved(sim);
        }
        Console.WriteLine($"PASS: {passed} assertions; pressure threshold, annular priming, overflow, conservation, pause, 10 deterministic runs, 8 simulated hours.");
    }
}
