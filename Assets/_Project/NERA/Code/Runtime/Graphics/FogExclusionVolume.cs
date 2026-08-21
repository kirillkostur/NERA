using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NERA.Graphics
{
    /// <summary>
    /// Removes volumetric fog inside one or more oriented BoxColliders. The
    /// colliders are used as authoring shapes; no trigger callbacks are required.
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

        [Tooltip(
            "Optional primary box kept for backwards compatibility. " +
            "All BoxCollider components on this object are collected automatically " +
            "and kept as triggers.")]
        [FormerlySerializedAs("volumeCollider")]
        [SerializeField] private BoxCollider primaryCollider;
        [Tooltip(
            "Also collect BoxCollider components from child objects. " +
            "Inactive children are cached but do not affect the fog until activated.")]
        [SerializeField] private bool includeChildColliders;
        [Tooltip(
            "Distance outside the box over which fog smoothly returns. " +
            "Set to 0 for a hard edge.")]
        [Min(0f)]
        [SerializeField] private float edgeFade = 0.5f;

        private readonly List<BoxCollider> volumeColliders =
            new List<BoxCollider>();
        private static int lastUploadedFrame = -1;

        public BoxCollider VolumeCollider => primaryCollider;
        public IReadOnlyList<BoxCollider> VolumeColliders => volumeColliders;
        public bool IncludeChildColliders => includeChildColliders;
        public float EdgeFade => edgeFade;

        private void Reset()
        {
            CacheColliders();
        }

        private void Awake()
        {
            CacheColliders();
        }

        private void OnEnable()
        {
            CacheColliders();
            if (!RegisteredVolumes.Contains(this))
                RegisteredVolumes.Add(this);

            UploadVolumes();
        }

        private void LateUpdate()
        {
            // Keep the editor preview in sync when colliders are added or removed.
            if (!Application.isPlaying)
                CacheColliders();

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
            CacheColliders();
            if (isActiveAndEnabled)
                UploadVolumes();
        }

        private void OnTransformChildrenChanged()
        {
            if (!includeChildColliders)
                return;

            CacheColliders();
            if (isActiveAndEnabled)
                UploadVolumes();
        }

        private void OnDrawGizmosSelected()
        {
            CacheColliders();
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            foreach (BoxCollider box in volumeColliders)
            {
                if (box == null)
                    continue;

                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.12f);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
                Gizmos.DrawWireCube(box.center, box.size);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void CacheColliders()
        {
            volumeColliders.Clear();
            if (includeChildColliders)
                GetComponentsInChildren(true, volumeColliders);
            else
                GetComponents(volumeColliders);

            if (primaryCollider == null && volumeColliders.Count > 0)
                primaryCollider = volumeColliders[0];
            else if (primaryCollider != null &&
                     !volumeColliders.Contains(primaryCollider))
                volumeColliders.Insert(0, primaryCollider);

            foreach (BoxCollider box in volumeColliders)
            {
                if (box != null && box.transform == transform)
                    box.isTrigger = true;
            }
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
                    !volume.isActiveAndEnabled)
                {
                    continue;
                }

                foreach (BoxCollider box in volume.volumeColliders)
                {
                    if (uploadedCount >= MaximumVolumeCount)
                        break;
                    if (box == null || !box.gameObject.activeInHierarchy)
                        continue;

                    Vector3 safeSize = new Vector3(
                        Mathf.Max(Mathf.Abs(box.size.x), 0.0001f),
                        Mathf.Max(Mathf.Abs(box.size.y), 0.0001f),
                        Mathf.Max(Mathf.Abs(box.size.z), 0.0001f));
                    Matrix4x4 boxLocalToWorld =
                        box.transform.localToWorldMatrix *
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
