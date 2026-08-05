using System;
using UnityEngine;

namespace NERA.CameraSystem
{
    [Serializable]
    public struct FreeLookOrbitSettings
    {
        [SerializeField] private float height;
        [SerializeField, Min(0.01f)] private float radius;

        public float Height => height;
        public float Radius => Mathf.Max(0.01f, radius);

        public FreeLookOrbitSettings(float height, float radius)
        {
            this.height = height;
            this.radius = Mathf.Max(0.01f, radius);
        }

        public FreeLookOrbitSettings Validated()
        {
            return new FreeLookOrbitSettings(height, radius);
        }
    }

    [CreateAssetMenu(
        fileName = "FreeLookCameraOrbitProfile",
        menuName = "NERA/Camera/Free Look Orbit Profile")]
    public sealed class FreeLookCameraOrbitProfile : ScriptableObject
    {
        [SerializeField] private FreeLookOrbitSettings topRig =
            new FreeLookOrbitSettings(5f, 1.5f);
        [SerializeField] private FreeLookOrbitSettings middleRig =
            new FreeLookOrbitSettings(3f, 6f);
        [SerializeField] private FreeLookOrbitSettings bottomRig =
            new FreeLookOrbitSettings(1f, 3.5f);

        [Header("Transition")]
        [SerializeField, Min(0f)] private float blendInDuration = 0.35f;
        [SerializeField, Min(0f)] private float blendOutDuration = 0.35f;

        public FreeLookOrbitSettings TopRig => topRig;
        public FreeLookOrbitSettings MiddleRig => middleRig;
        public FreeLookOrbitSettings BottomRig => bottomRig;
        public float BlendInDuration => Mathf.Max(0f, blendInDuration);
        public float BlendOutDuration => Mathf.Max(0f, blendOutDuration);

        public FreeLookOrbitSettings GetOrbit(int index)
        {
            return index switch
            {
                0 => topRig,
                1 => middleRig,
                2 => bottomRig,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        public void Configure(
            FreeLookOrbitSettings top,
            FreeLookOrbitSettings middle,
            FreeLookOrbitSettings bottom,
            float blendIn = 0.35f,
            float blendOut = 0.35f)
        {
            topRig = top.Validated();
            middleRig = middle.Validated();
            bottomRig = bottom.Validated();
            blendInDuration = Mathf.Max(0f, blendIn);
            blendOutDuration = Mathf.Max(0f, blendOut);
        }

        private void OnValidate()
        {
            topRig = topRig.Validated();
            middleRig = middleRig.Validated();
            bottomRig = bottomRig.Validated();
            blendInDuration = Mathf.Max(0f, blendInDuration);
            blendOutDuration = Mathf.Max(0f, blendOutDuration);
        }
    }
}
