using UnityEngine;

namespace GoLive.Phone
{
    [DisallowMultipleComponent]
    public sealed class PhoneMessagesBehaviour : MonoBehaviour
    {
        public PhoneMessages Messages { get; } = new();
    }
}
