using System.Collections.Generic;
using UnityEngine;

namespace NERA.Graphics
{
    /// <summary>
    /// Removes volumetric fog inside an oriented BoxCollider. The collider is
    /// used as an authoring shape; no trigger callbacks are required.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("NERA/Graphics/Fog Exclusion Volume")]
    public sealed class FogExclusionVolume : MonoBehaviour
    {
        public const int MaximumVolumeCount = 16;

        private static readonly int ExclusionCountId =
            Shader.PropertyToID("_FogExclusionCount");
        private static readonly int ExclusionWorldToLocalId =
            Shader.PropertyToID("_FogExclusionWorldToLocal");
        private static readonly int ExclusionParametersId =
            Shader.PropertyToID("_FogExclusionParameters");

        private static readonly List<FogExclusionVolume> RegisteredVolumes =
            new List<FogExclusionVolume>();
        private static readonly Matrix4x4[] WorldToLocalMatrices =
            new Matrix4x4[MaximumVolumeCount];
        private static readonly Vector4[] VolumeParameters =
            new Vector4[MaximumVolumeCount];

        [Tooltip("Box that defines the fog-free area.")]
        [SerializeField] private BoxCollider volumeCollider;
        [Tooltip(
            "Distance outside the box over which fog smoothly returns. " +
            "Set to 0 for a hard edge.")]
        [Min(0f)]
        [SerializeField] private float edgeFade = 0.5f;

        private static int lastUploadedFrame = -1;

        public BoxCollider VolumeCollider => volumeCollider;
        public float EdgeFade => edgeFade;

        private void Reset()
        {
            CacheCollider();
            if (volumeCollider != null)
                volumeCollider.isTrigger = true;
        }

        private void Awake()
        {
            CacheCollider();
        }

        private void OnEnable()
        {
            CacheCollider();
            if (!RegisteredVolumes.Contains(this))
                RegisteredVolumes.Add(this);

            UploadVolumes();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && lastUploadedFrame == Time.frameCount)
                return;

            UploadVolumes();
        }

        private void OnDisable()
        {
            RegisteredVolumes.Remove(this);
            UploadVolumes();
        }

        private void OnValidate()
        {
            edgeFade = Mathf.Max(0f, edgeFade);
            CacheCollider();
            if (isActiveAndEnabled)
                UploadVolumes();
        }

        private void OnDrawGizmosSelected()
        {
            CacheCollider();
            if (volumeCollider == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.12f);
            Gizmos.DrawCube(volumeCollider.center, volumeCollider.size);
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube(volumeCollider.center, volumeCollider.size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void CacheCollider()
        {
            if (volumeCollider == null)
                volumeCollider = GetComponent<BoxCollider>();
        }

        private static void UploadVolumes()
        {
            RemoveDestroyedVolumes();

            int uploadedCount = 0;
            foreach (FogExclusionVolume volume in RegisteredVolumes)
            {
                if (uploadedCount >= MaximumVolumeCount)
                    break;
                if (volume == null ||
                    !volume.isActiveAndEnabled ||
                    volume.volumeCollider == null)
                {
                    continue;
                }

                BoxCollider box = volume.volumeCollider;
                Vector3 safeSize = new Vector3(
                    Mathf.Max(Mathf.Abs(box.size.x), 0.0001f),
                    Mathf.Max(Mathf.Abs(box.size.y), 0.0001f),
                    Mathf.Max(Mathf.Abs(box.size.z), 0.0001f));
                Matrix4x4 boxLocalToWorld =
                    volume.transform.localToWorldMatrix *
                    Matrix4x4.TRS(
                        box.center,
                        Quaternion.identity,
                        safeSize);
                Vector3 worldSize = new Vector3(
                    boxLocalToWorld.GetColumn(0).magnitude,
                    boxLocalToWorld.GetColumn(1).magnitude,
                    boxLocalToWorld.GetColumn(2).magnitude);

                if (worldSize.x <= Mathf.Epsilon ||
                    worldSize.y <= Mathf.Epsilon ||
                    worldSize.z <= Mathf.Epsilon)
                {
                    continue;
                }

                WorldToLocalMatrices[uploadedCount] =
                    boxLocalToWorld.inverse;
                VolumeParameters[uploadedCount] = new Vector4(
                    worldSize.x,
                    worldSize.y,
                    worldSize.z,
                    volume.edgeFade);
                uploadedCount++;
            }

            Shader.SetGlobalInt(ExclusionCountId, uploadedCount);
            if (uploadedCount > 0)
            {
                Shader.SetGlobalMatrixArray(
                    ExclusionWorldToLocalId,
                    WorldToLocalMatrices);
                Shader.SetGlobalVectorArray(
                    ExclusionParametersId,
                    VolumeParameters);
            }

            lastUploadedFrame = Time.frameCount;
        }

        private static void RemoveDestroyedVolumes()
        {
            for (int index = RegisteredVolumes.Count - 1;
                 index >= 0;
                 index--)
            {
                if (RegisteredVolumes[index] == null)
                    RegisteredVolumes.RemoveAt(index);
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RegisteredVolumes.Clear();
            lastUploadedFrame = -1;
            Shader.SetGlobalInt(ExclusionCountId, 0);
        }
    }
}
