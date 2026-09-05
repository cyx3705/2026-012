using UnityEngine;

namespace OneHistory.ColdBrewTwin
{
    /// <summary>
    /// 液仓里的水体。按 <see cref="TankCavity"/> 的实测轮廓，把当前水量重建成一块贴合内腔的
    /// 回转网格，并给出液位、充满度等孪生量。编辑模式下同样生效，改水量即可在 Scene 视图里看到。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("ColdBrewTwin/Water Body")]
    public class WaterBody : MonoBehaviour
    {
        [Tooltip("水体所在的液仓内腔。留空则向父级查找。")]
        [SerializeField] TankCavity cavity;

        [Tooltip("当前水量（升）。上限由内腔容积决定。")]
        [SerializeField, Min(0f)] float volumeLitres;

        [Tooltip("回转网格的周向分段数。")]
        [SerializeField, Range(16, 128)] int radialSegments = 64;

        Mesh mesh;
        bool dirty = true;
        float builtVolume = float.NaN;

        /// <summary>水体所在的液仓内腔。</summary>
        public TankCavity Cavity
        {
            get
            {
                if (cavity == null) cavity = GetComponentInParent<TankCavity>();
                return cavity;
            }
        }

        /// <summary>内腔容积（升）。</summary>
        public float CapacityLitres => Cavity != null ? Cavity.CapacityLitres : 0f;

        /// <summary>当前水量（升）。写入时按内腔容积截断。</summary>
        public float VolumeLitres
        {
            get => volumeLitres;
            set
            {
                float clamped = Mathf.Clamp(value, 0f, CapacityLitres);
                if (Mathf.Approximately(clamped, volumeLitres)) return;
                volumeLitres = clamped;
                dirty = true;
            }
        }

        /// <summary>充满度 0..1。</summary>
        public float FillRatio => CapacityLitres > 0f ? volumeLitres / CapacityLitres : 0f;

        /// <summary>液面高度（米，液仓局部空间）。</summary>
        public float SurfaceY => Cavity != null ? Cavity.LevelForVolume(volumeLitres) : 0f;

        /// <summary>液面高度（米，世界空间）。</summary>
        public float SurfaceWorldY =>
            Cavity != null ? Cavity.transform.TransformPoint(0f, SurfaceY, 0f).y : 0f;

        /// <summary>是否已经灌满。</summary>
        public bool IsFull => CapacityLitres - volumeLitres <= 1e-4f;

        void OnEnable()
        {
            dirty = true;
            Rebuild();
        }

        void OnDisable()
        {
            // 网格是运行时生成的临时资源，随组件一起回收，避免编辑模式下泄漏。
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }

        void OnValidate()
        {
            volumeLitres = Mathf.Clamp(volumeLitres, 0f, Mathf.Max(CapacityLitres, 0f));
            dirty = true;
        }

        void Update()
        {
            if (dirty || !Mathf.Approximately(builtVolume, volumeLitres)) Rebuild();
        }

        void Rebuild()
        {
            TankCavity c = Cavity;
            if (c == null) return;

            if (mesh == null)
            {
                mesh = new Mesh { name = "LowerTankWater", hideFlags = HideFlags.DontSave };
            }

            c.BuildLiquidMesh(mesh, c.LevelForVolume(volumeLitres), radialSegments);

            // 网格按液仓局部坐标生成。本组件通常就是液仓的同坐标子物体，
            // 但万一被挪走或缩放过，就把顶点搬回本地空间，免得水体错位。
            Matrix4x4 toSelf = transform.worldToLocalMatrix * c.transform.localToWorldMatrix;
            if (!toSelf.isIdentity)
            {
                Vector3[] v = mesh.vertices;
                Vector3[] n = mesh.normals;
                for (int i = 0; i < v.Length; i++)
                {
                    v[i] = toSelf.MultiplyPoint3x4(v[i]);
                    n[i] = toSelf.MultiplyVector(n[i]).normalized;
                }
                mesh.vertices = v;
                mesh.normals = n;
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
            }

            GetComponent<MeshFilter>().sharedMesh = mesh;

            builtVolume = volumeLitres;
            dirty = false;
        }
    }
}
