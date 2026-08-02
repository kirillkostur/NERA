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
        public const float MinimumForwardSpeed = 1f;
        public const float MaximumUpwardHeight = 0.05f;

        private float dis;
        public VaultSlide(ThirdPersonController _vaultingController, Action _actionInfo) : base(_vaultingController, _actionInfo)
        {
        }

        
        /// <summary>
        /// Checks if Player can Slide the Obstacle
        /// </summary>
        public override bool CheckAction()
        {
            if (controller.characterInput.drop &&
                !controller.isVaulting &&
                HasForwardMomentum(
                    controller.characterMovement.GetVelocity(),
                    controller.transform.forward))
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
                            controller.characterAnimation.animator.CrossFade("Running Slide", 0.05f);
                            controller.characterAnimation.animator.SetFloat("AnimSpeed", dis);
                            controller.characterAnimation.switchCameras?.SlideCam();
                            controller.SetSlidingCollider(true);
                            vaultTime = startDelay;
                            animLength = clip.length + startDelay;
                            controller.DisableController();

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool HasForwardMomentum(
            Vector3 velocity,
            Vector3 forward)
        {
            velocity.y = 0f;
            forward.y = 0f;
            if (forward.sqrMagnitude <= Mathf.Epsilon)
                return false;

            return Vector3.Dot(velocity, forward.normalized) >=
                   MinimumForwardSpeed;
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
            if (controller.isVaulting)
            {
                float actualSpeed = Time.deltaTime / animLength;
                vaultTime += actualSpeed * (animator.animState.speed + dis);

                if (vaultTime > 1)
                {
                    controller.characterAnimation.animator.SetFloat("AnimSpeed",1);
                    controller.characterAnimation.switchCameras?.FreeLookCam();
                    controller.SetSlidingCollider(false);
                    controller.EnableController();
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

        public override void DrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(targetPos, 0.08f);
        }
    }
}
