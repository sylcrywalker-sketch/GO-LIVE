using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    // What one order row shows, ready to draw. Delivery is null once the order has arrived.
    public readonly struct ShopOrderRowData
    {
        public Sprite Image { get; }
        public string Name { get; }
        public string PaidPrice { get; }
        public string Status { get; }
        public string Delivery { get; }

        public ShopOrderRowData(Sprite image, string name, string paidPrice, string status, string delivery)
        {
            Image = image;
            Name = name;
            PaidPrice = paidPrice;
            Status = status;
            Delivery = delivery;
        }
    }

    // One order: [image] name / paid price / status / delivery time while it is on its way.
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

        public void Show(in ShopOrderRowData data)
        {
            _image.sprite = data.Image;
            _image.enabled = data.Image != null;

            _name.text = data.Name;
            _price.text = data.PaidPrice;
            _status.text = data.Status;

            bool hasDelivery = !string.IsNullOrEmpty(data.Delivery);
            _delivery.text = hasDelivery ? data.Delivery : string.Empty;
            _delivery.gameObject.SetActive(hasDelivery);
        }
    }
}
