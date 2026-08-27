using System;
using UnityEngine;

namespace NERA.Graphics
{
    [DisallowMultipleComponent]
    public sealed class ParticleEffectController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particleSystems =
            Array.Empty<ParticleSystem>();

        private bool playingRequested;

        public bool IsPlayingRequested => playingRequested;

        public void SetPlaying(bool playing)
        {
            if (playing)
                Play();
            else
                StopImmediate();
        }

        public void Play()
        {
            playingRequested = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                return;
            }

            if (isActiveAndEnabled)
                RestartParticles();
        }

        public void StopSmooth()
        {
            playingRequested = false;
            if (isActiveAndEnabled)
            {
                StopParticles(
                    ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void StopImmediate()
        {
            playingRequested = false;
            if (isActiveAndEnabled)
            {
                StopParticles(
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Awake()
        {
            CacheParticleSystems();
            DisablePlayOnAwake();
            if (!playingRequested)
            {
                StopParticles(
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            CacheParticleSystems();
            if (playingRequested)
                RestartParticles();
            else
                StopParticles(
                    ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            StopParticles(
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void RestartParticles()
        {
            StopParticles(
                ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem != null)
                    particleSystem.Play(false);
            }
        }

        private void StopParticles(ParticleSystemStopBehavior behavior)
        {
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem != null)
                    particleSystem.Stop(false, behavior);
            }
        }

        private void CacheParticleSystems()
        {
            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems =
                    GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        private void DisablePlayOnAwake()
        {
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                if (main.playOnAwake)
                    main.playOnAwake = false;
            }
        }

        private void OnValidate()
        {
            CacheParticleSystems();
            DisablePlayOnAwake();
        }
    }
}
