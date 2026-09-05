using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneHistory.ColdBrewTwin
{
    public sealed class TwinStudio : MonoBehaviour
    {
        readonly List<Object> owned=new List<Object>();
        Cubemap reflection;

        public void Initialize()
        {
            QualitySettings.antiAliasing=8;
            QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
            QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.High;
            QualitySettings.shadowDistance=3;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.76f,.80f,.84f);
            RenderSettings.ambientEquatorColor=new Color(.48f,.51f,.53f);
            RenderSettings.ambientGroundColor=new Color(.29f,.30f,.32f);
            foreach(var light in FindObjectsOfType<Light>()) light.enabled=false;
            Light("StudioKey",new Vector3(32,-35,0),new Color(1,.96f,.91f),1.1f,true);
            Light("StudioFill",new Vector3(28,140,0),new Color(.81f,.92f,1),.7f,false);
            Light("StudioRim",new Vector3(70,60,0),Color.white,.55f,false);

            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane); owned.Add(floor);
            floor.name="StudioFloor"; floor.transform.position=new Vector3(0,-.023f,0);
            floor.transform.localScale=Vector3.one*2;
            Destroy(floor.GetComponent<Collider>());
            var floorMaterial=new Material(Resources.Load<Shader>("TwinFloor")); owned.Add(floorMaterial);
            floorMaterial.SetColor("_Horizon",Camera.main.backgroundColor);
            floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            Plinth("DisplayPlinth",new[] {new Vector2(-.023f,.115f),new Vector2(-.021f,.121f),
                new Vector2(-.009f,.121f),new Vector2(-.006f,.117f),new Vector2(-.006f,0)},
                Material("GraphiteDisplay",new Color(.12f,.14f,.15f),.28f,.45f));
            Plinth("DisplayRim",new[] {new Vector2(-.019f,.1212f),new Vector2(-.0175f,.1212f)},
                Material("BrushedRim",new Color(.53f,.59f,.62f),.75f,.66f));
            BuildReflection();
        }

        void Light(string name,Vector3 angle,Color color,float intensity,bool shadows)
        {
            var obj=new GameObject(name); owned.Add(obj);
            obj.transform.rotation=Quaternion.Euler(angle);
            var light=obj.AddComponent<Light>(); light.type=LightType.Directional;
            light.color=color; light.intensity=intensity;
            light.shadows=shadows?LightShadows.Soft:LightShadows.None;
            light.shadowStrength=.55f; light.shadowBias=.012f; light.shadowNormalBias=.004f;
            light.shadowNearPlane=.02f;
        }
        Material Material(string name,Color color,float metallic,float smooth)
        {
            var mat=new Material(Shader.Find("Standard")) {name=name,color=color}; owned.Add(mat);
            mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Glossiness",smooth); return mat;
        }
        void Plinth(string name,Vector2[] profile,Material material)
        {
            const int segments=128;
            var vertices=new Vector3[profile.Length*(segments+1)];
            var indices=new int[(profile.Length-1)*segments*6]; int at=0;
            for(int j=0;j<profile.Length;j++) for(int i=0;i<=segments;i++)
            {
                int v=j*(segments+1)+i; float a=i*Mathf.PI*2/segments;
                vertices[v]=new Vector3(Mathf.Cos(a)*profile[j].y,profile[j].x,Mathf.Sin(a)*profile[j].y);
                if(j==profile.Length-1 || i==segments) continue;
                int next=v+segments+1;
                indices[at++]=v; indices[at++]=next; indices[at++]=v+1;
                indices[at++]=v+1; indices[at++]=next; indices[at++]=next+1;
            }
            var mesh=new Mesh {name=name}; owned.Add(mesh);
            mesh.vertices=vertices; mesh.triangles=indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); owned.Add(obj);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh; obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        void BuildReflection()
        {
            // A static studio environment supplies softbox reflections without per-frame probe renders.
            const int size=64;
            reflection=new Cubemap(size,TextureFormat.RGBAHalf,true) {name="StudioSoftboxes"}; owned.Add(reflection);
            for(int face=0;face<6;face++)
            {
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++) for(int x=0;x<size;x++)
                {
                    float u=(x+.5f)*2/size-1,v=(y+.5f)*2/size-1;
                    Vector3 d;
                    switch(face)
                    {
                        case 0:d=new Vector3(1,-v,-u);break;
                        case 1:d=new Vector3(-1,-v,u);break;
                        case 2:d=new Vector3(u,1,v);break;
                        case 3:d=new Vector3(u,-1,-v);break;
                        case 4:d=new Vector3(u,-v,1);break;
                        default:d=new Vector3(-u,-v,-1);break;
                    }
                    d.Normalize();
                    Color room=Color.Lerp(new Color(.18f,.20f,.22f),new Color(.72f,.77f,.81f),d.y*.5f+.5f);
                    float key=Panel(d,new Vector3(-.7f,.25f,-.7f).normalized,.12f,.85f);
                    float fill=Panel(d,new Vector3(.9f,.15f,.4f).normalized,.22f,.7f);
                    pixels[y*size+x]=room+new Color(1,.97f,.92f)*key*2.3f+new Color(.85f,.95f,1)*fill*1.5f;
                }
                reflection.SetPixels(pixels,(CubemapFace)face);
            }
            reflection.Apply(true,false);
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture=reflection; RenderSettings.reflectionIntensity=1.05f;
        }
        static float Panel(Vector3 direction,Vector3 normal,float width,float height)
        {
            float facing=Vector3.Dot(direction,normal); if(facing<=0) return 0;
            var right=Vector3.Cross(Vector3.up,normal).normalized;
            var up=Vector3.Cross(normal,right);
            float x=Mathf.Abs(Vector3.Dot(direction,right)/facing),y=Mathf.Abs(Vector3.Dot(direction,up)/facing);
            return (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(width*.85f,width,x)))*
                (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(height*.9f,height,y)));
        }
        void OnDestroy()
        {
            if(RenderSettings.customReflectionTexture==reflection) RenderSettings.customReflectionTexture=null;
            foreach(var obj in owned) if(obj!=null) Destroy(obj);
        }
    }
}
