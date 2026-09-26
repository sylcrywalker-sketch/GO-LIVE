using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GoLive.Tests
{
    // Test-only: reads the frame the game displays, at the real end of a game frame. EditMode [UnityTest] steps are
    // resumed from EditorApplication.update, where the current render target is the Game view window's own GUI back
    // buffer (GUIViewHDRRT, sized like the Editor window, e.g. 1920x275 in batchmode), so a WaitForEndOfFrame yielded
    // by the test itself never reaches the game's frame. A coroutine on a scene object is resumed by the player loop
    // at the true end of frame, where CaptureScreenshotAsTexture returns the game's own frame at its render
    // resolution, independent of the Editor window's size or the display's DPI scaling.
    internal static class DisplayedFrameReader
    {
        internal static IEnumerator Read(Action<Texture2D> result)
        {
            // Scripts in this Editor test assembly cannot be attached to scene objects, so the scene's EventSystem
            // (a built-in runtime MonoBehaviour the desktop UI needs anyway) hosts the player-loop coroutine.
            EventSystem host = EventSystem.current;
            Assert.That(host != null && host.isActiveAndEnabled, Is.True, "an active EventSystem hosts the end-of-frame read");
            Texture2D frame = null;
            bool done = false;
            Coroutine routine = host.StartCoroutine(ReadAtEndOfFrame(read =>
            {
                frame = read;
                done = true;
            }));
            try
            {
                yield return PlayModeWait.Until(() => done, "an end-of-frame read of the displayed game frame");
            }
            finally
            {
                if (!done && host != null) host.StopCoroutine(routine);
            }
            result(frame);
        }

        private static IEnumerator ReadAtEndOfFrame(Action<Texture2D> done)
        {
            yield return new WaitForEndOfFrame();
            done(ScreenCapture.CaptureScreenshotAsTexture());
        }
    }
}
