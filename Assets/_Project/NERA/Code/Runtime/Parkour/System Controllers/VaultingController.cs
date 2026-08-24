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
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Climbing
{
    [Flags]
    public enum VaultActions
    {
        Nothing = 0,
        Vault_Obstacle = 1 << 0,
        Vault_Over = 1 << 1,
        Slide = 1 << 2,
        Reach = 1 << 3,
        Climb_Ledge = 1 << 4,
        Jump_Prediction = 1 << 5,
        Vault_Down = 1 << 6,
    }

    public class VaultingController : MonoBehaviour
    {
        public bool debug;
        public VaultActions vaultActions;

        [HideInInspector] public ThirdPersonController controller;
        [HideInInspector] public Animator animator;

        private List<VaultAction> actions = new List<VaultAction>();
        private VaultAction curAction;
        private VaultClimbLedge climbLedgeAction;
        private VaultJumpPrediction jumpPredictionAction;
        private const float AirborneGrabScanInterval = 0.04f;
        private float nextAirborneGrabScanAt;

        public void Start()
        {
            controller = GetComponent<ThirdPersonController>();
            animator = GetComponent<Animator>();

            //Loads all Valt Actions Values
            if(vaultActions.HasFlag(VaultActions.Vault_Obstacle))
            {
                AddConfiguredAction(
                    "Parkour/ActionsConfig/VaultObstacle",
                    action => new VaultObstacle(controller, action));
            }
            if (vaultActions.HasFlag(VaultActions.Vault_Over))
            {
                AddConfiguredAction(
                    "Parkour/ActionsConfig/VaultOver",
                    action => new VaultOver(controller, action));
            }
            if (vaultActions.HasFlag(VaultActions.Slide))
            {
                AddConfiguredAction(
                    "Parkour/ActionsConfig/VaultSlide",
                    action => new VaultSlide(controller, action));
            }
            if (vaultActions.HasFlag(VaultActions.Reach))
            {
                AddConfiguredAction(
                    "Parkour/ActionsConfig/VaultReach",
                    action => new VaultReach(controller, action));
            }
            if (vaultActions.HasFlag(VaultActions.Climb_Ledge))
            {
                climbLedgeAction = new VaultClimbLedge(controller);
                Add(climbLedgeAction);
            }
            if (vaultActions.HasFlag(VaultActions.Jump_Prediction))
            {
                jumpPredictionAction = new VaultJumpPrediction(controller);
                Add(jumpPredictionAction);
            }
            if (vaultActions.HasFlag(VaultActions.Vault_Down))
            {
                Add(new VaultDown(controller));
            }
        }

        void Update()
        {
            if (controller.isJumping && !controller.isGrounded &&
                Time.time >= nextAirborneGrabScanAt)
            {
                nextAirborneGrabScanAt =
                    Time.time + AirborneGrabScanInterval;
                TryInterruptJumpWithLedgeGrab();
            }

            if (!controller.isVaulting)
            {
                curAction = null;
            }

            //Check if vaulting action can be performed
            foreach (var item in actions)
            {
                if (item.CheckAction())
                {
                    curAction = item;
                    controller.isVaulting = true;
                    break;
                }
            }

            //Update logic of current vaulting Action
            if (curAction != null && controller.isVaulting)
            {
                if (!curAction.Update())
                    controller.isVaulting = false;

            }
        }

        private void FixedUpdate()
        {
            //Fixed Update logic of current vaulting Action
            if (curAction != null && controller.isVaulting)
            {
                if(!curAction.FixedUpdate())
                    controller.isVaulting = false;

            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (curAction != null)
            {
                curAction.OnAnimatorIK(layerIndex);
            }
        }

        private void OnDrawGizmos()
        {
            if (curAction != null && debug)
            {
                curAction.DrawGizmos();
            }
        }

        private void Add(VaultAction action)
        {
            if (action != null)
                actions.Add(action);
        }

        public void ResetForTeleport()
        {
            curAction = null;
            if (controller == null)
                controller = GetComponent<ThirdPersonController>();

            animator ??= GetComponent<Animator>();
            if (animator != null)
                animator.SetFloat("AnimSpeed", 1f);

            if (controller != null)
            {
                controller.isVaulting = false;
                controller.SetSlidingCollider(false);
                controller.characterAnimation?.switchCameras?.FreeLookCam();
            }
        }

        private void TryInterruptJumpWithLedgeGrab()
        {
            if (climbLedgeAction == null ||
                !climbLedgeAction.TryAirborneGrab())
            {
                return;
            }

            jumpPredictionAction?.CancelForLedgeGrab();
            curAction = climbLedgeAction;
            controller.isVaulting = true;
        }

        private void AddConfiguredAction(
            string resourcePath,
            Func<Action, VaultAction> factory)
        {
            Action actionInfo = Resources.Load<Action>(resourcePath);
            if (actionInfo == null)
            {
                Debug.LogError(
                    $"Parkour action config was not found at Resources/{resourcePath}.",
                    this);
                return;
            }

            Add(factory(actionInfo));
        }
    }

}
