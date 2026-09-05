using UnityEngine;

namespace OneHistory.ColdBrewTwin
{
    /// <summary>
    /// 下层液仓的水源：一个可开关的进水口，按设定流量往 <see cref="WaterBody"/> 里注水。
    /// 只负责"水从哪里来、以多快的速度来"，水量到液位的换算留在 <see cref="TankCavity"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ColdBrewTwin/Water Source")]
    public class WaterSource : MonoBehaviour
    {
        [Tooltip("注水目标。留空则向父级查找。")]
        [SerializeField] WaterBody target;

        [Tooltip("进水流量（升/分钟）。")]
        [SerializeField, Min(0f)] float flowLitresPerMinute = 1.2f;

        [Tooltip("注到这个水量（升）就自动关闭；填 -1 表示注满为止。")]
        [SerializeField] float stopAtLitres = -1f;

        [Tooltip("勾选后开始注水；进入播放模式即生效。")]
        [SerializeField] bool open;

        [Tooltip("场景开始时自动打开。")]
        [SerializeField] bool openOnStart = false;

        /// <summary>注水目标。</summary>
        public WaterBody Target
        {
            get
            {
                if (target == null) target = GetComponentInParent<WaterBody>();
                return target;
            }
        }

        /// <summary>进水流量（升/分钟）。</summary>
        public float FlowLitresPerMinute
        {
            get => flowLitresPerMinute;
            set => flowLitresPerMinute = Mathf.Max(0f, value);
        }

        /// <summary>当前是否在放水。</summary>
        public bool IsOpen => open;

        /// <summary>本次注水的目标水量（升）。</summary>
        public float StopAtLitres =>
            stopAtLitres < 0f ? (Target != null ? Target.CapacityLitres : 0f) : stopAtLitres;

        /// <summary>打开水源，注到 <see cref="StopAtLitres"/> 为止。</summary>
        public void Open() => open = true;

        /// <summary>关闭水源。</summary>
        public void Close() => open = false;

        /// <summary>打开水源并把目标水量设成指定值（升）。</summary>
        public void FillTo(float litres)
        {
            stopAtLitres = Mathf.Max(0f, litres);
            open = true;
        }

        /// <summary>打开水源，一直注满。</summary>
        public void FillToCapacity()
        {
            stopAtLitres = -1f;
            open = true;
        }

        void Start()
        {
            if (openOnStart) open = true;
        }

        void Update()
        {
            if (!open || !Application.isPlaying) return;

            WaterBody body = Target;
            if (body == null) return;

            float stop = Mathf.Min(StopAtLitres, body.CapacityLitres);
            if (body.VolumeLitres >= stop)
            {
                open = false;
                return;
            }

            float added = flowLitresPerMinute * Time.deltaTime / 60f;
            body.VolumeLitres = Mathf.Min(body.VolumeLitres + added, stop);

            if (body.VolumeLitres >= stop) open = false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = open ? new Color(0.3f, 0.8f, 1f) : new Color(0.5f, 0.6f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.006f);

            WaterBody body = target != null ? target : GetComponentInParent<WaterBody>();
            if (body == null || body.Cavity == null) return;

            // 从进水口往下画到当前液面，方便在 Scene 视图里核对进水位置。
            Vector3 from = transform.position;
            Vector3 to = new Vector3(from.x, body.SurfaceWorldY, from.z);
            if (to.y < from.y) Gizmos.DrawLine(from, to);
        }
    }
}
