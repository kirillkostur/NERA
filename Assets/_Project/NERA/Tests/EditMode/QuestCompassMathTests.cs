using NERA.Navigation;
using NERA.UI;
using NUnit.Framework;
using UnityEngine;

namespace NERA.Tests.EditMode
{
    public sealed class QuestCompassMathTests
    {
        [Test]
        public void AlignedTargetIsCenteredOnCompass()
        {
            GameObject cameraObject = new GameObject("Camera");
            try
            {
                cameraObject.transform.position = Vector3.zero;
                cameraObject.transform.rotation = Quaternion.identity;

                float angle = QuestCompassMath.SignedHorizontalAngle(
                    cameraObject.transform,
                    new Vector3(0f, 4f, 30f));
                float x = QuestCompassMath.CalculateCompassX(
                    angle,
                    90f,
                    350f);

                Assert.That(angle, Is.EqualTo(0f).Within(0.001f));
                Assert.That(x, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void OffRangeTargetIsClampedToCompassEdge()
        {
            Assert.That(
                QuestCompassMath.CalculateCompassX(175f, 90f, 350f),
                Is.EqualTo(350f));
            Assert.That(
                QuestCompassMath.CalculateCompassX(-175f, 90f, 350f),
                Is.EqualTo(-350f));
        }

        [Test]
        public void DistanceAndPerObjectMarkerIdAreFormatted()
        {
            Assert.That(QuestCompassMath.FormatDistance(38.3f), Is.EqualTo("38m"));
            Assert.That(
                QuestMarkerAnchor.ResolveStageId(
                    "target.{targetId}",
                    "quest.demo",
                    "Battery.01"),
                Is.EqualTo("target.battery.01"));
        }
    }
}
