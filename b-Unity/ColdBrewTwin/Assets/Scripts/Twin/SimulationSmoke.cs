using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneHistory.ColdBrewTwin
{
    // Opt-in player verification and animation capture with isolated application data.
    public sealed class SimulationSmoke : MonoBehaviour
    {
        TwinApplication app;
        string output;
        readonly List<string> checks=new List<string>();
        readonly List<string> errors=new List<string>();
        ulong lastViewportHash;
        Color32[] viewportSamples;
        int movingFrames;
        void Awake() => Application.logMessageReceived+=OnLog;
        void OnLog(string message,string trace,LogType type)
        { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(message+"\n"+trace); }
        IEnumerator Start()
        {
            bool appearance=Array.IndexOf(Environment.GetCommandLineArgs(),"--appearance-smoke")>=0;
            output=Path.GetFullPath(Path.Combine(Application.dataPath,appearance?"../../AppearanceSmoke":"../../FlowSmoke"));
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"frames"));
            app=GetComponent<TwinApplication>(); app.enabled=false;
            Screen.SetResolution(1440,900,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.6f);
            app.Machine.Apply(app.Simulation.State,0);
            Check(FindObjectsOfType<LineRenderer>().Length==0,"No virtual line renderer");
            Check(!app.Simulation.Busy && app.Simulation.State.ChannelLitres==0,"Cold start idle");
            yield return Capture("01-idle");
            if(appearance)
            {
                Check(!app.Machine.Section,"Full exterior by default");
                Click("中部泵组");
                Check(!app.Machine.Section,"View change preserves closed section");
                yield return Capture("appearance-battery-motors");
                Click("整机");
                var cups=FindObjectsOfType<MeshRenderer>().Where(r=>r.name.StartsWith("2-1") || r.name.StartsWith("2-3") ||
                    r.name.StartsWith("4-1") || r.name.StartsWith("4-3")).ToDictionary(r=>r,r=>r.sharedMaterial);
                var toggle=FindObjectsOfType<Toggle>().First(t=>t.name=="剖视");
                ExecuteEvents.Execute(toggle.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                Check(app.Machine.Section,"Section toggle opens cutaway");
                yield return Capture("appearance-section-open");
                Check(cups.Count==4 && cups.Keys.All(r=>r.sharedMaterial.GetTag("RenderType",false)=="Opaque" &&
                    r.sharedMaterial.color.a==1),"Both cups and liners become opaque in section");
                Click("上部液仓"); yield return Capture("appearance-section-upper");
                Click("整机");
                ExecuteEvents.Execute(toggle.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                Check(!app.Machine.Section,"Section toggle restores full exterior");
                yield return Capture("appearance-section-closed");
                Check(cups.All(pair=>pair.Key.sharedMaterial==pair.Value),"Closing section restores original glass materials");
                foreach(var renderer in FindObjectsOfType<MeshRenderer>())
                    Check(renderer.sharedMaterial.shader.isSupported,"Supported material: "+renderer.name);
            }
            Click("开始水流演示"); Step(.6);
            Check(app.Simulation.State.ChannelLitres>0 && app.Simulation.State.UpperLitres<.0001f,"Channel primes before overflow");
            Check(app.Machine.Flow.VisibleFront<HydraulicModel.ChannelRoof,"Water front starts below top");
            yield return Capture("02-rising-column");
            Step(.9);
            if(appearance)
            {
                app.Machine.Section=true; yield return Capture("channel-coffee-rising"); app.Machine.Section=false;
            }
            Step(7.5);
            Check(app.Machine.Flow.HasSpill && app.Simulation.State.UpperLitres>0,"Real upper overflow sheet");
            yield return Capture("03-top-overflow");
            if(appearance)
            {
                app.Machine.Section=true;
                yield return Capture("channel-coffee-section");
                app.Machine.SetView(1); yield return Capture("channel-coffee-upper");
                app.Machine.SetView(0); app.Machine.Section=false;
            }
            float held=app.Simulation.State.LowerLitres;
            Click("暂停"); app.Machine.Apply(app.Simulation.State,app.Simulation.Clock);
            yield return Capture("04-pause-before"); var pausedPixels=viewportSamples;
            Step(3); yield return Capture("05-pause-after");
            // MSAA and transparent surface blending can round a channel by one byte between frames.
            Check(PixelDelta(pausedPixels,viewportSamples)<=1,"Paused viewport frozen within one color level");
            Check(Math.Abs(app.Simulation.State.LowerLitres-held)<.00001f,"Paused volume frozen");
            Click("暂停"); Step(5.6);
            Check(app.Simulation.State.NaturalReturn && !app.Simulation.State.Gpio16 && app.Machine.Flow.HasReturn,"Gravity through powder");
            yield return Capture("06-natural-return");
            CheckOuterChannelEmpty();
            app.Machine.Section=true;
            yield return Capture("down-empty-outer-section");
            CheckOuterChannelEmpty();
            app.Machine.Section=false;
            Step(4.4);
            Check(app.Simulation.State.Gpio16 && app.Simulation.State.PressureKpa<0,"Vacuum accelerates return");
            CheckOuterChannelEmpty();
            yield return Capture("07-vacuum-return");
            Click("软件停止"); Step(1);
            Check(!app.Machine.Flow.HasSpill && !app.Machine.Flow.HasReturn,"Stop removes active jets");

            app.ResetLiquid(); app.SetPressure(.8f,6); app.Simulation.Manual(MachineMode.Up); Step(10);
            Check(app.Simulation.State.UpperLitres<.00001f && app.Simulation.State.ChannelFrontY<.20f,"Low pressure stalls below rim");
            yield return Capture("08-low-pressure");
            app.SetPressure(12,6); Step(8);
            Check(app.Machine.Flow.HasSpill,"Live pressure increase creates overflow");
            app.Simulation.Stop();
            app.Machine.SetView(1); app.Machine.Apply(app.Simulation.State,app.Simulation.Clock);
            yield return Capture("09-upper-cavity");
            app.Machine.SetView(2); yield return Capture("10-powder");
            app.Machine.SetView(3); yield return Capture("11-pumps");

            app.Machine.SetView(0); app.ResetLiquid();
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.4f);
            app.StartFlowDemo(); app.Machine.Apply(app.Simulation.State,app.Simulation.Clock);
            for(int frame=0;frame<=140;frame++)
            {
                if(frame>0) Step(.2);
                var s=app.Simulation.State;
                Check(Math.Abs(s.LowerLitres+s.UpperLitres+s.ChannelLitres-s.TotalLitres)<.00001f,"Conservation frame "+frame);
                ulong previous=lastViewportHash;
                yield return Capture("frames/"+frame.ToString("D4"));
                if(previous!=lastViewportHash) movingFrames++;
            }
            Check(movingFrames>80,"Animation changes across captured frames");
            Check(!app.Simulation.Busy && app.Simulation.State.LowerLitres>.499f,"Demo returns liquid to lower");
            app.Machine.ReducedMotion=true; app.Simulation.Manual(MachineMode.Up); Step(8);
            yield return Capture("12-reduced-motion");
            Check(app.Machine.Flow.HasSpill,"Reduced effects retain real liquid");
            app.Simulation.Stop();
            Check(errors.Count==0,"No Unity runtime errors");
            File.WriteAllText(Path.Combine(output,"flow-result.txt"),string.Join("\n",checks)+"\n"+string.Join("\n",errors));
            Application.Quit(errors.Count==0 && checks.All(c=>c.StartsWith("PASS"))?0:1);
        }
        void Step(double seconds)
        {
            app.Simulation.Tick(seconds);
            app.Machine.Apply(app.Simulation.State,app.Simulation.Clock);
        }
        void CheckOuterChannelEmpty()
        {
            Check(app.Simulation.State.ChannelLitres==0,"Down has no outer channel liquid");
            foreach(string name in new[] {"AnnularLiquid_CADGap","AnnularMeniscus","AnnularCutFace","LowerAnnularInlet","UpperOverflowSheet"})
            {
                var renderer=FindObjectsOfType<MeshRenderer>().FirstOrDefault(r=>r.name==name);
                Check(renderer!=null && !renderer.enabled,"Down hides "+name);
            }
        }
        void Check(bool ok,string text) { checks.Add((ok?"PASS ":"FAIL ")+text); }
        static int PixelDelta(Color32[] a,Color32[] b)
        {
            if(a.Length!=b.Length) return 255;
            int maximum=0;
            for(int i=0;i<a.Length;i++) maximum=Math.Max(maximum,Math.Max(Math.Abs(a[i].r-b[i].r),
                Math.Max(Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b))));
            return maximum;
        }
        void Click(string name)
        {
            Canvas.ForceUpdateCanvases();
            var button=FindObjectsOfType<Button>().FirstOrDefault(b=>b.gameObject.name==name);
            Check(button!=null && button.IsInteractable(),"Button available: "+name);
            if(button==null || !button.IsInteractable()) return;
            var rect=button.GetComponent<RectTransform>();
            var pointer=new PointerEventData(EventSystem.current)
            {position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==button,"Button not occluded: "+name);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Capture(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
            var rect=app.Machine.InteractionRect; var pixels=texture.GetPixels32(); var colors=new HashSet<int>();
            var samples=new List<Color32>();
            ulong hash=1469598103934665603UL;
            for(int y=(int)rect.y+12;y<rect.yMax-12;y+=5)
                for(int x=(int)rect.x+12;x<rect.xMax-12;x+=5)
                {
                    var c=pixels[y*texture.width+x]; int color=(c.r<<16)|(c.g<<8)|c.b;
                    samples.Add(c);
                    unchecked { hash=(hash^(uint)color)*1099511628211UL; }
                    colors.Add((c.r/8<<10)|(c.g/8<<5)|c.b/8);
                }
            lastViewportHash=hash;
            viewportSamples=samples.ToArray();
            Check(colors.Count>40,"Nonblank viewport "+name);
            Destroy(texture);
        }
        void OnDestroy() => Application.logMessageReceived-=OnLog;
    }
}
