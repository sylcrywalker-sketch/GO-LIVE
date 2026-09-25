using UnityEngine;
using UnityEngine.EventSystems;

namespace GoLive.Desktop
{
    public sealed class DesktopWindowFocus : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private DesktopRuntimeBehaviour runtime;
        [SerializeField] private DesktopAppId appId;
        public void OnPointerDown(PointerEventData eventData) => runtime.State.Windows.Activate(appId);
    }
}
