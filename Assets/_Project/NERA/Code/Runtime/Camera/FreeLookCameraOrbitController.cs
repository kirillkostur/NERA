using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace NERA.CameraSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineFreeLook))]
    public sealed class FreeLookCameraOrbitController : MonoBehaviour
    {
        private sealed class ActiveZone
        {
            public UnityEngine.Object Source;
            public FreeLookCameraOrbitProfile Profile;
            public int Priority;
            public long ActivationOrder;
        }

        [SerializeField] private CinemachineFreeLook freeLook;

        private readonly List<ActiveZone> activeZones = new();
        private readonly FreeLookOrbitSettings[] defaultOrbits = new
            FreeLookOrbitSettings[3];
        private readonly FreeLookOrbitSettings[] transitionStart = new
            FreeLookOrbitSettings[3];
        private readonly FreeLookOrbitSettings[] transitionTarget = new
            FreeLookOrbitSettings[3];
        private readonly FreeLookOrbitSettings[] transitionCurrent = new
            FreeLookOrbitSettings[3];

        private bool defaultsCaptured;
        private bool isTransitioning;
        private long nextActivationOrder;
        private float transitionElapsed;
        private float transitionDuration;
        private UnityEngine.Object activeSource;

        public CinemachineFreeLook FreeLook => freeLook;
        public int ActiveZoneCount => activeZones.Count;
        public bool IsTransitioning => isTransitioning;

        private void Awake()
        {
            CacheCamera();
            CaptureDefaultOrbits();
        }

        private void Update()
        {
            AdvanceTransition(Time.deltaTime);
        }

        private void OnDisable()
        {
            activeZones.Clear();
            activeSource = null;
            isTransitioning = false;

            if (defaultsCaptured)
                ApplyOrbits(defaultOrbits);
        }

        public bool EnterZone(
            UnityEngine.Object source,
            FreeLookCameraOrbitProfile profile,
            int priority)
        {
            if (source == null || profile == null)
                return false;

            CacheCamera();
            if (freeLook == null)
                return false;

            RemoveDestroyedZones();
            if (activeZones.Count == 0 && !isTransitioning)
                CaptureDefaultOrbits();

            ActiveZone active = null;
            for (int i = 0; i < activeZones.Count; i++)
            {
                if (activeZones[i].Source == source)
                {
                    active = activeZones[i];
                    break;
                }
            }

            if (active == null)
            {
                active = new ActiveZone { Source = source };
                activeZones.Add(active);
            }

            active.Profile = profile;
            active.Priority = priority;
            active.ActivationOrder = ++nextActivationOrder;
            ApplyActiveProfile(profile.BlendInDuration, source);
            return true;
        }

        public void ExitZone(UnityEngine.Object source)
        {
            if (source == null)
                return;

            float blendOutDuration = 0f;
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                if (activeZones[i].Source == source)
                {
                    if (activeZones[i].Profile != null)
                    {
                        blendOutDuration = Mathf.Max(
                            blendOutDuration,
                            activeZones[i].Profile.BlendOutDuration);
                    }

                    activeZones.RemoveAt(i);
                }
            }

            ApplyActiveProfile(blendOutDuration);
        }

        public void AdvanceTransition(float deltaTime)
        {
            if (!isTransitioning)
                return;

            transitionElapsed += Mathf.Max(0f, deltaTime);
            float normalized = transitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(transitionElapsed / transitionDuration);
            float eased = normalized * normalized * (3f - 2f * normalized);

            for (int i = 0; i < transitionCurrent.Length; i++)
            {
                transitionCurrent[i] = new FreeLookOrbitSettings(
                    Mathf.LerpUnclamped(
                        transitionStart[i].Height,
                        transitionTarget[i].Height,
                        eased),
                    Mathf.LerpUnclamped(
                        transitionStart[i].Radius,
                        transitionTarget[i].Radius,
                        eased));
            }

            ApplyOrbits(transitionCurrent);
            if (normalized >= 1f)
                isTransitioning = false;
        }

        public void CaptureDefaultOrbits()
        {
            CacheCamera();
            if (freeLook == null)
                return;

            for (int i = 0; i < defaultOrbits.Length; i++)
                defaultOrbits[i] = ReadOrbit(i);

            defaultsCaptured = true;
        }

        public FreeLookOrbitSettings GetCurrentOrbit(int index)
        {
            if (index < 0 || index > 2)
                throw new ArgumentOutOfRangeException(nameof(index));

            CacheCamera();
            return ReadOrbit(index);
        }

        private void ApplyActiveProfile(
            float duration,
            UnityEngine.Object changedSource = null)
        {
            RemoveDestroyedZones();

            ActiveZone selected = null;
            for (int i = 0; i < activeZones.Count; i++)
            {
                ActiveZone candidate = activeZones[i];
                if (candidate.Profile == null)
                    continue;

                if (selected == null ||
                    candidate.Priority > selected.Priority ||
                    candidate.Priority == selected.Priority &&
                    candidate.ActivationOrder > selected.ActivationOrder)
                {
                    selected = candidate;
                }
            }

            UnityEngine.Object selectedSource = selected?.Source;
            bool forceRefresh = selected != null &&
                selected.Source == changedSource;
            if (selectedSource == activeSource && !forceRefresh)
                return;

            activeSource = selectedSource;
            if (selected != null)
            {
                BeginTransition(selected.Profile, duration);
            }
            else if (defaultsCaptured)
            {
                BeginTransition(defaultOrbits, duration);
            }
        }

        private void BeginTransition(
            FreeLookCameraOrbitProfile profile,
            float duration)
        {
            for (int i = 0; i < transitionTarget.Length; i++)
            {
                transitionStart[i] = ReadOrbit(i);
                transitionTarget[i] = profile.GetOrbit(i);
            }

            StartTransition(duration);
        }

        private void BeginTransition(
            IReadOnlyList<FreeLookOrbitSettings> target,
            float duration)
        {
            for (int i = 0; i < transitionTarget.Length; i++)
            {
                transitionStart[i] = ReadOrbit(i);
                transitionTarget[i] = target[i];
            }

            StartTransition(duration);
        }

        private void StartTransition(float duration)
        {
            transitionElapsed = 0f;
            transitionDuration = Mathf.Max(0f, duration);
            isTransitioning = transitionDuration > 0f;

            if (!isTransitioning)
                ApplyOrbits(transitionTarget);
        }

        private void ApplyOrbits(IReadOnlyList<FreeLookOrbitSettings> settings)
        {
            CacheCamera();
            if (freeLook == null || settings == null || settings.Count < 3)
                return;

            CinemachineFreeLook.Orbit[] orbits = freeLook.m_Orbits;
            if (orbits == null || orbits.Length != 3)
                orbits = new CinemachineFreeLook.Orbit[3];

            for (int i = 0; i < orbits.Length; i++)
            {
                orbits[i].m_Height = settings[i].Height;
                orbits[i].m_Radius = settings[i].Radius;
            }

            freeLook.m_Orbits = orbits;
        }

        private FreeLookOrbitSettings ReadOrbit(int index)
        {
            if (freeLook == null ||
                freeLook.m_Orbits == null ||
                index >= freeLook.m_Orbits.Length)
            {
                return new FreeLookOrbitSettings(0f, 0.01f);
            }

            CinemachineFreeLook.Orbit orbit = freeLook.m_Orbits[index];
            return new FreeLookOrbitSettings(orbit.m_Height, orbit.m_Radius);
        }

        private void RemoveDestroyedZones()
        {
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                if (activeZones[i].Source == null ||
                    activeZones[i].Profile == null)
                {
                    activeZones.RemoveAt(i);
                }
            }
        }

        private void CacheCamera()
        {
            if (freeLook == null)
                freeLook = GetComponent<CinemachineFreeLook>();
        }

        private void OnValidate()
        {
            CacheCamera();
        }
    }
}
