using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace GoLive.Tests
{
    // Waits for Edit Mode tests that enter Play Mode. The Test Runner steps such a test once per editor update, and an
    // editor update is not a frame of the game: when the Editor's player loop is paused or slowed down the test keeps
    // stepping while the game does not, and a test that counted steps would assert on a Start or Update that never ran.
    // These waits count the game's own frames (or wait for the condition itself), and a Play Mode that stops advancing
    // fails right here, saying so, instead of as a misleading assertion further on.
    internal static class PlayModeWait
    {
        private const float StallSeconds = 10f;

        // Every Start, Update and LateUpdate due, the physics steps and the deferred Destroy calls of that many frames.
        public static IEnumerator Frames(int count)
        {
            int target = Time.frameCount + count;
            float deadline = Time.realtimeSinceStartup + StallSeconds;

            while (Time.frameCount < target)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Play Mode ran {count - (target - Time.frameCount)} of {count} frames in {StallSeconds} s: the Editor's player loop is not advancing (is the Editor paused?).");

                yield return null;
            }
        }

        public static IEnumerator Until(Func<bool> condition, string what)
        {
            int startFrame = Time.frameCount;
            float deadline = Time.realtimeSinceStartup + StallSeconds;

            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail(Time.frameCount == startFrame
                        ? $"Waited {StallSeconds} s for {what} and the game ran no frame meanwhile: the Editor's player loop is not advancing (is the Editor paused?)."
                        : $"Waited {StallSeconds} s ({Time.frameCount - startFrame} frames) for {what}.");
                }

                yield return null;
            }
        }
    }
}
