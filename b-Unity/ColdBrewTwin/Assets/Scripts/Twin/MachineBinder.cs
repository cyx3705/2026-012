using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OneHistory.ColdBrewTwin
{
    public sealed class MachineBinder : MonoBehaviour
    {
        [Serializable] sealed class ProfileData { public Vector2[] profile = Array.Empty<Vector2>(); }
        [Serializable] sealed class ChannelData { public Vector3[] profile = Array.Empty<Vector3>(); }
        public AnnularChannelProfile Channel { get; private set; }
        readonly List<Material> materials = new List<Material>();
        readonly List<Material> shells = new List<Material>();
        readonly List<(Renderer renderer, Material glass, Material solid)> cupMaterials =
            new List<(Renderer, Material, Material)>();
        readonly List<GameObject> generated = new List<GameObject>();
        WaterBody lower, upper;
        Renderer upPump, downPump;
        Material pumpUpMaterial, pumpDownMaterial;
        public LiquidFlowAnimator Flow { get; private set; }
        Camera view;
        Canvas uiCanvas;
        Vector3 target = new Vector3(0, .2f, 0);
        float yaw = -32, pitch = 12, distance = .78f;
        bool section;
        public bool Section
        {
            get => section;
            set
            {
                if(section==value) return;
                section=value;
                foreach(var cup in cupMaterials) cup.renderer.sharedMaterial=value?cup.solid:cup.glass;
            }
        }
        public Rect InteractionRect { get; private set; }
        public bool ReducedMotion { get; set; }
        public float TransferCapacity { get; private set; } = .5f;
        public float LowerCapacity => lower.CapacityLitres;
        public float UpperCapacity => upper.CapacityLitres;
        public double LowerLevel(double litres) => lower.Cavity.LevelForVolume((float)litres);
        public double UpperLevel(double litres) => upper.Cavity.LevelForVolume((float)litres);
        public string GeometryStatus { get; private set; }
        public int SelectedView { get; private set; }
        public static readonly string[] ViewNames = { "整机", "上部液仓", "粉饼与流道", "中部泵组", "下部液仓" };

        public void Initialize()
        {
            lower = FindObjectOfType<WaterBody>();
            if (lower == null) throw new InvalidOperationException("Lower tank water is missing.");
            var data = Resources.Load<TextAsset>("UpperCavity");
            if (data == null) throw new InvalidOperationException("Measured upper cavity is missing.");
            var upperRoot = new GameObject("UpperTank_Measured");
            generated.Add(upperRoot);
            var cavity = upperRoot.AddComponent<TankCavity>();
            cavity.SetProfile(JsonUtility.FromJson<ProfileData>(data.text).profile);
            var water = new GameObject("UpperWater");
            water.transform.SetParent(upperRoot.transform, false);
            upper = water.AddComponent<WaterBody>();
            TransferCapacity = Mathf.Min(LowerCapacity, UpperCapacity);
            GeometryStatus = $"下仓 {LowerCapacity:F4} L  /  上仓 {UpperCapacity:F4} L";

            Material powderMaterial=null;
            foreach (var renderer in FindObjectsOfType<MeshRenderer>())
            {
                string n = renderer.name;
                if (n.StartsWith("6-1"))
                {
                    if (n.Contains("_1")) downPump = renderer; else upPump = renderer;
                    continue;
                }
                if (renderer == lower.GetComponent<Renderer>() || renderer == upper.GetComponent<Renderer>()) continue;
                bool powder = n.StartsWith("3-3");
                bool liner = n.StartsWith("2-3") || n.StartsWith("4-3");
                bool cup = n.StartsWith("2-1") || n.StartsWith("4-1");
                bool red = n.StartsWith("1-1") || n.StartsWith("5-1");
                bool black = n.StartsWith("1-2") || n.StartsWith("1-3") || n.StartsWith("5-2") || n.StartsWith("5-3") ||
                    n.StartsWith("3-6") || n.StartsWith("3-7") ||
                    n.StartsWith("2-4") || n.StartsWith("2-5") || n.StartsWith("4-5") || n.StartsWith("4-6");
                Material mat;
                if (powder) mat = MakeMaterial(new Color(.28f,.14f,.07f));
                else if (black)
                {
                    mat = new Material(Resources.Load<Shader>("TwinSection"));
                    mat.name = "SatinBlack_"+n; mat.color = new Color(.022f,.025f,.027f);
                    mat.SetFloat("_Metallic",0); mat.SetFloat("_Glossiness",n.StartsWith("3-6")?.22f:.35f);
                    materials.Add(mat); shells.Add(mat);
                }
                else if (n=="6-4")
                {
                    mat=MakeMaterial(new Color(.035f,.23f,.76f),.16f,.5f); mat.name="BatteryBlue";
                }
                else if (n.StartsWith("l-")) mat=MakeMaterial(new Color(.56f,.61f,.64f),.78f,.72f);
                else
                {
                    mat=new Material(Resources.Load<Shader>("TwinGlass")); mat.name=(red?"FrostedRed_":cup || liner?"SkyGlass_":"ClearGlass_")+n;
                    mat.color=red?new Color(.66f,.024f,.044f,.42f):cup?new Color(.54f,.80f,.97f,.09f):
                        liner?new Color(.60f,.83f,.96f,.018f):new Color(.79f,.91f,.92f,.035f);
                    mat.SetFloat("_EdgeOpacity",red?.32f:cup?.24f:liner?.12f:.15f);
                    mat.SetFloat("_Glossiness",red?.40f:cup?.91f:.96f);
                    mat.SetFloat("_Frost",red?1:cup?.12f:0);
                    // Inner liners are drawn before their surrounding glass walls.
                    mat.renderQueue=liner?3020:n.StartsWith("2-1") || n.StartsWith("4-1")?3040:3030;
                    shells.Add(mat); materials.Add(mat);
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                renderer.sharedMaterial = mat;
                if(cup || liner)
                {
                    var solid=new Material(Resources.Load<Shader>("TwinSection"));
                    solid.name="OpaqueCutaway_"+n;
                    solid.color=cup?new Color(.58f,.61f,.63f):new Color(.78f,.80f,.81f);
                    solid.SetFloat("_Metallic",.08f); solid.SetFloat("_Glossiness",.55f);
                    materials.Add(solid); shells.Add(solid);
                    cupMaterials.Add((renderer,mat,solid));
                    if(Section) renderer.sharedMaterial=solid;
                }
                if(powder) powderMaterial=mat;
            }
            if (upPump == null || downPump == null) throw new InvalidOperationException("CAD pump mapping is missing.");
            pumpUpMaterial = MakeMaterial(new Color(.95f,.64f,.13f), .3f, .55f);
            pumpDownMaterial = MakeMaterial(new Color(.12f,.49f,.83f), .3f, .55f);
            upPump.sharedMaterial = pumpUpMaterial;
            downPump.sharedMaterial = pumpDownMaterial;

            var flowRoot=new GameObject("LiquidFlow"); generated.Add(flowRoot);
            var channelPoints=JsonUtility.FromJson<ChannelData>(Resources.Load<TextAsset>("AnnularChannel").text).profile;
            var slices=new AnnularChannelProfile.Slice[channelPoints.Length];
            for(int i=0;i<slices.Length;i++) slices[i]=new AnnularChannelProfile.Slice(channelPoints[i].x,channelPoints[i].y,channelPoints[i].z);
            Channel=new AnnularChannelProfile(slices);
            Flow=flowRoot.AddComponent<LiquidFlowAnimator>(); Flow.Initialize(lower,upper,powderMaterial,Channel);

            view = Camera.main;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.83f,.86f,.87f);
            view.nearClipPlane = .005f;
            view.farClipPlane = 20;
            view.fieldOfView = 36;
            view.allowHDR=true; view.allowMSAA=true;
            gameObject.AddComponent<TwinStudio>().Initialize();
            SetView(0);
        }

        Material MakeMaterial(Color color, float metallic = 0, float smooth = .4f)
        {
            var mat = new Material(Shader.Find("Standard")); mat.color = color;
            mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Glossiness", smooth);
            materials.Add(mat); return mat;
        }
        public void Apply(TwinRuntimeState state, double time)
        {
            lower.VolumeLitres = state.LowerLitres;
            upper.VolumeLitres = state.UpperLitres;
            bool up = state.Gpio15;
            Flow.ReducedMotion=ReducedMotion;
            Flow.Apply(state,time);
            pumpUpMaterial.SetColor("_EmissionColor", up ? new Color(.7f,.27f,0) : Color.black);
            pumpDownMaterial.SetColor("_EmissionColor", state.Gpio16 ? new Color(0,.23f,.7f) : Color.black);
            pumpUpMaterial.EnableKeyword("_EMISSION"); pumpDownMaterial.EnableKeyword("_EMISSION");
        }
        public void SetView(int index)
        {
            SelectedView = Mathf.Clamp(index, 0, 4);
            float[] heights = { .2f, .311f, .225f, .179f, .085f };
            float[] distances = { .92f, .43f, .34f, .33f, .44f };
            target = new Vector3(0, heights[SelectedView], 0);
            distance = distances[SelectedView]; yaw = index == 3 ? 145 : -32; pitch = 12;
        }
        void LateUpdate()
        {
            if (view == null) return;
            if (uiCanvas == null) uiCanvas = FindObjectOfType<Canvas>();
            float scale = uiCanvas != null ? uiCanvas.scaleFactor : 1;
            float left = 296 * scale, right = 334 * scale, top = 60 * scale, bottom = 36 * scale;
            InteractionRect = new Rect(left, bottom, Mathf.Max(100, Screen.width-left-right), Mathf.Max(100,Screen.height-top-bottom));
            view.rect = new Rect(0,0,1,1);
            bool onView = InteractionRect.Contains(Input.mousePosition) && !(EventSystem.current?.IsPointerOverGameObject() ?? false);
            if (onView)
            {
                if (Input.GetMouseButton(0))
                {
                    yaw += Input.GetAxis("Mouse X") * 3;
                    pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2, -70, 75);
                }
                if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
                    target += (-view.transform.right * Input.GetAxis("Mouse X") - view.transform.up * Input.GetAxis("Mouse Y")) * distance * .012f;
                distance = Mathf.Clamp(distance * Mathf.Exp(-Input.mouseScrollDelta.y * .12f), .12f, 1.8f);
            }
            view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            view.transform.position = target - view.transform.forward * distance;
            Vector3 normal = -view.transform.forward; normal.y = 0;
            foreach (var material in shells)
            {
                material.SetFloat("_Section", Section ? 1 : 0);
                material.SetVector("_CutNormal", normal.normalized);
            }
            Flow.SetSection(Section,normal.normalized);
        }
        void OnDestroy()
        {
            foreach (var obj in generated) if (obj != null) Destroy(obj);
            foreach (var mat in materials) if (mat != null) Destroy(mat);
        }
    }
}
