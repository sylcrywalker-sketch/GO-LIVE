using System;
using GoLive.PcBuilding;
using NUnit.Framework;

namespace GoLive.Tests
{
    // The fixed enter/leave timeline of PC Build Mode: the PC comes over first, then the side panel comes off; leaving
    // plays it backwards.
    public sealed class PcBuildModeSequenceTests
    {
        private const float Approach = 0.6f;
        private const float Cover = 0.55f;
        private const float Overlap = 0.15f;
        private const float Total = Approach - Overlap + Cover;

        [Test]
        public void OpeningBringsThePcFirstThenTakesTheSidePanelOff()
        {
            PcBuildModeSequence sequence = new(Approach, Cover, Overlap);

            Assert.That(sequence.Phase, Is.EqualTo(PcBuildModePhase.Closed));
            Assert.That(sequence.IsActive, Is.False);
            Assert.That(sequence.Begin(), Is.True);
            Assert.That(sequence.Phase, Is.EqualTo(PcBuildModePhase.Opening));

            sequence.Tick(0.24f);
            Assert.That(sequence.ApproachAmount, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(sequence.CoverAmount, Is.Zero, "the panel waits for the PC");
            Assert.That(sequence.Progress, Is.EqualTo(0.24f / Total).Within(1e-5f));
            Assert.That(sequence.IsInteractive, Is.False);

            sequence.Tick(0.3f);
            Assert.That(sequence.ApproachAmount, Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(sequence.CoverAmount, Is.EqualTo((0.54f - (Approach - Overlap)) / Cover).Within(1e-5f), "the panel starts just before the PC settles");

            Assert.That(sequence.Tick(Total), Is.EqualTo(PcBuildModePhase.Open));
            Assert.That(sequence.ApproachAmount, Is.EqualTo(1f));
            Assert.That(sequence.CoverAmount, Is.EqualTo(1f));
            Assert.That(sequence.Progress, Is.EqualTo(1f));
            Assert.That(sequence.IsInteractive, Is.True);
            Assert.That(sequence.Begin(), Is.False, "already open");
        }

        [Test]
        public void ClosingPlaysTheSameTimelineBackwards()
        {
            PcBuildModeSequence sequence = Opened();

            Assert.That(sequence.End(), Is.True);
            Assert.That(sequence.Phase, Is.EqualTo(PcBuildModePhase.Closing));
            Assert.That(sequence.IsActive, Is.True, "the mode keeps the controls until the presentation has finished");
            Assert.That(sequence.IsInteractive, Is.False);

            sequence.Tick(0.3f);
            Assert.That(sequence.ApproachAmount, Is.EqualTo(1f), "the panel goes back on before the PC leaves");
            Assert.That(sequence.CoverAmount, Is.LessThan(1f).And.GreaterThan(0f));

            Assert.That(sequence.Tick(Total), Is.EqualTo(PcBuildModePhase.Closed));
            Assert.That(sequence.ApproachAmount, Is.Zero);
            Assert.That(sequence.CoverAmount, Is.Zero);
            Assert.That(sequence.Progress, Is.Zero);
            Assert.That(sequence.End(), Is.False, "already closed");
        }

        [Test]
        public void ReversingHalfWayIsContinuous()
        {
            PcBuildModeSequence sequence = new(Approach, Cover, Overlap);
            sequence.Begin();
            sequence.Tick(0.3f);
            float approach = sequence.ApproachAmount;

            Assert.That(sequence.End(), Is.True);
            Assert.That(sequence.ApproachAmount, Is.EqualTo(approach), "no jump when turning round");

            sequence.Tick(0.1f);
            Assert.That(sequence.ApproachAmount, Is.EqualTo(approach - 0.1f / Approach).Within(1e-5f));

            Assert.That(sequence.Begin(), Is.True, "and back in again");
            sequence.Tick(Total);
            Assert.That(sequence.Phase, Is.EqualTo(PcBuildModePhase.Open));
        }

        [Test]
        public void ResetGoesStraightBackToClosed()
        {
            PcBuildModeSequence sequence = Opened();

            sequence.Reset();

            Assert.That(sequence.Phase, Is.EqualTo(PcBuildModePhase.Closed));
            Assert.That(sequence.ApproachAmount, Is.Zero);
            Assert.That(sequence.CoverAmount, Is.Zero);
            Assert.That(sequence.Progress, Is.Zero);
        }

        [Test]
        public void InvalidTimingsAreRejected()
        {
            Assert.That(() => new PcBuildModeSequence(0f, Cover, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PcBuildModeSequence(Approach, Cover, Approach), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PcBuildModeSequence(Approach, Cover, Overlap).Tick(-1f), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static PcBuildModeSequence Opened()
        {
            PcBuildModeSequence sequence = new(Approach, Cover, Overlap);
            sequence.Begin();
            sequence.Tick(Total);
            return sequence;
        }
    }
}
