using UnityEngine;

namespace GoLive.PcBuilding
{
    // The player's own camera during PC Build Mode: it stays where the player's eye is and only settles its aim on the
    // build view (the player's heading, the build view's slight downward look) while the PC is brought over. Position,
    // parent, field of view and rendering are never touched; at zero it returns to exactly the local rotation it had.
    internal sealed class PcBuildCamera
    {
        private readonly Camera _camera;

        private Quaternion _homeLocalRotation;
        private bool _driving;

        public PcBuildCamera(Camera camera)
        {
            _camera = camera;
        }

        public void Apply(float amount, Quaternion buildRotation)
        {
            if (amount <= 0f)
            {
                Release();
                return;
            }

            Transform view = _camera.transform;

            if (!_driving)
            {
                _homeLocalRotation = view.localRotation;
                _driving = true;
            }

            Quaternion home = view.parent != null ? view.parent.rotation * _homeLocalRotation : _homeLocalRotation;
            view.rotation = amount >= 1f ? buildRotation : Quaternion.Slerp(home, buildRotation, Mathf.SmoothStep(0f, 1f, amount));
        }

        public void Release()
        {
            if (!_driving)
                return;

            _driving = false;

            if (_camera != null)
                _camera.transform.localRotation = _homeLocalRotation;
        }
    }
}
