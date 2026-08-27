using System.Collections;
using NERA.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NERA.Tests
{
    public sealed class SequentialEllipsisTextPlayModeTests
    {
        [UnityTest]
        public IEnumerator HighlightMovesFromOneDotToTheNext()
        {
            GameObject prefab = Resources.Load<GameObject>(
                LoadingScreenController.PrefabResourcePath);
            Assert.That(prefab, Is.Not.Null);
            GameObject source = prefab.transform.Find(
                "LoadingWindow/LoadingText").gameObject;
            GameObject root = Object.Instantiate(source);
            SequentialEllipsisText animator =
                root.GetComponent<SequentialEllipsisText>();
            Assert.That(animator, Is.Not.Null);
            animator.SetBaseText("Сохранение...");
            yield return null;

            int firstDot = animator.ActiveDotIndex;
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.That(animator.ActiveDotIndex, Is.Not.EqualTo(firstDot));
            Object.Destroy(root);
        }
    }
}
