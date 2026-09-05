using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHistory.ColdBrewTwin
{
    /// <summary>
    /// 下层液仓内腔的回转轮廓，以及基于该轮廓的容积／液位换算。
    ///
    /// 轮廓来自 CAD 装配 b-Module-GE/0-咖啡冷萃机-除板子外.stp：沿轴线逐层向外发射射线，
    /// 取首个实体交点的半径。内腔完全轴对称（各方位半径离散度小于 0.1 mm），因此用
    /// (高度, 半径) 折线即可无损描述。
    ///
    /// 坐标系：组件局部空间，Y 为高度轴，原点与 CAD 原点重合，单位为米。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ColdBrewTwin/Tank Cavity")]
    public class TankCavity : MonoBehaviour
    {
        /// <summary>内腔实测轮廓：x = 高度（米），y = 半径（米），按高度升序。</summary>
        static readonly Vector2[] MeasuredProfile =
        {
            new Vector2(0.02250f, 0.05298f), // 仓底平面
            new Vector2(0.02300f, 0.05362f),
            new Vector2(0.02350f, 0.05567f),
            new Vector2(0.02450f, 0.05789f),
            new Vector2(0.02800f, 0.06144f),
            new Vector2(0.03500f, 0.06298f), // 底部圆角结束，进入直筒段
            new Vector2(0.12800f, 0.06298f), // 直筒段结束
            new Vector2(0.13400f, 0.05705f),
            new Vector2(0.13500f, 0.05698f),
            new Vector2(0.14100f, 0.05098f),
            new Vector2(0.15000f, 0.05098f),
            new Vector2(0.15050f, 0.05398f),
            new Vector2(0.15250f, 0.05398f), // 仓顶（2-4 / 2-5 垫圈密封面）
        };

        [Tooltip("留空则使用代码内的实测轮廓。只有需要试算其它腔体时才覆盖。")]
        [SerializeField]
        Vector2[] profileOverride = Array.Empty<Vector2>();

        Vector2[] Profile =>
            profileOverride != null && profileOverride.Length >= 2 ? profileOverride : MeasuredProfile;

        public void SetProfile(Vector2[] samples)
        {
            if (samples == null || samples.Length < 2) throw new ArgumentException("At least two cavity samples are required.");
            for (int i = 0; i < samples.Length; i++)
                if (float.IsNaN(samples[i].x) || float.IsInfinity(samples[i].x) ||
                    float.IsNaN(samples[i].y) || float.IsInfinity(samples[i].y) || samples[i].y <= 0 ||
                    i > 0 && samples[i].x <= samples[i - 1].x)
                    throw new ArgumentException("Invalid cavity profile.");
            profileOverride = (Vector2[])samples.Clone();
        }

        /// <summary>仓底高度（米，局部空间）。</summary>
        public float FloorY => Profile[0].x;

        /// <summary>仓顶高度（米，局部空间）。</summary>
        public float CeilingY => Profile[Profile.Length - 1].x;

        /// <summary>灌到仓顶时的容积（升）。</summary>
        public float CapacityLitres => VolumeBelow(CeilingY);

        /// <summary>指定高度处的内腔半径（米）。超出轮廓范围时取端点值。</summary>
        public float RadiusAt(float y)
        {
            Vector2[] p = Profile;
            if (y <= p[0].x) return p[0].y;
            if (y >= p[p.Length - 1].x) return p[p.Length - 1].y;

            for (int i = 1; i < p.Length; i++)
            {
                if (y > p[i].x) continue;
                float span = p[i].x - p[i - 1].x;
                float t = span > Mathf.Epsilon ? (y - p[i - 1].x) / span : 0f;
                return Mathf.Lerp(p[i - 1].y, p[i].y, t);
            }
            return p[p.Length - 1].y;
        }

        /// <summary>液面在指定高度时的容积（升）。逐段按圆台体积积分。</summary>
        public float VolumeBelow(float y)
        {
            Vector2[] p = Profile;
            float top = Mathf.Clamp(y, p[0].x, p[p.Length - 1].x);
            float cubicMetres = 0f;

            for (int i = 1; i < p.Length && p[i - 1].x < top; i++)
            {
                float y0 = p[i - 1].x;
                float y1 = Mathf.Min(p[i].x, top);
                float span = p[i].x - y0;
                float r0 = p[i - 1].y;
                float r1 = span > Mathf.Epsilon ? Mathf.Lerp(r0, p[i].y, (y1 - y0) / span) : p[i].y;
                cubicMetres += Mathf.PI * (y1 - y0) * (r0 * r0 + r0 * r1 + r1 * r1) / 3f;
            }
            return cubicMetres * 1000f;
        }

        /// <summary>给定容积（升）时的液面高度（米，局部空间）。</summary>
        public float LevelForVolume(float litres)
        {
            if (litres <= 0f) return FloorY;
            if (litres >= CapacityLitres) return CeilingY;

            // VolumeBelow 单调递增，二分求逆；40 次迭代的残差远小于 CAD 量测误差。
            float lo = FloorY, hi = CeilingY;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (VolumeBelow(mid) < litres) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        /// <summary>
        /// 把仓底到 levelY 之间的液体体积写进 mesh：仓底封盖 + 回转侧壁 + 液面圆盘。
        /// </summary>
        public void BuildLiquidMesh(Mesh mesh, float levelY, int radialSegments)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            mesh.Clear();

            int seg = Mathf.Clamp(radialSegments, 8, 256);
            float top = Mathf.Clamp(levelY, FloorY, CeilingY);
            if (top - FloorY < 1e-5f) return;

            // 液面以下的轮廓采样点，末尾补一个恰好落在液面的点。
            Vector2[] p = Profile;
            var rings = new List<Vector2>(p.Length + 1);
            foreach (Vector2 s in p)
            {
                if (s.x < top) rings.Add(s);
            }
            rings.Add(new Vector2(top, RadiusAt(top)));

            int ringCount = rings.Count;
            int perRing = seg + 1;                     // 首尾各留一列重复顶点，UV 才不会撕裂
            int wallVerts = ringCount * perRing;
            const int surfaceBands=12;
            int interiorStart=wallVerts+2*(seg+2);
            var vertices = new Vector3[interiorStart+(surfaceBands-1)*perRing];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];

            for (int r = 0; r < ringCount; r++)
            {
                Vector2 s = rings[r];
                // 侧壁法线跟随轮廓倾角，底部圆角段才不会出现硬边。
                int a0 = r == 0 ? 0 : r - 1;
                int a1 = r == 0 ? 1 : r;
                float dr = rings[a1].y - rings[a0].y;
                float dy = rings[a1].x - rings[a0].x;
                Vector2 n2 = new Vector2(dy, -dr).normalized;
                float v = Mathf.InverseLerp(FloorY, top, s.x);

                for (int i = 0; i < perRing; i++)
                {
                    float a = (float)i / seg * Mathf.PI * 2f;
                    float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                    int idx = r * perRing + i;
                    vertices[idx] = new Vector3(cos * s.y, s.x, sin * s.y);
                    normals[idx] = new Vector3(cos * n2.x, n2.y, sin * n2.x).normalized;
                    uvs[idx] = new Vector2((float)i / seg, v);
                }
            }

            int floorCentre = wallVerts;
            int surfaceCentre = wallVerts + seg + 2;
            WriteCap(vertices, normals, uvs, floorCentre, seg, FloorY, rings[0].y, Vector3.down);
            WriteCap(vertices, normals, uvs, surfaceCentre, seg, top, rings[ringCount - 1].y, Vector3.up);
            for(int band=1;band<surfaceBands;band++)
                for(int i=0;i<=seg;i++)
                {
                    int at=interiorStart+(band-1)*perRing+i;
                    float angle=i*Mathf.PI*2/seg, ratio=(float)band/surfaceBands;
                    vertices[at]=new Vector3(Mathf.Cos(angle)*RadiusAt(top)*ratio,top,Mathf.Sin(angle)*RadiusAt(top)*ratio);
                    normals[at]=Vector3.up;
                    uvs[at]=new Vector2(.5f+Mathf.Cos(angle)*ratio*.5f,.5f+Mathf.Sin(angle)*ratio*.5f);
                }

            var tris = new List<int>((ringCount - 1) * seg * 6 + seg * 6);
            for (int r = 0; r < ringCount - 1; r++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int a = r * perRing + i;
                    int b = a + 1;
                    int c = a + perRing;
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            for (int i = 0; i < seg; i++)
            {
                tris.Add(floorCentre); tris.Add(floorCentre + 1 + i); tris.Add(floorCentre + 2 + i);
                tris.Add(surfaceCentre); tris.Add(interiorStart+1+i); tris.Add(interiorStart+i);
            }
            for(int band=0;band<surfaceBands-1;band++)
                for(int i=0;i<seg;i++)
                {
                    int inner=interiorStart+band*perRing+i;
                    int outer=band==surfaceBands-2?surfaceCentre+1+i:inner+perRing;
                    tris.Add(inner); tris.Add(outer+1); tris.Add(outer);
                    tris.Add(inner); tris.Add(inner+1); tris.Add(outer+1);
            }

            mesh.indexFormat = vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        static void WriteCap(Vector3[] vertices, Vector3[] normals, Vector2[] uvs,
                             int centre, int seg, float y, float radius, Vector3 normal)
        {
            vertices[centre] = new Vector3(0f, y, 0f);
            normals[centre] = normal;
            uvs[centre] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                int idx = centre + 1 + i;
                vertices[idx] = new Vector3(cos * radius, y, sin * radius);
                normals[idx] = normal;
                uvs[idx] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.9f);

            Vector2[] p = Profile;
            for (int i = 0; i < p.Length; i++)
            {
                DrawRing(p[i].x, p[i].y);
                if (i > 0) DrawSpokes(p[i - 1], p[i]);
            }
        }

        static void DrawRing(float y, float radius)
        {
            const int Seg = 48;
            Vector3 prev = new Vector3(radius, y, 0f);
            for (int i = 1; i <= Seg; i++)
            {
                float a = (float)i / Seg * Mathf.PI * 2f;
                Vector3 next = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        static void DrawSpokes(Vector2 lower, Vector2 upper)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                Gizmos.DrawLine(new Vector3(cos * lower.y, lower.x, sin * lower.y),
                                new Vector3(cos * upper.y, upper.x, sin * upper.y));
            }
        }
    }
}
