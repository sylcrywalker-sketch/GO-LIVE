using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    [DisallowMultipleComponent]
    public sealed class ShopOrderRowView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private TMP_Text _delivery;

        public bool IsConfigured =>
            _image != null &&
            _name != null &&
            _price != null &&
            _status != null &&
            _delivery != null;

        public void Show(Sprite image, string productName, string price, string status, string delivery)
        {
            _image.sprite = image;
            _image.gameObject.SetActive(image != null);

            _name.text = productName;
            _price.text = price;
            _status.text = status;

            bool hasDelivery = !string.IsNullOrEmpty(delivery);

            _delivery.text = hasDelivery ? delivery : string.Empty;
            _delivery.gameObject.SetActive(hasDelivery);
        }
    }
}
