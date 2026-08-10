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
    public class ClimbController : MonoBehaviour
    {
        public bool debug = false;
        public enum ClimbState { None, BHanging, FHanging };
        private ClimbState curClimbState = ClimbState.None;

        private bool active = false;
        private bool ledgeFound = false;
        private bool wallFound = false;
        private bool reachedEnd = false;
        private bool onLedge = false;
        private bool toLedge = false;
        private bool jumping = false;
        private bool leftHandIKFound = false;
        private bool rightHandIKFound = false;
        private bool leftFootIKFound = false;
        private bool rightFootIKFound = false;
        private bool leftHandIKInitialized = false;
        private bool rightHandIKInitialized = false;
        private bool leftFootIKInitialized = false;
        private bool rightFootIKInitialized = false;

        private float startTime = 0.0f;
        private float endTime = 0.0f;
        private float rotTime = 0.0f;
        private float horizontalMovement = 0.0f;
        private float wallContactTime = 0.0f;
        private float wallMissTime = 0.0f;
        private float smallHopMaxDistance = 0.35f; 
        private float distanceToLedgeBraced = 0.3f;
        private float distanceToLedgeFree = 0.1f;
        private const float MaxClimbTargetDistance = 4f;
        private const float RightEndpointGrabOffset = 0.5f;

        private ThirdPersonController characterController;
        private DetectionCharacterController characterDetection;
        private AnimationCharacterController characterAnimation;
        private GameObject curLedge;
        private HandlePoints releasedRegrabHandle;
        private Point targetPoint = null;
        private Point currentPoint = null;

        private Vector3 target = Vector3.zero;
        private Quaternion targetRot = Quaternion.identity;
        private Vector3 curOriginGrabOffset = Vector3.zero;
        private Vector3 HandPosition = Vector3.zero;
        private Vector3 leftHandPosition, rightHandPosition, leftFootPosition, rightFootPosition = Vector3.zero;
        private Vector3 smoothedLeftHandPosition = Vector3.zero;
        private Vector3 smoothedRightHandPosition = Vector3.zero;
        private Vector3 smoothedLeftFootPosition = Vector3.zero;
        private Vector3 smoothedRightFootPosition = Vector3.zero;

        [Header("Offset Positions")]
        [SerializeField] private Vector3 FreeHangOffset;
        [SerializeField] private Vector3 BracedHangOffset;

        [Header("Air Ledge Grab")]
        [Tooltip("First normalized frame used to pull an airborne player to the ledge.")]
        [Range(0f, 1f)]
        [SerializeField] private float airGrabMatchStart = 0.05f;
        [Tooltip("Last normalized frame used to pull an airborne player to the ledge.")]
        [Range(0f, 1f)]
        [SerializeField] private float airGrabMatchEnd = 0.72f;

        [Header("Ledge Movement")]
        [Tooltip("Keeps the centre of the hands away from the last Point so " +
                 "the hands do not move past the end of a short ledge.")]
        [Min(0f)]
        [SerializeField] private float ledgeEndpointPadding = 0.18f;

        [Header("IK Settings")]
        [SerializeField] private Vector3 originHandIKBracedOffset;
        [SerializeField] private Vector3 originHandIKFreeOffset;
        [SerializeField] private Vector3 originFootIKOffset;
        [SerializeField] private float IKHandRayLength = 0.5f;
        [SerializeField] private float IKFootRayLength = 0.5f;

        [Header("IK Smoothing")]
        [Tooltip("Smooths the single HandlePoints IK target without changing its weight or source.")]
        [Min(0.01f)]
        [SerializeField] private float ikPositionSmoothing = 18f;
        [Tooltip("Smooths root alignment while moving along a ledge.")]
        [Min(0.01f)]
        [SerializeField] private float ledgeRootAlignmentSmoothing = 20f;
        [Tooltip("Prevents one missed foot ray from switching hang type.")]
        [Min(0f)]
        [SerializeField] private float wallContactGraceTime = 0.10f;

        [Header("IK GameObjects")]
        [Tooltip("Auto Search the bones when not specified")]
        [SerializeField] private bool AutoSearchBones;
        [SerializeField] private GameObject LHand;
        [SerializeField] private GameObject RHand;
        [SerializeField] private GameObject LFoot;
        [SerializeField] private GameObject RFoot;

        [Header("Animation Curves")]
        public string LHandAnimVariableName = "LHandCurve";
        public string RHandAnimVariableName = "RHandCurve";
        public string LFootAnimVariableName = "LeftFootCurve";
        public string RFootAnimVariableName = "RightFootCurve";

        // Start is called before the first frame update
        void Start()
        {
            curLedge = null;
            characterController = GetComponent<ThirdPersonController>();
            characterAnimation = characterController.characterAnimation;
            characterDetection = characterController.characterDetection;

            if (LHand == null || RHand == null || LFoot == null || RFoot == null)
            {
                if (AutoSearchBones)
                {
                    Debug.LogWarning("In the Player ClimbController script is recommended to set the bones of Hands and Feet");

                    if (LHand == null)
                        LHand = characterAnimation.animator.GetBoneTransform(HumanBodyBones.LeftHand).gameObject;
                    if (RHand == null)
                        RHand = characterAnimation.animator.GetBoneTransform(HumanBodyBones.RightHand).gameObject;
                    if (LFoot == null)
                        LFoot = characterAnimation.animator.GetBoneTransform(HumanBodyBones.LeftFoot).gameObject;
                    if (RFoot == null)
                        RFoot = characterAnimation.animator.GetBoneTransform(HumanBodyBones.RightFoot).gameObject;
                }
                else
                {
                    Debug.LogError("In the Player check that the ClimbController script has the GameObjects of the Hands and Feet assigned");
                }
            }
        }

        public void OnDrawGizmos()
        {
            if (targetPoint != null && currentPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(targetPoint.transform.position, 0.1f);
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(currentPoint.transform.position, 0.1f);
            }
        }

        public void ResetForTeleport()
        {
            curClimbState = ClimbState.None;
            active = false;
            ledgeFound = false;
            wallFound = false;
            reachedEnd = false;
            SetOnLedge(false);
            toLedge = false;
            jumping = false;
            leftHandIKFound = false;
            rightHandIKFound = false;
            leftFootIKFound = false;
            rightFootIKFound = false;
            curLedge = null;
            targetPoint = null;
            currentPoint = null;
            target = Vector3.zero;
            releasedRegrabHandle = null;
            ResetIKSmoothing();

            if (characterAnimation != null &&
                characterAnimation.animator != null)
            {
                characterAnimation.animator.SetBool("Hanging", false);
                characterAnimation.animator.SetInteger(
                    "Climb State",
                    (int)ClimbState.None);
            }

            characterController ??= GetComponent<ThirdPersonController>();
            characterController?.cameraController?.ResetOffsetImmediate();
        }
        public void OnAnimatorIK(int layerIndex)
        {
            Animator animator = characterAnimation?.animator;
            if (animator == null)
                return;

            // Animator IK is written only here. Hand contacts themselves are
            // supplied by the single package HandlePoints/IK solver path.
            if (!onLedge)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
                return;
            }

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
            ApplySmoothedIKPosition(
                AvatarIKGoal.LeftHand,
                leftHandPosition,
                ref smoothedLeftHandPosition,
                ref leftHandIKInitialized);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            ApplySmoothedIKPosition(
                AvatarIKGoal.RightHand,
                rightHandPosition,
                ref smoothedRightHandPosition,
                ref rightHandIKInitialized);

            bool useFootIK =
                wallFound && curClimbState == ClimbState.BHanging;
            bool useLeftFootIK = useFootIK && leftFootIKFound;
            bool useRightFootIK = useFootIK && rightFootIKFound;
            animator.SetIKPositionWeight(
                AvatarIKGoal.LeftFoot,
                useLeftFootIK ? 1f : 0f);
            if (useLeftFootIK)
                ApplySmoothedIKPosition(
                    AvatarIKGoal.LeftFoot,
                    leftFootPosition,
                    ref smoothedLeftFootPosition,
                    ref leftFootIKInitialized);
            else
                leftFootIKInitialized = false;
            animator.SetIKPositionWeight(
                AvatarIKGoal.RightFoot,
                useRightFootIK ? 1f : 0f);
            if (useRightFootIK)
                ApplySmoothedIKPosition(
                    AvatarIKGoal.RightFoot,
                    rightFootPosition,
                    ref smoothedRightFootPosition,
                    ref rightFootIKInitialized);
            else
                rightFootIKInitialized = false;
        }

        /// <summary>
        /// Checks on Ground if the player can grab on a Ledge
        /// </summary>
        public bool ClimbCheck()
        {
            active = false;
            if (characterController.isGrounded)
                releasedRegrabHandle = null;

            if (!characterController.dummy && characterController.isGrounded)
            {
                SetOnLedge(false);
                RaycastHit hit;
                if (characterController.characterInput.jump && !toLedge && !onLedge)
                {
                    if (!TryStartLedgeGrab(false, out hit))
                    {
                        target = Vector3.zero;
                        targetRot = Quaternion.identity;
                    }
                }

                //If player wants to drop to a Ledge from a Top Surface
                if (characterController.characterInput.drop && characterController.isGrounded)
                {
                    //Throw Rays below Player
                    characterDetection.FindDropLedgeCollision(out hit);
                    if (hit.collider && TryReachLedge(hit, out target))
                    {
                        targetRot = Quaternion.LookRotation(-hit.normal);
                        transform.rotation = Quaternion.FromToRotation(
                            transform.forward,
                            hit.normal) * transform.rotation;

                        //Check if Ledge is a Braced or FreeHand Point
                        wallFound = characterDetection.FindFootCollision(
                            target,
                            targetRot,
                            hit.normal);

                        if (wallFound)
                        {
                            characterAnimation.DropToBraced((int)ClimbState.BHanging);
                            curClimbState = ClimbState.BHanging;
                        }
                        else
                        {
                            characterAnimation.DropToFree((int)ClimbState.FHanging);
                            curClimbState = ClimbState.FHanging;
                        }

                        startTime = 0.3f;
                        endTime = 0.45f;
                        active = true;
                        characterController.ToggleWalk();
                    }
                }
            }
            return active;
        }

        /// <summary>
        /// Lets the active jump action hand control over to climbing when a
        /// ledge is found in front of the player while airborne.
        /// </summary>
        public bool TryAirborneLedgeGrab()
        {
            if (characterController.isGrounded)
            {
                releasedRegrabHandle = null;
                return false;
            }

            if (!characterController.isJumping ||
                toLedge ||
                onLedge)
            {
                return false;
            }

            return TryStartLedgeGrab(true, out _);
        }

        private bool TryStartLedgeGrab(bool fromAir, out RaycastHit hit)
        {
            ledgeFound = fromAir
                ? characterDetection.FindAirborneLedgeCollision(out hit)
                : characterDetection.FindLedgeCollision(out hit);
            if (!ledgeFound)
                return false;

            HandlePoints candidateHandle =
                FindHandlePoints(hit.collider);
            if (ShouldBlockLedgeRegrab(
                    releasedRegrabHandle,
                    candidateHandle,
                    characterController.isGrounded))
            {
                ledgeFound = false;
                return false;
            }

            if (!TryReachLedge(hit, out target))
            {
                ledgeFound = false;
                return false;
            }

            if (candidateHandle != releasedRegrabHandle)
                releasedRegrabHandle = null;

            targetRot = Quaternion.LookRotation(-hit.normal);
            wallFound = characterDetection.FindFootCollision(
                target,
                targetRot,
                hit.normal);
            curClimbState = wallFound
                ? ClimbState.BHanging
                : ClimbState.FHanging;

            if (fromAir)
            {
                characterAnimation.HangLedgeFromAir(curClimbState);
                characterController.isJumping = false;
                characterController.onAir = false;
            }
            else
            {
                characterAnimation.HangLedge(curClimbState);
            }

            startTime = 0f;
            endTime = fromAir ? airGrabMatchEnd : 0.2f;
            active = true;
            characterController.ToggleWalk();
            return true;
        }

        /// <summary>
        /// Main climbing update that checks climbing movement and inTransition animations
        /// </summary>
        public bool ClimbUpdate()
        {
            if (!characterController.dummy && curLedge == null)
            {
                active = false;
            }

            if (onLedge && characterController.dummy)
            {
                //Movement on Ledge
                ClimbMovement(characterController.characterInput.movement); 

                //Dismount from Ledge
                if (characterController.characterInput.drop && characterController.characterInput.movement == Vector2.zero)
                {
                    ReleaseCurrentLedge(true);
                }
            }

            //Controls Climbing Transitions
            if (toLedge)
            {
                bool matchingTarget = false;
                bool matchRotation = true;

                //Idle To Ledge
                if (characterAnimation.animState.IsName("Idle To Braced Hang") ||
                    characterAnimation.animState.IsName("Idle To Freehang"))
                {
                    matchingTarget = true;
                    rotTime = 0;

                    if (wallFound) //Braced
                        characterAnimation.SetMatchTarget(AvatarTarget.LeftHand, target, targetRot, targetRot * BracedHangOffset, startTime, 0.56f);
                    else //Free
                        characterAnimation.SetMatchTarget(AvatarTarget.LeftHand, target, targetRot, targetRot * FreeHangOffset, startTime, 0.56f);
                }

                // Airborne jump to a braced ledge. The short Braced Hang clip
                // aligns the hands before its Animator transition continues
                // into Braced Hanging/Hanging Movement.
                if (characterAnimation.animState.IsName("Braced Hang"))
                {
                    matchingTarget = true;
                    rotTime = 0;

                    if (!characterAnimation.animator.IsInTransition(0))
                    {
                        characterAnimation.SetMatchTarget(
                            AvatarTarget.LeftHand,
                            target,
                            targetRot,
                            targetRot * BracedHangOffset,
                            airGrabMatchStart,
                            airGrabMatchEnd);
                    }
                }

                //Jump Ledge to Ledge 
                if (!characterAnimation.animState.IsName("Hanging Movement") && jumping == true)
                {
                    matchingTarget = true;
                    rotTime = 0;

                    characterAnimation.SetMatchTarget(AvatarTarget.LeftHand, target, targetRot, targetRot * BracedHangOffset, startTime, endTime);
                }

                //Climb 
                if (characterAnimation.animState.IsName("Braced Hang To Crouch") ||
                    characterAnimation.animState.IsName("Freehang Climb"))
                {
                    matchingTarget = true;
                    rotTime = 0;

                    if (curClimbState == ClimbState.BHanging)
                        characterAnimation.SetMatchTarget(AvatarTarget.LeftFoot, target, targetRot, Vector3.zero, startTime, endTime);// Braced
                    else
                        characterAnimation.SetMatchTarget(AvatarTarget.RightFoot, target, targetRot, Vector3.zero, startTime, endTime);

                    curClimbState = ClimbState.None;
                    characterAnimation.DropLedge((int)curClimbState);
                }

                //Dismount
                if (characterAnimation.animState.IsName("Drop To Freehang") ||
                    characterAnimation.animState.IsName("Drop To Bracedhang"))
                {
                    matchingTarget = true;
                    rotTime = 0;

                    if (wallFound)
                        characterAnimation.SetMatchTarget(AvatarTarget.LeftHand, target, targetRot, targetRot * -BracedHangOffset, startTime, endTime);
                    else
                        characterAnimation.SetMatchTarget(AvatarTarget.LeftHand, target, targetRot, Vector3.zero, startTime, endTime);
                }

                //Move Player and Rotate to Target Point
                if (matchingTarget)
                {
                    if (matchRotation)
                    {
                        if (characterAnimation.animState.normalizedTime >= startTime && rotTime <= 1.0f)
                        {
                            rotTime += Time.deltaTime / 0.15f;
                            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotTime);
                        }
                    }

                    //If MatchTarget animation ends, reset default values
                    if (characterAnimation.animator.IsInTransition(0)) 
                    {
                        SetOnLedge(true);
                        toLedge = false;
                        jumping = false;
                        leftHandPosition = characterAnimation.animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                        rightHandPosition = characterAnimation.animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                        leftFootPosition = characterAnimation.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                        rightFootPosition = characterAnimation.animator.GetBoneTransform(HumanBodyBones.RightFoot).position;

                        //Enable controller if climbing State Ends
                        if (curClimbState == ClimbState.None)
                        {
                            active = false;
                            SetOnLedge(false);
                        }
                    }
                }
            }
            return active;
        }

        public void ClimbMovement(Vector2 direction)
        {
            if (curClimbState == ClimbState.BHanging)
                curOriginGrabOffset = originHandIKBracedOffset;
            else if (curClimbState == ClimbState.FHanging)
                curOriginGrabOffset = originHandIKFreeOffset;

            //Detect change of input direction to allow movement again after reaching end of the ledge
            if (((direction.x >= 0 && horizontalMovement <= 0) || (direction.x <= 0 && horizontalMovement >= 0)) ||
                !characterAnimation.animState.IsName("Hanging Movement"))
            {
                reachedEnd = false;
            }

            if (!reachedEnd)//Stops Movement on Ledge
            {
                horizontalMovement = direction.x; //Stores player input direction

                if (!CheckValidMovement(direction.x))
                {
                    reachedEnd = true;
                }
            }

            //Stops Horizontal Movement if Reached End of Ledge
            if (reachedEnd)
                direction.x = 0;

            //Solver to position Limbs + Checks if need to change climb state from Braced and Free Hang
            IKSolver();

            //Change from Braced Hang <-----> Free Hang
            ChangeBracedFreeHang();

            bool wantsToDescend = WantsToDescend(direction);

            // Space moves up between ledges. Backward movement alone moves
            // down, including the diagonal S+A and S+D directions.
            if ((characterController.characterInput.jump || wantsToDescend) &&
                characterAnimation.animState.IsName("Hanging Movement"))
            {
                //Check if can climb on surface
                bool climbing = false;
                if (!wantsToDescend &&
                    characterController.characterInput.movement.y > 0.8f &&
                    characterController.characterInput.movement.x < 0.3 &&
                    characterController.characterInput.movement.x > -0.3 &&
                    onLedge)
                {
                    climbing = ClimbFromLedge();
                }

                // Do not update the hanging movement blend tree in the same
                // frame as another ledge action. Sampling its side pose before
                // the hop/ledge-climb crossfade causes a one-frame double pose.
                if (climbing)
                    return;

                bool hasConnectedLedge = TryFindLedgeNeighbour(
                    characterController.characterInput.movement.x,
                    characterController.characterInput.movement.y,
                    wantsToDescend,
                    out Neighbour neighbour,
                    out float xDistance);

                if (hasConnectedLedge && wallFound)
                {
                    JumpToLedge(neighbour, xDistance);
                    return;
                }
            }

            characterAnimation.HangMovement(direction.x, (int)curClimbState); //Move on Ledge Animations
        }

        public static bool WantsToDescend(Vector2 direction)
        {
            return direction.y < -0.5f;
        }

        /// <summary>
        /// Climbs From Ledge to Upwards Surface
        /// </summary>
        bool ClimbFromLedge()
        {
            if (!TryGetStableGrabPosition(out Vector3 origin))
                return false;

            //Checks if the player fits on the top surface to climb
            RaycastHit hit;
            if (characterController.characterDetection.ThrowClimbRay(
                    origin,
                    transform.forward,
                    IKHandRayLength,
                    out hit) &&
                IsLocalClimbTarget(transform.position, hit.point))
            {
                if (curClimbState == ClimbState.BHanging)
                {
                    characterAnimation.BracedClimb();
                    startTime = 0.70f;
                    endTime = 1.0f;
                }
                else
                {
                    characterAnimation.FreeClimb();
                    startTime = 0.80f;
                    endTime = 1.0f;
                }

                target = hit.point;
                targetRot = transform.rotation;
                toLedge = true;
                SetOnLedge(false);
                characterController.cameraController.newOffset(false);
                return true;
            }

            return false;
        }

        /// <summary>
        /// A climb target must remain close to the player. This prevents a
        /// failed hand ray from turning Vector3.zero into a valid world-space
        /// MatchTarget destination.
        /// </summary>
        public static bool IsLocalClimbTarget(
            Vector3 playerPosition,
            Vector3 targetPosition)
        {
            return IsFinite(playerPosition) &&
                   IsFinite(targetPosition) &&
                   (targetPosition - playerPosition).sqrMagnitude <=
                   MaxClimbTargetDistance * MaxClimbTargetDistance;
        }

        /// <summary>
        /// Checks available points to jump Ledge to Ledge dependng on the input direction
        /// </summary>
        private bool TryFindLedgeNeighbour(
            float horizontal,
            float vertical,
            bool drop,
            out Neighbour neighbour,
            out float xDistance)
        {
            neighbour = null;
            xDistance = 0f;
            if (vertical == 0 && horizontal == 0)
                return false;

            HandlePoints handle = GetCurrentHandlePoints();
            if (handle == null)
                return false;

            Point point;
            if (horizontalMovement > 0 && reachedEnd)
                point = handle.GetClosestPoint(rightHandPosition);
            else
                point = handle.GetClosestPoint(leftHandPosition);

            currentPoint = point;
            if (point == null)
                return false;

            Vector3 direction = new Vector3(horizontal, vertical, 0f);
            neighbour = CandidatePointOnDirection(
                direction,
                point,
                point.neighbours,
                ref xDistance,
                drop);
            return neighbour != null;
        }

        private void JumpToLedge(Neighbour toPoint, float xDistance)
        {
            if (toPoint == null || toPoint.target == null)
                return;

            HandlePoints targetHandle =
                toPoint.target.GetComponentInParent<HandlePoints>();
            ParkourSurface targetSurface = targetHandle != null
                ? targetHandle.GetComponentInParent<ParkourSurface>()
                : null;
            GameObject targetLedge = targetSurface != null
                ? targetSurface.gameObject
                : targetHandle != null && targetHandle.transform.parent != null
                    ? targetHandle.transform.parent.gameObject
                    : null;
            Quaternion ledgeRotation = targetLedge != null
                ? targetLedge.transform.rotation
                : toPoint.target.transform.rotation;
            if (!TrySetLedgeTarget(
                    targetLedge,
                    targetHandle,
                    toPoint.target,
                    ledgeRotation,
                    out target))
            {
                return;
            }

            SetOnLedge(false, animationDrivenBetweenLedges: true);
            toLedge = true;
            jumping = true;

            Vector3 direction = toPoint.direction;

            if ((xDistance < smallHopMaxDistance && xDistance > -smallHopMaxDistance) && direction.y != 0)
                direction.x = 0;

            wallFound = characterDetection.FindFootCollision(
                target,
                targetRot,
                -toPoint.target.transform.forward);

            characterController.characterAnimation.LedgeToLedge(
                curClimbState,
                direction,
                ref startTime,
                ref endTime);
        }

        /// <summary>
        /// Changes Between Braced and Free Hang
        /// </summary>
        void ChangeBracedFreeHang()
        {
            if (curLedge)
            {
                if (wallFound && curClimbState != ClimbState.BHanging)
                {
                    curClimbState = ClimbState.BHanging;
                    Vector3 offset = new Vector3(0, 0f, 0.0f);
                    HandPosition = characterAnimation.animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    HandPosition.y = curLedge.transform.position.y + offset.y;
                }
                else if (!wallFound && curClimbState != ClimbState.FHanging)
                {
                    curClimbState = ClimbState.FHanging;
                    Vector3 offset = new Vector3(0, -0.1f, 0.0f);
                    HandPosition = characterAnimation.animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    HandPosition.y = curLedge.transform.position.y;
                }

                //Adjust Next Animation to the previous Anim Hand Position
                if (characterAnimation.animState.IsName("Free Hang To Braced") ||
                    characterAnimation.animState.IsName("Braced To FreeHang"))
                {
                    characterAnimation.SetMatchTarget(AvatarTarget.LeftHand, HandPosition, transform.rotation, Vector3.zero, 0.0f, 0.001f);
                }
            }
        }

        /// <summary>
        /// Checks if input direction is within angle direction range of Target Point
        /// </summary>
        public Neighbour CandidatePointOnDirection(Vector3 inputDirection, Point from, List<Neighbour> candidatePoints, ref float xDistance, bool drop)
        {
            if (!from)
                return null;

            Neighbour retPoint = null;
            float minAngle = float.PositiveInfinity;

            for (int p = 0; p < candidatePoints.Count; p++)
            {
                Neighbour targetPoint = candidatePoints[p];

                if (candidatePoints[p].target == null)
                    continue;

                if (drop && targetPoint.target.transform.position.y >= from.transform.position.y)
                    continue;

                if (!drop && targetPoint.target.transform.position.y + 0.6f < from.transform.position.y)
                    continue;

                Vector3 direction = targetPoint.target.transform.position - from.transform.position;
                Vector3 pointDirection = from.transform.InverseTransformDirection(direction);
                pointDirection.z = 0;

                //This returns the angle between input and target direction
                float angle = Mathf.Acos(Vector3.Dot(inputDirection.normalized, pointDirection.normalized)) * Mathf.Rad2Deg;

                //Stores closest target with angle difference between 40 degrees
                if (angle < minAngle && Mathf.Abs(angle) < 40)
                {
                    minAngle = angle;
                    retPoint = targetPoint;
                    xDistance = pointDirection.x;
                }
            }

            return retPoint;
        }

        /// <summary>
        /// Computes IK Solver to place the limbs at the correct Ledge and Wall Position
        /// </summary>
        void IKSolver()
        {
            RaycastHit hit1;
            RaycastHit hit2;
            RaycastHit hit3;
            RaycastHit hit4;

            Vector3 origin1 = LHand.transform.position + (transform.rotation * new Vector3(-curOriginGrabOffset.x, curOriginGrabOffset.y, curOriginGrabOffset.z));
            Vector3 origin2 = RHand.transform.position + (transform.rotation * new Vector3(curOriginGrabOffset.x, curOriginGrabOffset.y, curOriginGrabOffset.z));
            Vector3 origin3 = LFoot.transform.position + (transform.rotation * originFootIKOffset);
            Vector3 origin4 = RFoot.transform.position + (transform.rotation * originFootIKOffset);
            origin1.y = transform.position.y + curOriginGrabOffset.y;
            origin2.y = origin1.y;

            leftHandIKFound = characterController.characterDetection
                .ThrowHandRayToLedge(
                    origin1,
                    new Vector3(0.25f, -0.15f, 1).normalized,
                    IKHandRayLength,
                    out hit1);
            if (leftHandIKFound)
                leftHandPosition = hit1.point;

            rightHandIKFound = characterController.characterDetection
                .ThrowHandRayToLedge(
                    origin2,
                    new Vector3(-0.25f, -0.15f, 1).normalized,
                    IKHandRayLength,
                    out hit2);
            if (rightHandIKFound)
                rightHandPosition = hit2.point;

            leftFootIKFound = characterController.characterDetection
                .ThrowFootRayToLedge(
                    origin3,
                    Vector3.forward,
                    IKFootRayLength,
                    out hit3);
            if (leftFootIKFound)
                leftFootPosition = hit3.point + hit3.normal * 0.15f;

            rightFootIKFound = characterController.characterDetection
                .ThrowFootRayToLedge(
                    origin4,
                    Vector3.forward,
                    IKFootRayLength,
                    out hit4);
            if (rightFootIKFound)
                rightFootPosition = hit4.point + hit4.normal * 0.15f;
        }

        /// <summary>
        /// Moves player on Ledge and checks if current movement is valid
        /// </summary>
        bool CheckValidMovement(float translation)
        {
            bool ret = Mathf.Abs(translation) < 0.01f;
            RaycastHit hit1;
            RaycastHit hit2;
            RaycastHit hit3;
            RaycastHit hit4;

            Vector3 origin1 = LHand.transform.position + (transform.rotation * (curOriginGrabOffset + new Vector3(-0.18f,0,0)));
            Vector3 origin2 = RHand.transform.position + (transform.rotation * (curOriginGrabOffset));
            origin1.y = transform.position.y + curOriginGrabOffset.y - 0.05f;
            origin2.y = origin1.y;

            Vector3 origin3 = Vector3.zero;
            Vector3 origin4 = Vector3.zero;
            if (ClimbState.BHanging == curClimbState)
            {
                origin3 = transform.position + (transform.rotation * new Vector3(-0.10f, 0, 0));
                origin4 = transform.position + (transform.rotation * new Vector3(0.10f, 0, 0));
            }
            else
            {
                origin3 = transform.position + (transform.rotation * (curOriginGrabOffset + new Vector3(-0.45f, 0, 0)));
                origin4 = transform.position + (transform.rotation * (curOriginGrabOffset + new Vector3(0.35f, 0, 0)));
                origin3.y = transform.position.y + 0.5f;
                origin4.y = origin3.y;
            }

            // Checks if Player can move on the ledge with the current movement
            if (characterController.characterDetection.ThrowHandRayToLedge(origin1, Vector3.forward, IKHandRayLength, out hit1))
            {
                if (translation < 0)
                {
                    SetCurrentLedgeFromCollider(hit1.collider);
                    ret = true;
                }
            }
            if (characterController.characterDetection.ThrowHandRayToLedge(origin2, Vector3.forward, IKHandRayLength, out hit2)){
                if (translation > 0)
                {
                    SetCurrentLedgeFromCollider(hit2.collider);
                    ret = true;
                }
            }

            // A short or thin bar can fall between the two hand rays even
            // though its configured Points still define a valid movement
            // span. Use those Points as a deterministic fallback and as the
            // endpoint limit for the root-motion animation.
            if (!ret && TryGetStableGrabPosition(out Vector3 gripPosition))
            {
                ret = CanMoveWithinPointSpan(
                    GetCurrentHandlePoints(),
                    gripPosition,
                    transform.right * translation,
                    ledgeEndpointPadding);
            }

            //Checks if Foot detects a wall to place the feet
            if(curClimbState == ClimbState.BHanging)
            {
                bool b1 = characterController.characterDetection.ThrowFootRayToLedge(origin3, Vector3.forward, IKFootRayLength + 0.1f, out hit3);
                bool b2 = characterController.characterDetection.ThrowFootRayToLedge(origin4, Vector3.forward, IKFootRayLength + 0.1f, out hit4);
                if (!b1 && !b2)
                {
                    wallContactTime = 0f;
                    wallMissTime += Time.deltaTime;
                    if (wallMissTime >= wallContactGraceTime)
                        wallFound = false;
                }
                else
                    wallMissTime = 0f;
            }
            else if (curClimbState == ClimbState.FHanging)
            {
                bool b1 = characterController.characterDetection.ThrowFootRayToLedge(origin3, Vector3.forward, IKFootRayLength + 0.1f, out hit3);
                bool b2 = characterController.characterDetection.ThrowFootRayToLedge(origin4, Vector3.forward, IKFootRayLength + 0.1f, out hit4);
                if (b1 && b2)
                {
                    wallMissTime = 0f;
                    wallContactTime += Time.deltaTime;
                    if (wallContactTime >= wallContactGraceTime)
                        wallFound = true;
                }
                else
                    wallContactTime = 0f;
            }

            //If movement is valid adjust player with the motion
            if (hit1.collider != null && hit2.collider != null)
            {
                //Rotates the character towards the ledge while moving
                Vector3 direction = hit2.point - hit1.point;
                Vector3 tangent = Vector3.Cross(Vector3.up, direction).normalized;
                float alignment = ExponentialBlend(
                    ledgeRootAlignmentSmoothing,
                    Time.deltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(-tangent),
                    alignment);

                //Sets the model at a relative distance from the ledge without clipping into surface
                Vector3 origin = transform.position - transform.forward * 0.25f;
                origin.y += curOriginGrabOffset.y;

                float raylength = (curClimbState == ClimbState.BHanging) ? distanceToLedgeBraced + 0.2f : distanceToLedgeFree + 0.12f;

                if(debug)
                    Debug.DrawLine(origin, origin + -tangent * (raylength), Color.cyan);

                RaycastHit hit;
                if (characterDetection.ThrowRayOnDirection(origin, -tangent, raylength, out hit, characterDetection.ledgeLayer))
                {
                    raylength = (curClimbState == ClimbState.BHanging) ? distanceToLedgeBraced : distanceToLedgeFree;
                    Vector3 newPos = (hit.point + hit.normal * raylength);
                    Vector3 alignedPosition = new Vector3(
                        newPos.x,
                        transform.position.y,
                        newPos.z);
                    transform.position = Vector3.Lerp(
                        transform.position,
                        alignedPosition,
                        alignment);
                }
            }

            return ret;
        }

        /// <summary>
        /// Checks whether movement continues inside the world-space span of
        /// the Points configured for a ledge. Point list order is irrelevant.
        /// </summary>
        public static bool CanMoveWithinPointSpan(
            HandlePoints handle,
            Vector3 gripPosition,
            Vector3 worldMovementDirection,
            float endpointPadding = 0.18f)
        {
            if (handle == null ||
                handle.pointsInOrder == null ||
                !IsFinite(gripPosition) ||
                !IsFinite(worldMovementDirection))
            {
                return false;
            }

            if (worldMovementDirection.sqrMagnitude < 0.0001f)
                return true;

            Vector3 axis = handle.transform.right.normalized;
            if (axis.sqrMagnitude < 0.5f)
                return false;

            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            int validPointCount = 0;
            Vector3 reference = handle.transform.position;

            for (int i = 0; i < handle.pointsInOrder.Count; i++)
            {
                Point point = handle.pointsInOrder[i];
                if (point == null || !IsFinite(point.transform.position))
                    continue;

                float projection = Vector3.Dot(
                    point.transform.position - reference,
                    axis);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
                validPointCount++;
            }

            float span = maximum - minimum;
            if (validPointCount < 2 || span < 0.01f)
                return false;

            float movement = Vector3.Dot(worldMovementDirection, axis);
            if (Mathf.Abs(movement) < 0.001f)
                return false;

            float padding = Mathf.Clamp(endpointPadding, 0f, span * 0.45f);
            float grip = Vector3.Dot(gripPosition - reference, axis);
            return movement > 0f
                ? grip < maximum - padding
                : grip > minimum + padding;
        }

        /// <summary>
        /// Calculates the IK Position to place the limb
        /// </summary>
        void CalculateIKPositions(AvatarIKGoal IKGoal, ref Vector3 IKPosition)
        {
            Vector3 targetIKPosition = characterAnimation.animator.GetIKPosition(IKGoal);

            if (IKPosition != Vector3.zero)
            {
                Vector3 _IKPosition = transform.InverseTransformPoint(IKPosition);
                targetIKPosition = transform.InverseTransformPoint(targetIKPosition);
                targetIKPosition.z = _IKPosition.z;
                targetIKPosition = transform.TransformPoint(targetIKPosition);
            }

            characterAnimation.animator.SetIKPosition(IKGoal, targetIKPosition);
        }

        /// <summary>
        /// Smooths the position produced by the original HandlePoints/raycast
        /// solver. It deliberately does not add another contact source and does
        /// not change IK weight, so single ledges and connected ledges keep the
        /// same grab behaviour.
        /// </summary>
        private void ApplySmoothedIKPosition(
            AvatarIKGoal goal,
            Vector3 rawPosition,
            ref Vector3 smoothedPosition,
            ref bool initialized)
        {
            if (!IsFinite(rawPosition))
                return;

            if (!initialized)
            {
                smoothedPosition = rawPosition;
                initialized = true;
            }
            else
            {
                smoothedPosition = Vector3.Lerp(
                    smoothedPosition,
                    rawPosition,
                    ExponentialBlend(ikPositionSmoothing, Time.deltaTime));
            }

            CalculateIKPositions(goal, ref smoothedPosition);
        }

        /// <summary>
        /// Frame-rate independent interpolation factor in the [0, 1] range.
        /// </summary>
        public static float ExponentialBlend(float speed, float deltaTime)
        {
            if (!float.IsFinite(speed) || !float.IsFinite(deltaTime) ||
                speed <= 0f || deltaTime <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.Exp(-speed * deltaTime);
        }

        /// <summary>
        /// Gets the closes Point to the player to climb on the Ledge from ground
        /// </summary>
        bool TryReachLedge(RaycastHit hit, out Vector3 targetPos)
        {
            targetPos = Vector3.zero;
            if (hit.collider == null)
                return false;

            GameObject ledge = hit.transform.gameObject;
            HandlePoints handle = ledge.GetComponentInChildren<HandlePoints>();
            if (handle == null)
            {
                ParkourSurface surface =
                    hit.collider.GetComponentInParent<ParkourSurface>();
                if (surface != null)
                {
                    ledge = surface.gameObject;
                    handle = ledge.GetComponentInChildren<HandlePoints>();
                }
            }

            Point closestPoint = handle != null
                ? handle.GetClosestPoint(transform.position)
                : null;
            if (closestPoint == null ||
                !IsFinite(closestPoint.transform.position))
            {
                return false;
            }

            Quaternion ledgeRotation = hit.normal.sqrMagnitude > 0.5f
                ? Quaternion.LookRotation(-hit.normal)
                : ledge.transform.rotation;
            if (!TrySetLedgeTarget(
                    ledge,
                    handle,
                    closestPoint,
                    ledgeRotation,
                    out targetPos))
            {
                return false;
            }

            characterController.DisableController();
            toLedge = true;
            characterController.cameraController.newOffset(true);

            return true;
        }

        private bool TrySetLedgeTarget(
            GameObject ledge,
            HandlePoints handle,
            Point point,
            Quaternion rotation,
            out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;
            if (ledge == null ||
                handle == null ||
                point == null ||
                !TryGetPointGrabTarget(handle, point, out targetPosition))
            {
                return false;
            }

            curLedge = ledge;
            targetPoint = point;
            currentPoint = point;
            targetRot = rotation;
            ResetIKSmoothing();
            return true;
        }

        /// <summary>
        /// Both an initial grab and a ledge-to-ledge jump use the same GPoint
        /// anchor. This is the package's original alignment contract.
        /// </summary>
        public static bool TryGetPointGrabTarget(
            HandlePoints handle,
            Point point,
            out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;
            if (handle == null ||
                point == null ||
                !IsFinite(point.transform.position))
            {
                return false;
            }

            targetPosition = point.transform.position;
            if (handle.furthestRight == point)
            {
                targetPosition -=
                    handle.transform.right * RightEndpointGrabOffset;
            }

            return IsFinite(targetPosition);
        }

        /// <summary>
        /// A manually released ledge stays blocked until the player lands.
        /// Other HandlePoints remain available, so a fall can still transition
        /// into a deliberately placed lower ledge.
        /// </summary>
        public static bool ShouldBlockLedgeRegrab(
            HandlePoints releasedHandle,
            HandlePoints candidateHandle,
            bool isGrounded)
        {
            return !isGrounded &&
                   releasedHandle != null &&
                   candidateHandle == releasedHandle;
        }

        private static HandlePoints FindHandlePoints(Collider collider)
        {
            if (collider == null)
                return null;

            HandlePoints handle =
                collider.GetComponentInChildren<HandlePoints>(true);
            handle ??= collider.GetComponentInParent<HandlePoints>();

            if (handle == null)
            {
                ParkourSurface surface =
                    collider.GetComponentInParent<ParkourSurface>();
                if (surface != null)
                    handle = surface.GetComponentInChildren<HandlePoints>(true);
            }

            return handle;
        }

        private void ReleaseCurrentLedge(bool blockReleasedLedge)
        {
            if (blockReleasedLedge)
                releasedRegrabHandle = GetCurrentHandlePoints();

            active = false;
            wallFound = false;
            reachedEnd = false;
            SetOnLedge(false);
            toLedge = false;
            jumping = false;
            curLedge = null;
            targetPoint = null;
            currentPoint = null;
            target = Vector3.zero;
            characterController.isJumping = true;
            characterController.onAir = false;
            curClimbState = ClimbState.None;
            ResetIKSmoothing();
            characterAnimation.DropLedge((int)curClimbState);
            characterController.cameraController?.newOffset(false);
        }

        private void SetOnLedge(
            bool attached,
            bool animationDrivenBetweenLedges = false)
        {
            onLedge = attached;

            MovementCharacterController movement =
                characterController?.characterMovement ??
                GetComponent<MovementCharacterController>();
            movement?.SetAnimationDrivenClimb(
                attached || animationDrivenBetweenLedges);
        }

        private void SetCurrentLedgeFromCollider(Collider collider)
        {
            if (collider == null)
                return;

            ParkourSurface surface =
                collider.GetComponentInParent<ParkourSurface>();
            GameObject ledge = surface != null
                ? surface.gameObject
                : collider.gameObject;

            if (ledge.GetComponentInChildren<HandlePoints>() != null)
                curLedge = ledge;
        }

        private HandlePoints GetCurrentHandlePoints()
        {
            return curLedge != null
                ? curLedge.GetComponentInChildren<HandlePoints>()
                : null;
        }

        private bool TryGetStableGrabPosition(out Vector3 position)
        {
            position = Vector3.zero;

            if (LHand != null && RHand != null &&
                IsFinite(LHand.transform.position) &&
                IsFinite(RHand.transform.position))
            {
                position = (LHand.transform.position +
                            RHand.transform.position) * 0.5f;
            }
            else if (leftHandIKFound && rightHandIKFound &&
                     IsFinite(leftHandPosition) &&
                     IsFinite(rightHandPosition))
            {
                position = (leftHandPosition + rightHandPosition) * 0.5f;
            }
            else
            {
                HandlePoints handle = GetCurrentHandlePoints();
                Point closest = handle != null
                    ? handle.GetClosestPoint(transform.position)
                    : null;
                if (closest == null)
                    return false;

                position = closest.transform.position;
            }

            // The Point is the authoritative height of a thin ledge; the
            // animation bones can be a few centimetres above or below it.
            HandlePoints currentHandle = GetCurrentHandlePoints();
            Point nearestPoint = currentHandle != null
                ? currentHandle.GetClosestPoint(position)
                : null;
            if (nearestPoint != null)
                position.y = nearestPoint.transform.position.y;

            return IsLocalClimbTarget(transform.position, position);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private void ResetIKSmoothing()
        {
            leftHandIKFound = false;
            rightHandIKFound = false;
            leftFootIKFound = false;
            rightFootIKFound = false;
            leftHandIKInitialized = false;
            rightHandIKInitialized = false;
            leftFootIKInitialized = false;
            rightFootIKInitialized = false;
            smoothedLeftHandPosition = Vector3.zero;
            smoothedRightHandPosition = Vector3.zero;
            smoothedLeftFootPosition = Vector3.zero;
            smoothedRightFootPosition = Vector3.zero;
            wallContactTime = 0f;
            wallMissTime = 0f;
        }
    }
}
