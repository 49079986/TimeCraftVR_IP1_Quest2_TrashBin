using UnityEngine;

namespace TimeCraft.IP1
{
    public sealed class TimeCraftTimelineScrubber : MonoBehaviour
    {
        [SerializeField] private Transform fill;
        [SerializeField] private Transform handle;
        [SerializeField] private float width = 5f;

        public void Initialize(Transform fillTransform, Transform handleTransform, float trackWidth)
        {
            fill = fillTransform;
            handle = handleTransform;
            width = trackWidth;
            SetNormalized(0f);
        }

        public void SetNormalized(float value)
        {
            value = Mathf.Clamp01(value);
            if (fill != null)
            {
                var length = Mathf.Max(0.02f, width * value);
                fill.localScale = new Vector3(length, 0.07f, 0.13f);
                fill.localPosition = new Vector3(-width * 0.5f + length * 0.5f, 0.035f, 0f);
            }
            if (handle != null)
            {
                handle.localPosition = new Vector3(Mathf.Lerp(-width * 0.5f, width * 0.5f, value), 0.13f, 0f);
            }
        }
    }
}
