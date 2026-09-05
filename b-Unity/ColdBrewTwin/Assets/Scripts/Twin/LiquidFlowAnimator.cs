using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHistory.ColdBrewTwin
{
    public sealed class LiquidFlowAnimator : MonoBehaviour
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Material> waterMaterials = new List<Material>();
        WaterBody lower, upper;
        MeshRenderer sleeve, meniscus, spill, returnJet, sectionFace, inlet;
        Mesh spillMesh, jetMesh, frontMesh;
        AnnularChannelProfile channel;
        Material sectionMaterial;
        Material lowerMaterial, upperMaterial, sleeveMaterial, spillMaterial, returnMaterial;
        ParticleSystem upperSpray, lowerSpray;
        Transform upperPlane, lowerPlane;
        Material powder;
        double previousClock, emissionUp, emissionDown;
        readonly System.Random random = new System.Random(2026012);
        float wetness;
        public bool ReducedMotion { get; set; }
        public float VisibleFront { get; private set; }
        public bool HasSpill => spill != null && spill.enabled;
        public bool HasReturn => returnJet != null && returnJet.enabled;

        public void Initialize(WaterBody bottom, WaterBody top, Material powderMaterial, AnnularChannelProfile channel)
        {
            this.channel=channel;
            lower=bottom; upper=top; powder=powderMaterial;
            lowerMaterial=Water(new Color(.29f,.10f,.035f,.94f));
            upperMaterial=Water(new Color(.33f,.12f,.045f,.94f));
            lower.GetComponent<Renderer>().sharedMaterial=lowerMaterial;
            upper.GetComponent<Renderer>().sharedMaterial=upperMaterial;
            sleeveMaterial=Water(new Color(.40f,.16f,.055f,.86f)); sleeveMaterial.SetFloat("_IsChannel",1);
            sleeve=Surface("AnnularLiquid_CADGap",sleeveMaterial,out var channelMesh);
            var profile=new List<Vector2>();
            for(int i=0;i<channel.Count;i++) profile.Add(new Vector2((float)channel[i].Y,(float)channel[i].Outer));
            for(int i=channel.Count-1;i>=0;i--) profile.Add(new Vector2((float)channel[i].Y,(float)channel[i].Inner));
            profile.Add(profile[0]);
            Lathe(channelMesh,profile.ToArray(),128);
            var frontMaterial=Water(new Color(.56f,.28f,.10f,1)); frontMaterial.SetFloat("_IsChannel",1);
            meniscus=Surface("AnnularMeniscus",frontMaterial,out frontMesh);
            sectionMaterial=new Material(Resources.Load<Shader>("TwinChannelSection")); owned.Add(sectionMaterial);
            sectionFace=Surface("AnnularCutFace",sectionMaterial,out var cutMesh);
            BuildCutFace(cutMesh);
            var inletMaterial=Water(new Color(.40f,.16f,.055f,.86f)); inletMaterial.SetFloat("_IsChannel",1);
            inlet=Surface("LowerAnnularInlet",inletMaterial,out var inletMesh);
            Lathe(inletMesh,new[] {new Vector2(.023f,.0515f),new Vector2(.021f,.05236f),
                new Vector2(.019f,.05323f),new Vector2(.017f,.05518f),new Vector2(.0165f,.05612f),
                new Vector2(.016f,.0563f),new Vector2(.0156f,.05925f),new Vector2(.016f,.0617f),
                new Vector2(.0165f,.06235f),new Vector2(.017f,.0633f),new Vector2(.019f,.06527f),
                new Vector2(.021f,.06613f),new Vector2(.025f,(float)((channel[0].Inner+channel[0].Outer)*.5))},128);
            spillMaterial=Water(new Color(.39f,.15f,.055f,.80f));
            spill=Surface("UpperOverflowSheet",spillMaterial,out spillMesh);
            returnMaterial=Water(new Color(.30f,.095f,.028f,.93f));
            returnJet=Surface("PowderReturnWater",returnMaterial,out jetMesh);
            upperSpray=Spray("UpperImpactDroplets",out upperPlane);
            lowerSpray=Spray("LowerImpactDroplets",out lowerPlane);
        }

        Material Water(Color color)
        {
            var material=new Material(Resources.Load<Shader>("TwinWater")); material.color=color;
            owned.Add(material); waterMaterials.Add(material); return material;
        }
        MeshRenderer Surface(string name,Material material,out Mesh mesh)
        {
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); obj.transform.SetParent(transform,false);
            mesh=new Mesh { name=name }; mesh.MarkDynamic(); owned.Add(mesh);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }
        static void Lathe(Mesh mesh,Vector2[] profile,int count)
        {
            var vertices=new Vector3[profile.Length*(count+1)];
            var uv=new Vector2[vertices.Length];
            var indices=new int[(profile.Length-1)*count*6]; int at=0;
            for(int j=0;j<profile.Length;j++)
                for(int i=0;i<=count;i++)
                {
                    float a=i*Mathf.PI*2/count; int v=j*(count+1)+i;
                    vertices[v]=new Vector3(Mathf.Cos(a)*profile[j].y,profile[j].x,Mathf.Sin(a)*profile[j].y);
                    uv[v]=new Vector2((float)i/count,(float)j/(profile.Length-1));
                    if(j==profile.Length-1 || i==count) continue;
                    int next=v+count+1;
                    indices[at++]=v; indices[at++]=next; indices[at++]=v+1;
                    indices[at++]=v+1; indices[at++]=next; indices[at++]=next+1;
                }
            mesh.Clear(); mesh.indexFormat=vertices.Length>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=indices;
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        }
        void BuildCutFace(Mesh mesh)
        {
            var vertices=new Vector3[channel.Count*4];
            var triangles=new int[(channel.Count-1)*12]; int at=0;
            for(int side=0;side<2;side++) for(int i=0;i<channel.Count;i++)
            {
                int v=side*channel.Count*2+i*2; float sign=side==0?1:-1;
                vertices[v]=new Vector3(sign*(float)channel[i].Inner,(float)channel[i].Y,0);
                vertices[v+1]=new Vector3(sign*(float)channel[i].Outer,(float)channel[i].Y,0);
                if(i==channel.Count-1) continue;
                triangles[at++]=v; triangles[at++]=v+2; triangles[at++]=v+1;
                triangles[at++]=v+1; triangles[at++]=v+2; triangles[at++]=v+3;
            }
            mesh.vertices=vertices; mesh.triangles=triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        ParticleSystem Spray(string name,out Transform plane)
        {
            var obj=new GameObject(name); obj.transform.SetParent(transform,false);
            var particles=obj.AddComponent<ParticleSystem>(); particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main; main.playOnAwake=false; main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=512; main.startLifetime=.20f; main.startSize=.0012f; main.startSpeed=0;
            main.gravityModifier=.6f; main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            particles.useAutoRandomSeed=false; particles.randomSeed=2026012;
            var emission=particles.emission; emission.enabled=false;
            var shape=particles.shape; shape.enabled=false;
            var material=new Material(Resources.Load<Shader>("TwinDroplets")); owned.Add(material);
            var renderer=obj.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Stretch; renderer.lengthScale=1.4f; renderer.velocityScale=.015f;
            var surface=new GameObject(name+"CollisionPlane"); surface.transform.SetParent(transform,false); plane=surface.transform;
            var collision=particles.collision; collision.enabled=true; collision.type=ParticleSystemCollisionType.Planes;
            collision.SetPlane(0,plane); collision.bounce=.08f; collision.dampen=.7f; collision.lifetimeLoss=.75f;
            return particles;
        }

        public void Apply(TwinRuntimeState state,double clock)
        {
            double dt=Math.Max(0,clock-previousClock); previousClock=clock;
            if(state.IsPaused) return;
            float flow=state.UpFlowLps+state.ReturnFlowLps;
            float amplitude=ReducedMotion ? 0 : Mathf.Min(.0012f,flow*.018f);
            SetSurface(lowerMaterial,lower,clock,amplitude);
            SetSurface(upperMaterial,upper,clock,amplitude);
            foreach(var material in waterMaterials)
            {
                material.SetFloat("_Clock",ReducedMotion?0:(float)clock);
                material.SetFloat("_Travel",ReducedMotion?0:(float)state.FlowTravel);
                material.SetFloat("_Moving",flow>.00001f?1:0);
            }
            VisibleFront=state.ChannelFrontY;
            sleeve.enabled=state.ChannelLitres>.000001f;
            sleeveMaterial.SetFloat("_Ceiling",VisibleFront);
            sectionMaterial.SetFloat("_Ceiling",VisibleFront);
            sectionMaterial.SetFloat("_Travel",ReducedMotion?0:(float)state.FlowTravel);
            sectionMaterial.SetFloat("_Moving",flow>.00001f?1:0);
            meniscus.enabled=sleeve.enabled;
            var front=channel.At(VisibleFront);
            Lathe(frontMesh,new[] {new Vector2(0,(float)front.Inner),new Vector2(0,(float)front.Outer)},128);
            meniscus.transform.localPosition=new Vector3(0,VisibleFront,0);
            bool up=state.IsRunning && state.Phase==CyclePhase.Up && state.OutletFlowLps>.00001f;
            bool down=state.IsRunning && state.Phase==CyclePhase.Down && state.ReturnFlowLps>.00001f;
            inlet.enabled=state.IsRunning && state.Phase==CyclePhase.Up && state.UpFlowLps>.00001f;
            spill.enabled=up; returnJet.enabled=down;
            if(up) BuildOverflow(upper.SurfaceY,state.OutletFlowLps,(float)clock);
            if(down) BuildReturn(lower.SurfaceY,state.ReturnFlowLps,(float)clock);
            if(state.UpperLitres>.001f && state.Phase==CyclePhase.Down) wetness=Mathf.Min(1,wetness+(float)dt*.3f);
            if(state.Phase==CyclePhase.None && state.ElapsedSeconds==0) wetness=0;
            if(powder!=null) powder.color=Color.Lerp(new Color(.28f,.14f,.07f),new Color(.105f,.064f,.031f),wetness);
            upperPlane.position=new Vector3(0,upper.SurfaceWorldY,0); lowerPlane.position=new Vector3(0,lower.SurfaceWorldY,0);
            if(!state.IsRunning)
            {
                upperSpray.Clear(); lowerSpray.Clear(); emissionUp=emissionDown=0;
            }
            else if(dt>0)
            {
                upperSpray.Simulate((float)dt,true,false,false); lowerSpray.Simulate((float)dt,true,false,false);
                if(!ReducedMotion)
                {
                    Emit(upperSpray,ref emissionUp,state.OutletFlowLps*dt*4000,upper.SurfaceWorldY,.045f);
                    Emit(lowerSpray,ref emissionDown,state.ReturnFlowLps*dt*3000,lower.SurfaceWorldY,.005f);
                }
                else { upperSpray.Clear(); lowerSpray.Clear(); }
                upperSpray.Pause(); lowerSpray.Pause();
            }
        }
        void Emit(ParticleSystem ps,ref double carry,double amount,float y,float radius)
        {
            carry+=amount; int count=Math.Min(80,(int)carry); carry-=count;
            for(int i=0;i<count;i++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2;
                var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                ps.Emit(new ParticleSystem.EmitParams { position=radial*radius+Vector3.up*(y+.001f),
                    velocity=-radial*(.01f+(float)random.NextDouble()*.025f)+Vector3.up*(.03f+(float)random.NextDouble()*.07f),
                    startSize=.0007f+(float)random.NextDouble()*.0011f,
                    startLifetime=.12f+(float)random.NextDouble()*.16f,
                    startColor=new Color(.55f,.26f,.09f,.8f) },1);
            }
        }
        static void SetSurface(Material material,WaterBody body,double clock,float amplitude)
        {
            material.SetFloat("_SurfaceY",body.SurfaceY);
            material.SetFloat("_Radius",body.Cavity.RadiusAt(body.SurfaceY));
            material.SetFloat("_Amplitude",Mathf.Min(amplitude,
                Mathf.Min(body.SurfaceY-body.Cavity.FloorY,body.Cavity.CeilingY-body.SurfaceY)*.35f));
        }
        void BuildOverflow(float surface,float rate,float time)
        {
            var profile=new List<Vector2> {
                new Vector2(.373f,(float)((channel[channel.Count-1].Inner+channel[channel.Count-1].Outer)*.5)),
                new Vector2(.375f,.06527f),new Vector2(.377f,.0633f),new Vector2(.3775f,.06235f),
                new Vector2(.378f,.0617f),new Vector2(.3784f,.05925f),new Vector2(.378f,.0563f),
                new Vector2(.3775f,.05612f),new Vector2(.377f,.05518f),new Vector2(.375f,.05323f),
                new Vector2(.373f,.05236f),new Vector2(.371f,.0515f) };
            float bottom=Mathf.Min(.371f,surface+.001f);
            for(int i=1;i<=24;i++)
            {
                float t=i/24f;
                float wave=ReducedMotion?0:Mathf.Sin(t*27-time*11)*.00035f*t;
                profile.Add(new Vector2(Mathf.Lerp(.371f,bottom,t),Mathf.Lerp(.0515f,.0435f,t)+wave));
            }
            Lathe(spillMesh,profile.ToArray(),96);
        }
        void BuildReturn(float surface,float rate,float time)
        {
            float radius=Mathf.Clamp(Mathf.Sqrt(rate*.001f/(Mathf.PI*.7f)),.0007f,.007f);
            var profile=new List<Vector2> {new Vector2(.226f,.026f),new Vector2(.219f,.009f),
                new Vector2(.211f,radius),new Vector2(.156f,radius)};
            float bottom=Mathf.Min(.153f,surface+.001f);
            for(int i=1;i<=20;i++)
            {
                float t=i/20f;
                float disturbance=ReducedMotion?0:Mathf.Sin(t*32-time*12)*.00015f;
                profile.Add(new Vector2(Mathf.Lerp(.156f,bottom,t),radius*(1-t*.3f)+disturbance));
            }
            Lathe(jetMesh,profile.ToArray(),32);
        }
        public void SetSection(bool enabled,Vector3 normal)
        {
            sectionFace.enabled=enabled && sleeve.enabled;
            sectionFace.transform.localRotation=Quaternion.FromToRotation(Vector3.forward,normal);
            sectionFace.transform.localPosition=normal*.000015f;
            foreach(var material in waterMaterials)
            { material.SetFloat("_Section",enabled?1:0); material.SetVector("_CutNormal",normal); }
        }
        void OnDestroy() { foreach(var obj in owned) if(obj!=null) Destroy(obj); }
    }
}
