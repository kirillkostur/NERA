using NERA.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class IOPowerDisruptionContentTests
    {
        [TestCase(
            "Assets/_Project/NERA/Prefabs/IO/IO_Red_Enforcer.prefab",
            8f,
            14f,
            4f)]
        [TestCase(
            "Assets/_Project/NERA/Prefabs/IO/IO_Violet_Overseer.prefab",
            12.5f,
            10f,
            3f)]
        public void LastTwoIOPrefabsHaveConfiguredPowerDisruption(
            string prefabPath,
            float radius,
            float cooldown,
            float initialDelay)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            IOPowerDisruptionAbility ability =
                prefab.GetComponent<IOPowerDisruptionAbility>();
            Assert.That(ability, Is.Not.Null, prefabPath);
            Assert.That(
                ability.Radius,
                Is.EqualTo(radius).Within(0.001f));
            Assert.That(
                ability.Cooldown,
                Is.EqualTo(cooldown).Within(0.001f));

            SerializedObject serialized =
                new SerializedObject(ability);
            Assert.That(
                serialized.FindProperty("initialDelay").floatValue,
                Is.EqualTo(initialDelay).Within(0.001f));
        }

        [TestCase(
            "Assets/_Project/NERA/Prefabs/IO/IO_Blue_Weak.prefab")]
        [TestCase(
            "Assets/_Project/NERA/Prefabs/IO/IO_Green_Regenerator.prefab")]
        [TestCase(
            "Assets/_Project/NERA/Prefabs/IO/IO_Yellow_Hunter.prefab")]
        public void FirstThreeIOPrefabsDoNotCastPowerDisruption(
            string prefabPath)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(
                prefab.GetComponent<IOPowerDisruptionAbility>(),
                Is.Null);
        }
    }
}
