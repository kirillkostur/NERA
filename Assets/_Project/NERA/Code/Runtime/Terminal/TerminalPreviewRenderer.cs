using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NERA.Terminal
{
    /// <summary>
    /// Keeps a preview camera disabled and submits bounded, on-demand URP
    /// render requests into its authored RenderTexture.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TerminalPreviewRenderer : MonoBehaviour
    {
        private const float MinimumRenderInterval = 0.1f;

        private Camera previewCamera;
        private Coroutine renderRoutine;
        private bool previewActive;
        private bool unsupportedRequestLogged;
        private float lastRenderTime = float.NegativeInfinity;

        public bool IsRenderPending => renderRoutine != null;

        private void Awake()
        {
            Initialize(GetComponent<Camera>());
        }

        public void Initialize(Camera camera)
        {
            previewCamera = camera;
            if (previewCamera != null)
                previewCamera.enabled = false;
        }

        public void SetPreviewActive(bool active)
        {
            previewActive = active;
            if (previewCamera != null)
                previewCamera.enabled = false;

            if (!previewActive)
            {
                CancelPendingRender();
                return;
            }

            RequestRender();
        }

        public void RequestRender()
        {
            if (!previewActive ||
                !isActiveAndEnabled ||
                previewCamera == null ||
                previewCamera.targetTexture == null ||
                renderRoutine != null)
            {
                return;
            }

            renderRoutine = StartCoroutine(RenderAtEndOfFrame());
        }

        private IEnumerator RenderAtEndOfFrame()
        {
            float delay = lastRenderTime + MinimumRenderInterval -
                Time.unscaledTime;
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            yield return new WaitForEndOfFrame();
            renderRoutine = null;

            if (!previewActive ||
                previewCamera == null ||
                previewCamera.targetTexture == null)
            {
                yield break;
            }

            RenderTexture target = previewCamera.targetTexture;
            if (!target.IsCreated())
                target.Create();

            UniversalRenderPipeline.SingleCameraRequest request = new()
            {
                destination = target
            };
            if (!RenderPipeline.SupportsRenderRequest(previewCamera, request))
            {
                if (!unsupportedRequestLogged)
                {
                    unsupportedRequestLogged = true;
                    Debug.LogWarning(
                        $"The active render pipeline does not support " +
                        $"on-demand rendering for {previewCamera.name}.",
                        previewCamera);
                }
                yield break;
            }

            RenderPipeline.SubmitRenderRequest(previewCamera, request);
            lastRenderTime = Time.unscaledTime;
        }

        private void OnDisable()
        {
            previewActive = false;
            CancelPendingRender();
            if (previewCamera != null)
                previewCamera.enabled = false;
        }

        private void CancelPendingRender()
        {
            if (renderRoutine == null)
                return;

            StopCoroutine(renderRoutine);
            renderRoutine = null;
        }
    }
}
