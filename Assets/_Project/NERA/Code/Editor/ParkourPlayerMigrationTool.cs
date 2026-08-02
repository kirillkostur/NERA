using System;
using System.Collections.Generic;
using Climbing;
using NERA.Combat;
using NERA.Interaction;
using NERA.Inventory;
using NERA.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ParkourPlayerMigrationTool
{
    private const string PlayerPrefabPath =
        "Assets/_Project/NERA/Prefabs/Player/Player.prefab";
    private const string MainScenePath =
        "Assets/_Project/NERA/Scenes/MainScene.unity";
    private const string StationScenePath =
        "Assets/_Project/NERA/Scenes/Player_Station.unity";
    private const string ParkourPrefabFolder =
        "Assets/_Project/NERA/Prefabs/Parkour";
    private const string ParkourDevelopmentScenePath =
        "Assets/_Project/NERA/_Development/Parkour/Testing.unity";

    private const int PlayerLayer = 3;
    private const int ParkourLedgeLayer = 14;
    private const int ParkourSurfaceLayer = 15;

    [MenuItem("NERA/Parkour/Rebuild Player Integration")]
    public static void Apply()
    {
        ConfigureEnvironmentPrefabs();
        ConfigureParkourPointPrefabs();
        BuildPlayerPrefab();
        ConfigureDevelopmentScene();
        ReplaceMainScenePlayer();
        RemoveLegacyCameraZones();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("NERA parkour player integration rebuilt successfully.");
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            Apply();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void BuildPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform modelTransform = root.transform.Find("PlayerModel");
            if (modelTransform == null)
                throw new InvalidOperationException(
                    "Parkour Player prefab has no PlayerModel child.");

            GameObject model = modelTransform.gameObject;
            root.name = "Player";
            root.tag = "Untagged";
            model.tag = "Player";
            SetLayerRecursively(root, PlayerLayer);

            Animator animator = model.GetComponent<Animator>();
            Rigidbody locomotionBody = model.GetComponent<Rigidbody>();
            ThirdPersonController thirdPerson =
                model.GetComponent<ThirdPersonController>();
            DetectionCharacterController detection =
                model.GetComponent<DetectionCharacterController>();
            if (animator == null || locomotionBody == null || thirdPerson == null)
            {
                throw new InvalidOperationException(
                    "PlayerModel is missing Animator, Rigidbody or parkour controller.");
            }

            animator.applyRootMotion = true;
            CapsuleCollider[] motorColliders =
                model.GetComponents<CapsuleCollider>();
            if (motorColliders.Length < 2)
                throw new InvalidOperationException(
                    "PlayerModel requires normal and sliding CapsuleCollider components.");

            thirdPerson.normalCapsuleCollider = motorColliders[0];
            thirdPerson.slidingCapsuleCollider = motorColliders[1];
            thirdPerson.SetSlidingCollider(false);

            ConfigureDetectionMasks(detection);
            ConfigureCameraCollision(root);

            PlayerInteractionController interaction =
                GetOrAdd<PlayerInteractionController>(model);
            ConfigureInteraction(interaction);
            GetOrAdd<PlayerInventory>(model);
            GetOrAdd<PlayerEquipmentController>(model);
            GetOrAdd<PlayerEnergyWeaponController>(model);
            ParkourPlayerBridge bridge = GetOrAdd<ParkourPlayerBridge>(model);

            RagdollBuildResult ragdoll = BuildRagdoll(animator);
            PlayerHealth health = GetOrAdd<PlayerHealth>(model);
            ConfigureHealth(
                health,
                animator,
                ragdoll.Root,
                locomotionBody,
                motorColliders,
                ragdoll.HipsBody);
            ConfigureBridge(
                bridge,
                model,
                root,
                locomotionBody,
                motorColliders);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureEnvironmentPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { ParkourPrefabFolder });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ParkourSurfaceType type = GetSurfaceType(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ClearTagsRecursively(root);
                if (type != ParkourSurfaceType.None)
                {
                    ParkourSurface surface = GetOrAdd<ParkourSurface>(root);
                    surface.Configure(type);
                    int layer = (type & ParkourSurfaceType.Ledge) != 0
                        ? ParkourLedgeLayer
                        : ParkourSurfaceLayer;
                    SetLayerRecursively(root, layer);
                    foreach (HandlePoints points in
                             root.GetComponentsInChildren<HandlePoints>(true))
                    {
                        SetLayerRecursively(
                            points.gameObject,
                            16);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ConfigureParkourPointPrefabs()
    {
        string[] paths =
        {
            "Assets/_Project/NERA/Resources/Parkour/Climbing/GPoint.prefab",
            "Assets/_Project/NERA/Prefabs/Parkour/Jump/Jump Points.prefab",
        };

        foreach (string path in paths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ClearTagsRecursively(root);
                SetLayerRecursively(root, 16);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static ParkourSurfaceType GetSurfaceType(string assetPath)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        if (name.Contains("Small Ledge", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Ledge", StringComparison.OrdinalIgnoreCase))
        {
            return ParkourSurfaceType.Ledge | ParkourSurfaceType.Climb;
        }
        if (name.Equals("Wall", StringComparison.OrdinalIgnoreCase))
            return ParkourSurfaceType.Climb;
        if (name.Contains("Reach", StringComparison.OrdinalIgnoreCase))
            return ParkourSurfaceType.Reach;
        if (name.Contains("Slide", StringComparison.OrdinalIgnoreCase))
            return ParkourSurfaceType.Slide;
        if (name.Contains("Obstacle", StringComparison.OrdinalIgnoreCase))
            return ParkourSurfaceType.Vault;
        if (name.Contains("Box", StringComparison.OrdinalIgnoreCase))
            return ParkourSurfaceType.VaultOver;
        if (name.Contains("Pole", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Pilar", StringComparison.OrdinalIgnoreCase))
        {
            return ParkourSurfaceType.Pole;
        }

        return ParkourSurfaceType.None;
    }

    private static void ConfigureDevelopmentScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ParkourDevelopmentScenePath,
            OpenSceneMode.Single);
        var prefabRoots = new HashSet<GameObject>();

        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (Transform child in
                     sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                GameObject target = child.gameObject;
                ParkourSurfaceType legacyType =
                    GetLegacySurfaceType(target.tag);
                if (legacyType != ParkourSurfaceType.None)
                {
                    GetOrAdd<ParkourSurface>(target).Configure(legacyType);
                    SetLayerRecursively(target, ParkourSurfaceLayer);
                    target.tag = "Untagged";
                }
                GameObject prefabRoot =
                    PrefabUtility.GetNearestPrefabInstanceRoot(target);
                if (prefabRoot != null)
                    prefabRoots.Add(prefabRoot);
            }
        }

        foreach (GameObject prefabRoot in prefabRoots)
        {
            string path =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    prefabRoot);
            ParkourSurfaceType type = GetSurfaceType(path);
            if (type == ParkourSurfaceType.None)
                continue;

            GetOrAdd<ParkourSurface>(prefabRoot).Configure(type);
            int layer = (type & ParkourSurfaceType.Ledge) != 0
                ? ParkourLedgeLayer
                : ParkourSurfaceLayer;
            SetLayerRecursively(prefabRoot, layer);
        }

        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (HandlePoints points in
                     sceneRoot.GetComponentsInChildren<HandlePoints>(true))
            {
                SetLayerRecursively(points.gameObject, 16);
            }

            ParkourPlayerBridge bridge =
                sceneRoot.GetComponentInChildren<ParkourPlayerBridge>(true);
            if (bridge != null)
            {
                Transform playerRoot = bridge.transform.parent;
                SetLayerRecursively(
                    playerRoot != null
                        ? playerRoot.gameObject
                        : bridge.gameObject,
                    PlayerLayer);
                bridge.gameObject.tag = "Player";
            }
        }

        EditorSceneManager.SaveScene(scene);
    }

    private static ParkourSurfaceType GetLegacySurfaceType(string tag)
    {
        switch (tag)
        {
            case "Pole":
                return ParkourSurfaceType.Pole;
            case "Reach":
                return ParkourSurfaceType.Reach;
            case "Slide":
                return ParkourSurfaceType.Slide;
            case "VaultObstacle":
                return ParkourSurfaceType.Vault;
            case "VaultOver":
                return ParkourSurfaceType.VaultOver;
            default:
                return ParkourSurfaceType.None;
        }
    }

    private static RagdollBuildResult BuildRagdoll(Animator animator)
    {
        var bodies = new Dictionary<HumanBodyBones, Rigidbody>();
        AddBody(animator, bodies, HumanBodyBones.Hips, 10f);
        AddBody(animator, bodies, HumanBodyBones.Spine, 7f);
        AddBody(animator, bodies, HumanBodyBones.Chest, 8f);
        AddBody(animator, bodies, HumanBodyBones.Head, 5f);
        AddBody(animator, bodies, HumanBodyBones.LeftUpperArm, 2f);
        AddBody(animator, bodies, HumanBodyBones.LeftLowerArm, 1.5f);
        AddBody(animator, bodies, HumanBodyBones.RightUpperArm, 2f);
        AddBody(animator, bodies, HumanBodyBones.RightLowerArm, 1.5f);
        AddBody(animator, bodies, HumanBodyBones.LeftUpperLeg, 7f);
        AddBody(animator, bodies, HumanBodyBones.LeftLowerLeg, 4f);
        AddBody(animator, bodies, HumanBodyBones.RightUpperLeg, 7f);
        AddBody(animator, bodies, HumanBodyBones.RightLowerLeg, 4f);

        ConfigureBox(bodies, HumanBodyBones.Hips,
            new Vector3(0f, 0.08f, 0f), new Vector3(0.32f, 0.22f, 0.24f));
        ConfigureBox(bodies, HumanBodyBones.Spine,
            new Vector3(0f, 0.09f, 0f), new Vector3(0.28f, 0.24f, 0.2f));
        ConfigureBox(bodies, HumanBodyBones.Chest,
            new Vector3(0f, 0.08f, 0f), new Vector3(0.34f, 0.25f, 0.2f));
        ConfigureSphere(bodies, HumanBodyBones.Head, 0.14f);

        ConfigureCapsule(animator, bodies, HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm, 0.28f);
        ConfigureCapsule(animator, bodies, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand, 0.24f);
        ConfigureCapsule(animator, bodies, HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm, 0.28f);
        ConfigureCapsule(animator, bodies, HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand, 0.24f);
        ConfigureCapsule(animator, bodies, HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg, 0.3f);
        ConfigureCapsule(animator, bodies, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot, 0.24f);
        ConfigureCapsule(animator, bodies, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg, 0.3f);
        ConfigureCapsule(animator, bodies, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot, 0.24f);

        Connect(bodies, HumanBodyBones.Spine, HumanBodyBones.Hips, 20f, 25f);
        Connect(bodies, HumanBodyBones.Chest, HumanBodyBones.Spine, 20f, 20f);
        Connect(bodies, HumanBodyBones.Head, HumanBodyBones.Chest, 25f, 25f);
        Connect(bodies, HumanBodyBones.LeftUpperArm, HumanBodyBones.Chest, 45f, 35f);
        Connect(bodies, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm, 10f, 60f);
        Connect(bodies, HumanBodyBones.RightUpperArm, HumanBodyBones.Chest, 45f, 35f);
        Connect(bodies, HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm, 10f, 60f);
        Connect(bodies, HumanBodyBones.LeftUpperLeg, HumanBodyBones.Hips, 30f, 35f);
        Connect(bodies, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftUpperLeg, 10f, 60f);
        Connect(bodies, HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips, 30f, 35f);
        Connect(bodies, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg, 10f, 60f);

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        return new RagdollBuildResult
        {
            Root = hips,
            HipsBody = bodies.TryGetValue(
                HumanBodyBones.Hips,
                out Rigidbody hipsBody)
                ? hipsBody
                : null,
        };
    }

    private static void AddBody(
        Animator animator,
        IDictionary<HumanBodyBones, Rigidbody> bodies,
        HumanBodyBones bone,
        float mass)
    {
        Transform transform = animator.GetBoneTransform(bone);
        if (transform == null)
            return;

        Rigidbody body = GetOrAdd<Rigidbody>(transform.gameObject);
        body.mass = mass;
        body.linearDamping = 0.05f;
        body.angularDamping = 0.05f;
        body.useGravity = false;
        body.isKinematic = true;
        body.detectCollisions = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        bodies[bone] = body;
    }

    private static void ConfigureBox(
        IReadOnlyDictionary<HumanBodyBones, Rigidbody> bodies,
        HumanBodyBones bone,
        Vector3 center,
        Vector3 size)
    {
        if (!bodies.TryGetValue(bone, out Rigidbody body))
            return;

        BoxCollider collider = GetOrAdd<BoxCollider>(body.gameObject);
        collider.center = center;
        collider.size = size;
        collider.enabled = false;
    }

    private static void ConfigureSphere(
        IReadOnlyDictionary<HumanBodyBones, Rigidbody> bodies,
        HumanBodyBones bone,
        float radius)
    {
        if (!bodies.TryGetValue(bone, out Rigidbody body))
            return;

        SphereCollider collider = GetOrAdd<SphereCollider>(body.gameObject);
        collider.center = new Vector3(0f, 0.08f, 0f);
        collider.radius = radius;
        collider.enabled = false;
    }

    private static void ConfigureCapsule(
        Animator animator,
        IReadOnlyDictionary<HumanBodyBones, Rigidbody> bodies,
        HumanBodyBones bone,
        HumanBodyBones endBone,
        float radiusRatio)
    {
        if (!bodies.TryGetValue(bone, out Rigidbody body))
            return;

        Transform end = animator.GetBoneTransform(endBone);
        if (end == null)
            return;

        Vector3 localEnd = body.transform.InverseTransformPoint(end.position);
        float length = Mathf.Max(0.08f, localEnd.magnitude);
        Vector3 absolute = new Vector3(
            Mathf.Abs(localEnd.x),
            Mathf.Abs(localEnd.y),
            Mathf.Abs(localEnd.z));
        int direction = absolute.x > absolute.y
            ? (absolute.x > absolute.z ? 0 : 2)
            : (absolute.y > absolute.z ? 1 : 2);

        CapsuleCollider collider = GetOrAdd<CapsuleCollider>(body.gameObject);
        collider.direction = direction;
        collider.center = localEnd * 0.5f;
        collider.radius = Mathf.Clamp(length * radiusRatio, 0.045f, 0.14f);
        collider.height = Mathf.Max(length, collider.radius * 2f);
        collider.enabled = false;
    }

    private static void Connect(
        IReadOnlyDictionary<HumanBodyBones, Rigidbody> bodies,
        HumanBodyBones child,
        HumanBodyBones parent,
        float twist,
        float swing)
    {
        if (!bodies.TryGetValue(child, out Rigidbody childBody) ||
            !bodies.TryGetValue(parent, out Rigidbody parentBody))
        {
            return;
        }

        CharacterJoint joint = GetOrAdd<CharacterJoint>(childBody.gameObject);
        joint.connectedBody = parentBody;
        joint.autoConfigureConnectedAnchor = true;
        joint.enableCollision = false;
        joint.enablePreprocessing = false;
        Vector3 toParent = childBody.transform.InverseTransformDirection(
            parentBody.worldCenterOfMass - childBody.worldCenterOfMass);
        joint.axis = toParent.sqrMagnitude > 0.001f
            ? toParent.normalized
            : Vector3.right;
        joint.swingAxis = Vector3.Cross(joint.axis, Vector3.up).sqrMagnitude > 0.001f
            ? Vector3.Cross(joint.axis, Vector3.up).normalized
            : Vector3.forward;
        joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
        joint.highTwistLimit = new SoftJointLimit { limit = twist };
        joint.swing1Limit = new SoftJointLimit { limit = swing };
        joint.swing2Limit = new SoftJointLimit { limit = swing };
    }

    private static void ConfigureHealth(
        PlayerHealth health,
        Animator animator,
        Transform ragdollRoot,
        Rigidbody locomotionBody,
        Collider[] locomotionColliders,
        Rigidbody hipsBody)
    {
        SerializedObject serialized = new SerializedObject(health);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("ragdollRoot").objectReferenceValue = ragdollRoot;
        serialized.FindProperty("locomotionBody").objectReferenceValue =
            locomotionBody;
        serialized.FindProperty("impulseBody").objectReferenceValue = hipsBody;
        SetObjectArray(
            serialized.FindProperty("locomotionColliders"),
            locomotionColliders);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBridge(
        ParkourPlayerBridge bridge,
        GameObject model,
        GameObject root,
        Rigidbody locomotionBody,
        Collider[] locomotionColliders)
    {
        SerializedObject serialized = new SerializedObject(bridge);
        SetReference<InputCharacterController>(serialized, "inputController", model);
        SetReference<ThirdPersonController>(serialized, "parkourController", model);
        SetReference<MovementCharacterController>(serialized, "movementController", model);
        SetReference<VaultingController>(serialized, "vaultingController", model);
        SetReference<ClimbController>(serialized, "climbController", model);
        SetReference<JumpPredictionController>(serialized, "jumpController", model);
        SetReference<PlayerInteractionController>(serialized, "interactionController", model);
        SetReference<PlayerEquipmentController>(serialized, "equipmentController", model);
        serialized.FindProperty("locomotionBody").objectReferenceValue =
            locomotionBody;
        serialized.FindProperty("gameplayCamera").objectReferenceValue =
            root.GetComponentInChildren<Camera>(true);
        SetObjectArray(
            serialized.FindProperty("locomotionColliders"),
            locomotionColliders);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureDetectionMasks(
        DetectionCharacterController detection)
    {
        int ledgeMask = 1 << ParkourLedgeLayer;
        int climbMask = (1 << 9) | (1 << ParkourLedgeLayer) |
                        (1 << ParkourSurfaceLayer);
        int environmentMask = (1 << 0) | (1 << 9) | (1 << 10) |
                              (1 << 11) | ledgeMask |
                              (1 << ParkourSurfaceLayer);
        SerializedObject serialized = new SerializedObject(detection);
        serialized.FindProperty("ledgeLayer").intValue = ledgeMask;
        serialized.FindProperty("climbLayer").intValue = climbMask;
        serialized.FindProperty("environmentLayer").intValue = environmentMask;
        serialized.FindProperty("groundLayer").intValue = environmentMask;
        serialized.FindProperty("parkourPointLayer").intValue =
            ledgeMask | (1 << ParkourSurfaceLayer) | (1 << 16);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureInteraction(
        PlayerInteractionController interaction)
    {
        const int obstructionMask =
            (1 << 0) | (1 << 9) | (1 << 10) | (1 << 11) |
            (1 << 14) | (1 << 15);
        const int overlapMask = (1 << 6) | (1 << 7);
        SerializedObject serialized = new SerializedObject(interaction);
        serialized.FindProperty("overlapMask").intValue = overlapMask;
        serialized.FindProperty("obstructionMask").intValue =
            obstructionMask;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCameraCollision(GameObject root)
    {
        int collisionMask = (1 << 0) | (1 << 9) | (1 << 10) |
                            (1 << 11) | (1 << ParkourLedgeLayer) |
                            (1 << ParkourSurfaceLayer);
        foreach (MonoBehaviour behaviour in
                 root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null ||
                behaviour.GetType().Name != "CinemachineCollider")
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty mask =
                serialized.FindProperty("m_CollideAgainst");
            if (mask != null)
            {
                mask.intValue = collisionMask;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void ReplaceMainScenePlayer()
    {
        Scene scene = EditorSceneManager.OpenScene(
            MainScenePath,
            OpenSceneMode.Single);
        GameObject runtimeRoot = FindRoot(scene, "RuntimeRoot");
        if (runtimeRoot == null)
            throw new InvalidOperationException("MainScene has no RuntimeRoot.");

        var rootsToRemove = new List<GameObject>();
        foreach (Transform child in runtimeRoot.transform)
        {
            if (child.name == "Player" ||
                child.name == "Player_Camera" ||
                child.name.StartsWith(
                    "Player (Missing Prefab",
                    StringComparison.Ordinal))
            {
                rootsToRemove.Add(child.gameObject);
            }
        }

        foreach (GameObject oldRoot in rootsToRemove)
            Object.DestroyImmediate(oldRoot);

        foreach (GameObject rootObject in scene.GetRootGameObjects())
            RemoveNamedObjects(rootObject.transform, "AimCrosshair");

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(
            prefab,
            scene);
        player.transform.SetParent(runtimeRoot.transform, false);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;
        player.transform.localScale = Vector3.one;
        EditorSceneManager.SaveScene(scene);
    }

    private static void RemoveLegacyCameraZones()
    {
        Scene scene = EditorSceneManager.OpenScene(
            StationScenePath,
            OpenSceneMode.Single);
        var zones = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in
                     root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null &&
                    behaviour.GetType().Name == "CameraDistanceZone" &&
                    !zones.Contains(behaviour.gameObject))
                {
                    zones.Add(behaviour.gameObject);
                }
            }
        }

        foreach (GameObject zone in zones)
            Object.DestroyImmediate(zone);

        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return null;
    }

    private static void RemoveNamedObjects(Transform root, string name)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                Object.DestroyImmediate(child.gameObject);
            else
                RemoveNamedObjects(child, name);
        }
    }

    private static void ClearTagsRecursively(GameObject root)
    {
        root.tag = "Untagged";
        foreach (Transform child in root.transform)
            ClearTagsRecursively(child.gameObject);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetReference<T>(
        SerializedObject serialized,
        string field,
        GameObject source) where T : Component
    {
        serialized.FindProperty(field).objectReferenceValue =
            source.GetComponent<T>();
    }

    private static void SetObjectArray<T>(
        SerializedProperty property,
        IReadOnlyList<T> values) where T : Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private struct RagdollBuildResult
    {
        public Transform Root;
        public Rigidbody HipsBody;
    }
}
