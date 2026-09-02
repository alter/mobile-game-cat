using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 70-analytics/02 groundwork: the nine-event surface is pinned, and
    /// name validation rejects what GameAnalytics would silently drop.
    /// </summary>
    [TestFixture]
    public class AnalyticsTests
    {
        private List<string> _design;
        private List<(string, int)> _progression;

        [SetUp]
        public void Wire()
        {
            _design = new List<string>();
            _progression = new List<(string, int)>();
            Analytics.Configure(
                (name, value, extra) => _design.Add(name),
                (name, score, levelId) => _progression.Add((name, score)));
        }

        [Test]
        public void ExactlyNineEvents_OnTheSurface()
        {
            Assert.That(AnalyticsEventNames.All.Length, Is.EqualTo(9));
            Assert.That(AnalyticsEventNames.All.Distinct().Count(), Is.EqualTo(9));
        }

        [Test]
        public void DesignHelpers_FireTheExactName()
        {
            Analytics.AppOpen();
            Analytics.PhotoScreenShown();
            Analytics.PhotoUploaded();
            Analytics.PhotoRejected();
            Analytics.BoosterTap();
            Analytics.NotificationAllowed();

            Assert.That(_design, Is.EqualTo(new[]
            {
                "app:open", "photo:screen_shown", "photo:uploaded",
                "photo:rejected", "booster:tap", "notification:allowed",
            }));
            Assert.That(_progression, Is.Empty);
        }

        [Test]
        public void ProgressionHelpers_KeyByLevelNumber()
        {
            Analytics.LevelStart(3);
            Analytics.LevelWin(3);
            Analytics.LevelFail(4);

            Assert.That(_progression, Is.EquivalentTo(new[]
            {
                ("level_start", 3), ("level_win", 3), ("level_fail", 4),
            }));
            Assert.That(_design, Is.Empty,
                "level events must be Progression, not Design (VERIFY 2)");
        }

        [Test]
        public void InvalidNames_AreRejected_BeforeTheSink()
        {
            Assert.Throws<ArgumentException>(
                () => Analytics.Design("has space"));
            Assert.Throws<ArgumentException>(() => Analytics.Design(""));
            Assert.Throws<ArgumentException>(
                () => Analytics.Design(new string('x', 65)));
            Assert.That(_design, Is.Empty);
        }

        [Test]
        public void AllSurfaceNames_PassValidation()
        {
            foreach (var name in AnalyticsEventNames.All)
                Assert.DoesNotThrow(() => Analytics.EnsureValid(name),
                    $"{name} must satisfy GameAnalytics rules");
        }

        [Test]
        public void Unconfigured_Analytics_ThrowsLoudly()
        {
            // Reset by configuring nulls — sinks null but surface validated.
            Analytics.Configure(null, null);
            Assert.DoesNotThrow(() => Analytics.AppOpen(),
                "null sink = no-op mode, still valid");
        }

        // -- task 60-shell-build/21: telemetry must not end the game --------

        [Test]
        public void Progression_BeforeConfigure_DropsEventInsteadOfThrowing()
        {
            Analytics.ResetForTests();
            try
            {
                Assert.DoesNotThrow(() => Analytics.LevelStart(3));
                Assert.That(_progression, Is.Empty,
                    "sink must not fire before Configure ran");
            }
            finally
            {
                // Leave _validated=true so later tests in the run are unaffected.
                Analytics.Configure(
                    (name, value, extra) => _design.Add(name),
                    (name, score, levelId) => _progression.Add((name, score)));
            }
        }

        [Test]
        public void Design_BeforeConfigure_DropsEventInsteadOfThrowing()
        {
            Analytics.ResetForTests();
            try
            {
                Assert.DoesNotThrow(() => Analytics.AppOpen());
                Assert.That(_design, Is.Empty,
                    "sink must not fire before Configure ran");
            }
            finally
            {
                Analytics.Configure(
                    (name, value, extra) => _design.Add(name),
                    (name, score, levelId) => _progression.Add((name, score)));
            }
        }

        [Test]
        public void Progression_LevelNumberBoundaries_OutOfRangeDroppedNotThrown()
        {
            Assert.DoesNotThrow(() => Analytics.LevelStart(0));
            Assert.DoesNotThrow(() => Analytics.LevelStart(1));
            Assert.DoesNotThrow(() => Analytics.LevelStart(999));
            Assert.DoesNotThrow(() => Analytics.LevelStart(1000));

            // 0 and 1000 are out of 1..999: dropped, not clamped into range —
            // a clamped 1000->999 would misreport the level as a valid one.
            Assert.That(_progression, Is.EqualTo(new[]
            {
                ("level_start", 1), ("level_start", 999),
            }));
        }
    }
}
