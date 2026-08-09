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

namespace Climbing
{
    public class VaultSlide : VaultAction
    {
        public const float MaximumUpwardHeight = 0.05f;
        public const float MinimumSlopeAngle = 31f;
        public const float SlopeSlideSpeed = 6f;
        private const string SlideStateName = "Running Slide";
        private const float SlopeLoopStartNormalized = 12.4f / 46f;
        private const float SlopeLoopEndNormalized = 20f / 46f;

        private enum SlideMode
        {
            None,
            Obstacle,
            Slope,
        }

        private float dis;
        private SlideMode slideMode;
        private Vector3 slopeDirection;
        public VaultSlide(ThirdPersonController _vaultingController, Action _actionInfo) : base(_vaultingController, _actionInfo)
        {
        }


        /// <summary>
        /// Checks if Player can Slide the Obstacle
        /// </summary>
        public override bool CheckAction()
        {
            if (controller.isVaulting)
                return false;

            if (TryStartSlopeSlide())
                return true;

            if (controller.characterInput.drop)
            {
                RaycastHit hit;
                Vector3 origin = controller.transform.position + kneeRaycastOrigin;

                //Finds Obstacle
                if (controller.characterDetection.ThrowRayOnDirection(origin, controller.transform.forward, kneeRaycastLength, out hit))
                {
                    Vector3 origin2 = origin + (-hit.normal * (hit.transform.localScale.z + landOffset));

                    // If direction not the same as object don't do anything
                    // or angle of movement not valid
                    if ((hit.normal == hit.collider.transform.forward ||
                        hit.normal == -hit.collider.transform.forward) == false ||
                        Mathf.Abs(Vector3.Dot(-hit.normal, controller.transform.forward)) < 0.60 ||
                        !MatchesSurface(hit.collider))
                        return false;

                    RaycastHit hit2;
                    //Get ending position
                    if (controller.characterDetection.ThrowRayOnDirection(origin2, Vector3.down, 10, out hit2)) //Ground Hit
                    {
                        if (hit2.collider)
                        {
                            startPos = controller.transform.position;
                            if (!IsNonAscendingDestination(startPos, hit2.point))
                                return false;

                            startRot = controller.transform.rotation;
                            targetPos = hit2.point;
                            targetRot = Quaternion.LookRotation(targetPos - startPos);
                            float distance = Mathf.Max(
                                0.01f,
                                Vector3.Distance(startPos, targetPos));
                            dis = 4f / distance;
                            slideMode = SlideMode.Obstacle;
                            BeginSlide(dis);
                            vaultTime = startDelay;
                            animLength = clip.length + startDelay;

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TryStartSlopeSlide()
        {
            if (!controller.isGrounded ||
                !TryGetGroundHit(out RaycastHit groundHit) ||
                !IsSlideSlope(groundHit.normal) ||
                !TryGetDownhillDirection(
                    groundHit.normal,
                    out slopeDirection))
            {
                return false;
            }

            slideMode = SlideMode.Slope;
            targetRot = GetUprightSlopeRotation(
                slopeDirection,
                controller.transform.rotation);
            BeginSlide(1f);
            return true;
        }

        private void BeginSlide(float animationSpeed)
        {
            controller.characterAnimation.animator.CrossFade(
                SlideStateName,
                0.05f);
            controller.characterAnimation.animator.SetFloat(
                "AnimSpeed",
                animationSpeed);
            controller.characterAnimation.switchCameras?.SlideCam();
            controller.SetSlidingCollider(true);
            controller.DisableController();
        }

        private void EndSlide()
        {
            slideMode = SlideMode.None;
            slopeDirection = Vector3.zero;
            controller.characterAnimation.animator.SetFloat("AnimSpeed", 1f);
            controller.characterAnimation.switchCameras?.FreeLookCam();
            controller.SetSlidingCollider(false);
            controller.EnableController();
        }

        private bool TryGetGroundHit(out RaycastHit hit)
        {
            float distance = Mathf.Max(1.5f, controller.stepHeight + 0.8f);
            Vector3 origin = controller.transform.position + Vector3.up * 0.5f;
            return controller.characterDetection.ThrowRayOnDirection(
                origin,
                Vector3.down,
                distance,
                out hit);
        }

        public static bool IsSlideSlope(Vector3 normal)
        {
            if (normal.sqrMagnitude <= Mathf.Epsilon)
                return false;

            float angle = Vector3.Angle(normal, Vector3.up);
            return angle >= MinimumSlopeAngle && angle < 89f;
        }

        public static bool TryGetDownhillDirection(
            Vector3 normal,
            out Vector3 downhill)
        {
            downhill = Vector3.ProjectOnPlane(Vector3.down, normal);
            if (downhill.sqrMagnitude <= Mathf.Epsilon)
            {
                downhill = Vector3.zero;
                return false;
            }

            downhill.Normalize();
            return true;
        }

        private static Quaternion GetUprightSlopeRotation(
            Vector3 downhill,
            Quaternion fallback)
        {
            downhill.y = 0f;
            return downhill.sqrMagnitude > Mathf.Epsilon
                ? Quaternion.LookRotation(downhill.normalized, Vector3.up)
                : fallback;
        }

        public static bool IsNonAscendingDestination(
            Vector3 start,
            Vector3 destination)
        {
            return destination.y <= start.y + MaximumUpwardHeight;
        }

        /// <summary>
        /// Executes Vaulting Animation
        /// </summary>
        public override bool Update()
        {
            bool ret = false;
            if (!controller.isVaulting)
                return false;

            if (slideMode == SlideMode.Slope)
            {
                if (!TryGetGroundHit(out RaycastHit groundHit) ||
                    !IsSlideSlope(groundHit.normal) ||
                    !TryGetDownhillDirection(
                        groundHit.normal,
                        out slopeDirection))
                {
                    EndSlide();
                    return false;
                }

                targetRot = GetUprightSlopeRotation(
                    slopeDirection,
                    controller.transform.rotation);
                MaintainSlopeAnimation();
                controller.transform.rotation = Quaternion.Slerp(
                    controller.transform.rotation,
                    targetRot,
                    Time.deltaTime * 10f);
                return true;
            }

            if (slideMode == SlideMode.Obstacle)
            {
                float actualSpeed = Time.deltaTime / animLength;
                vaultTime += actualSpeed * (animator.animState.speed + dis);

                if (vaultTime > 1)
                {
                    EndSlide();
                }
                else
                {
                    controller.transform.rotation = Quaternion.Lerp(startRot, targetRot, vaultTime * 4);
                    controller.transform.position = Vector3.Lerp(startPos, targetPos, vaultTime);
                    ret = true;
                }
            }

            return ret;
        }

        private void MaintainSlopeAnimation()
        {
            Animator slideAnimator = controller.characterAnimation.animator;
            if (slideAnimator.IsInTransition(0) &&
                slideAnimator.GetNextAnimatorStateInfo(0)
                    .IsName(SlideStateName))
            {
                return;
            }

            AnimatorStateInfo state =
                slideAnimator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(SlideStateName))
            {
                slideAnimator.CrossFade(SlideStateName, 0.05f);
                return;
            }

            if (state.normalizedTime >= SlopeLoopEndNormalized)
            {
                // The imported clip contains a dedicated looping crouched
                // section between frames 12.4 and 20. Keeping the state in
                // that window lets the same clip play its exit pose once the
                // character reaches level ground.
                slideAnimator.Play(
                    SlideStateName,
                    0,
                    SlopeLoopStartNormalized);
            }
        }

        public override bool FixedUpdate()
        {
            if (!controller.isVaulting || slideMode != SlideMode.Slope)
                return slideMode != SlideMode.None;

            Rigidbody body = controller.characterMovement.rb;
            Vector3 movement =
                slopeDirection * SlopeSlideSpeed * Time.fixedDeltaTime;
            if (body != null)
            {
                body.MovePosition(body.position + movement);
                body.MoveRotation(controller.transform.rotation);
            }
            else
            {
                controller.transform.position += movement;
            }

            return true;
        }

        public override void DrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(targetPos, 0.08f);
        }
    }
}
