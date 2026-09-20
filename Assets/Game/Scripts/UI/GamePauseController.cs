using System;
using GoLive.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace GoLive.UI
{
    [DisallowMultipleComponent]
    public sealed class GamePauseController : MonoBehaviour
    {
        [FormerlySerializedAs("playerController")]
        [SerializeField] private PlayerController _playerController;

        [FormerlySerializedAs("pauseOverlay")]
        [SerializeField] private CanvasGroup _pauseOverlay;

        public bool IsPaused { get; private set; }

        private IDisposable _controlBlock;
        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;
        private float _previousTimeScale;

        private void Awake()
        {
            if (_playerController != null)
            {
                SetOverlayVisible(false);
                return;
            }

            Debug.LogError($"{nameof(GamePauseController)} on {name} requires a Player Controller.", this);
            enabled = false;
        }

        private void OnDisable()
        {
            Close();
        }

        public void Open()
        {
            if (!isActiveAndEnabled || IsPaused)
                return;

            IsPaused = true;

            _previousTimeScale = UnityEngine.Time.timeScale;
            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            _controlBlock = _playerController.Controls.Block(PlayerControlMask.All);

            UnityEngine.Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SetOverlayVisible(true);
        }

        public void Close()
        {
            if (!IsPaused)
                return;

            IsPaused = false;

            UnityEngine.Time.timeScale = _previousTimeScale;

            _controlBlock?.Dispose();
            _controlBlock = null;

            Cursor.lockState = _previousCursorLockMode;
            Cursor.visible = _previousCursorVisible;

            SetOverlayVisible(false);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_pauseOverlay == null)
                return;

            _pauseOverlay.gameObject.SetActive(visible);
            _pauseOverlay.alpha = visible ? 1f : 0f;
            _pauseOverlay.interactable = visible;
            _pauseOverlay.blocksRaycasts = visible;
        }
    }
}