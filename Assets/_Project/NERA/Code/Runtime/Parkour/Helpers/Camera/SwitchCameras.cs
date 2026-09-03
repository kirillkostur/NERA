/*
MIT License

Copyright (c) 2023 Èric Canela
Contact: knela96@gmail.com or @knela96 twitter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (Dynamic Parkour System), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Climbing
{
    public class SwitchCameras : MonoBehaviour
    {
        enum CameraType
        {
            None,
            Freelook,
            Slide,
            Inventory
        }

        CameraType curCam = CameraType.None;

        [SerializeField] private CinemachineVirtualCameraBase FreeLook;
        [SerializeField] private CinemachineVirtualCameraBase Slide;
        [SerializeField] private CinemachineVirtualCameraBase Inventory;

        private CinemachineBrain brain;
        private Coroutine cameraCompletionRoutine;
        private System.Action pendingCameraCompletion;

        public bool IsFreeLookActive => curCam == CameraType.Freelook;
        public bool IsInventoryActive => curCam == CameraType.Inventory;
        public CinemachineVirtualCameraBase FreeLookCamera => FreeLook;
        public CinemachineVirtualCameraBase InventoryCamera => Inventory;

        private void Start()
        {
            ResolveReferences();
            FreeLookCam();
        }

        public void FreeLookCam()
        {
            FreeLookCam(null);
        }

        public void FreeLookCam(System.Action onTransitionComplete)
        {
            ResolveReferences();
            if (FreeLook == null)
            {
                onTransitionComplete?.Invoke();
                return;
            }

            if (curCam == CameraType.Freelook)
            {
                BeginCameraCompletion(FreeLook, onTransitionComplete);
                return;
            }

            if (curCam == CameraType.Inventory)
                InheritInventoryCameraPosition();

            SetPriorities(CameraType.Freelook);
            curCam = CameraType.Freelook;
            BeginCameraCompletion(FreeLook, onTransitionComplete);
        }

        public void SlideCam()
        {
            ResolveReferences();
            if (curCam == CameraType.Slide || Slide == null)
                return;

            SetPriorities(CameraType.Slide);
            curCam = CameraType.Slide;
        }

        public void InventoryCam()
        {
            ResolveReferences();
            if (curCam == CameraType.Inventory || Inventory == null)
                return;

            CancelCameraCompletion();
            ConfigureInventoryBlendHint();
            Inventory.PreviousStateIsValid = false;
            SetPriorities(CameraType.Inventory);
            curCam = CameraType.Inventory;
        }

        private void ConfigureInventoryBlendHint()
        {
            if (Inventory is CinemachineCamera camera)
            {
                camera.BlendHint |=
                    CinemachineCore.BlendHints.SphericalPosition;
            }
            else if (Inventory is CinemachineVirtualCamera virtualCamera)
            {
                virtualCamera.BlendHint |=
                    CinemachineCore.BlendHints.SphericalPosition;
            }
        }

        private void BeginCameraCompletion(
            CinemachineVirtualCameraBase target,
            System.Action completion)
        {
            if (completion == null)
                return;

            CancelCameraCompletion();
            pendingCameraCompletion = completion;
            if (!Application.isPlaying || brain == null || !isActiveAndEnabled)
            {
                CompleteCameraTransition();
                return;
            }

            cameraCompletionRoutine = StartCoroutine(
                WaitForCameraTransition(target));
        }

        private IEnumerator WaitForCameraTransition(
            CinemachineVirtualCameraBase target)
        {
            yield return null;
            while (brain != null &&
                   (brain.IsBlending || !brain.IsLiveChild(target, true)))
            {
                yield return null;
            }

            CompleteCameraTransition();
        }

        private void CancelCameraCompletion()
        {
            if (cameraCompletionRoutine != null)
                StopCoroutine(cameraCompletionRoutine);

            cameraCompletionRoutine = null;
            pendingCameraCompletion = null;
        }

        private void CompleteCameraTransition()
        {
            System.Action completion = pendingCameraCompletion;
            cameraCompletionRoutine = null;
            pendingCameraCompletion = null;
            completion?.Invoke();
        }

        private void SetPriorities(CameraType activeCamera)
        {
            if (FreeLook != null)
                FreeLook.Priority =
                    activeCamera == CameraType.Freelook ? 1 : 0;
            if (Slide != null)
                Slide.Priority =
                    activeCamera == CameraType.Slide ? 1 : 0;
            if (Inventory != null)
                Inventory.Priority =
                    activeCamera == CameraType.Inventory ? 1 : 0;
        }

        private void InheritInventoryCameraPosition()
        {
            if (FreeLook == null ||
                Inventory == null ||
                !Inventory.PreviousStateIsValid)
            {
                return;
            }

            CameraState inventoryState = Inventory.State;
            FreeLook.ForceCameraPosition(
                inventoryState.GetFinalPosition(),
                inventoryState.GetFinalOrientation());
        }

        private void ResolveReferences()
        {
            brain ??= GetComponent<CinemachineBrain>();
            if (FreeLook != null && Slide != null && Inventory != null)
                return;

            CinemachineVirtualCameraBase[] cameras = transform.root
                .GetComponentsInChildren<CinemachineVirtualCameraBase>(true);
            foreach (CinemachineVirtualCameraBase camera in cameras)
            {
                switch (camera.gameObject.name)
                {
                    case "FreeLookCam":
                        FreeLook ??= camera;
                        break;
                    case "SlideCam":
                        Slide ??= camera;
                        break;
                    case "InventoryCamera":
                        Inventory ??= camera;
                        break;
                }
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (pendingCameraCompletion != null)
                CompleteCameraTransition();
        }
    }
}
