using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Localization;
using GoLive.Phone;
using GoLive.Shop;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // The Phone Shop in the real GL scene: the real phone, Shop, wallet and page views, clicked the way the phone's
    // pointer clicks (a pointer click on the button). [+] and "add to cart" only fill the cart; money and orders change
    // only at checkout; nothing of the Shop shows or takes clicks outside the Shop screen; rows stay readable. Nothing
    // here saves.
    public sealed class PhoneShopPlayModeTests
    {
        private const string GameScene = "Assets/Game/Scenes/GL.unity";
        private const string GpuId = "budget-gpu";
        private const string MicrophoneId = "used-microphone";
        private const string BananaId = "banana";
        private const string MugId = "mug";

        // In ItemCategory order: Food, Electronics, Household.
        private static readonly string[] CategoryNamesRu = { "Еда", "Электроника", "Быт" };

        private SceneSetup[] _previousScenes;
        private PhoneBehaviour _phone;
        private PhoneShopView _shopView;
        private ShopBehaviour _shop;
        private Wallet _wallet;
        private LocalizationContext _localization;
        private ShopStorefrontView _storefront;
        private ShopProductDetailsView _details;
        private ShopCartView _cart;
        private ShopOrdersView _orders;
        private RectTransform _canvas;
        private Button _back;
        private int _balanceChanges;

        [OneTimeSetUp]
        public void OpenGameScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.isDirty && scene.rootCount > 0)
                    Assert.Ignore("Save your open scene before running the GL Phone Shop fixture; unsaved scene work will not be closed.");
            }

            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreEditorSceneSetup()
        {
            SaveTestWorld.RestoreScene(_previousScenes);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator StorefrontShowsEveryProductsPriceBroadCategoryAndImage()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return OpenShop();

            HashSet<string> seen = new();
            Button[] tabs = Tabs();

            for (int tab = tabs.Length - 1; tab >= 0; tab--)
            {
                Click(tabs[tab]);
                yield return PlayModeWait.Frames(1);

                foreach (ShopProductCardView card in Cards())
                {
                    Assert.That(_shop.TryGetProduct(card.ProductId, out ShopProductDefinition product), Is.True);
                    TMP_Text price = Field<TMP_Text>(card, "_price");

                    Assert.That(price.gameObject.activeInHierarchy, Is.True, $"{card.ProductId}: the price is always shown");
                    Assert.That(price.text, Is.EqualTo(ShopText.FormatMoney(product.PriceCents)), card.ProductId);
                    Assert.That(price.isTextTruncated, Is.False, $"{card.ProductId}: the price is never cut");
                    Assert.That(Field<TMP_Text>(card, "_category").text, Is.EqualTo(CategoryNamesRu[(int)product.Category]), $"{card.ProductId}: its broad category");
                    Assert.That(Field<Image>(card, "_image").sprite, Is.EqualTo(product.DisplayImage), $"{card.ProductId}: own image, else the delivered item's icon");
                    seen.Add(card.ProductId);
                }
            }

            Assert.That(seen, Is.EquivalentTo(_shop.Products.Select(product => product.ProductId)), "every product is on some tab");
            Assert.That(Cards().Select(card => Field<TMP_Text>(card, "_category").text).Distinct(), Is.SubsetOf(CategoryNamesRu), "only the three broad categories");
        }

        [UnityTest]
        public IEnumerator PlusOnlyPutsTheProductInTheCart()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return OpenShop();

            Click(AddButton(GpuId));
            Click(AddButton(BananaId));
            yield return PlayModeWait.Frames(1);

            Assert.That(_wallet.BalanceCents, Is.EqualTo(10000), "no money is taken");
            Assert.That(_balanceChanges, Is.Zero);
            Assert.That(_shop.Orders.Orders, Is.Empty, "no order is placed");
            Assert.That(_shop.CartItemCount, Is.EqualTo(2));
            Assert.That(Field<GameObject>(_storefront, "_cartBadge").activeSelf, Is.True);
            Assert.That(Field<TMP_Text>(_storefront, "_cartCount").text, Is.EqualTo("2"));
            Assert.That(Field<Button>(Card(GpuId), "_add").interactable, Is.False, "the one allowed GPU is in the cart");
        }

        [UnityTest]
        public IEnumerator ProductPageOnlyAddsToTheCart()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return OpenShop();

            Click(Field<Button>(Card(MicrophoneId), "_open"));
            yield return PlayModeWait.Frames(1);
            Assert.That(_shopView.TitleKey, Is.EqualTo(ShopText.DetailsTitleKey));

            Button add = Field<Button>(_details, "_addToCart");
            Assert.That(Field<TMP_Text>(_details, "_addToCartLabel").text, Is.EqualTo(Text(ShopText.AddToCartKey)));

            Click(add);
            yield return PlayModeWait.Frames(1);

            Assert.That(_wallet.BalanceCents, Is.EqualTo(10000));
            Assert.That(_shop.Orders.Orders, Is.Empty);
            Assert.That(_shop.GetCartQuantity(MicrophoneId), Is.EqualTo(1));
            Assert.That(add.interactable, Is.False, "one per customer, and it is in the cart");
            Assert.That(Field<TMP_Text>(_details, "_addToCartLabel").text, Is.EqualTo(Text(ShopText.InCartKey)));
        }

        [UnityTest]
        public IEnumerator CheckoutChargesOnceAndShowsTheNewOrders()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return OpenShop();

            Click(AddButton(GpuId));
            Click(AddButton(BananaId));
            Click(AddButton(BananaId));
            yield return OpenCartPage();

            Assert.That(CartRows(), Has.Length.EqualTo(2));
            Assert.That(Field<TMP_Text>(_cart, "_total").text, Is.EqualTo(_localization.Format(ShopText.CartTotalKey, "$16.20")));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(10000), "nothing is paid before checkout");

            Click(Field<Button>(_cart, "_checkout"));
            yield return PlayModeWait.Frames(1);

            Assert.That(_wallet.BalanceCents, Is.EqualTo(10000 - 1620));
            Assert.That(_balanceChanges, Is.EqualTo(1), "one charge for the whole cart");
            Assert.That(_shop.Orders.Orders.Select(order => order.ProductId), Is.EqualTo(new[] { GpuId, BananaId, BananaId }));
            Assert.That(_shop.CartLines, Is.Empty);
            Assert.That(_shopView.TitleKey, Is.EqualTo(ShopText.OrdersTitleKey), "the new orders are shown");
            Assert.That(OrderRows(), Has.Length.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator CheckoutWithoutEnoughMoneyChangesNothing()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            _wallet.Restore(1000);
            _balanceChanges = 0;
            yield return OpenShop();

            Click(AddButton(GpuId));
            yield return OpenCartPage();

            Button checkout = Field<Button>(_cart, "_checkout");
            Assert.That(checkout.interactable, Is.False);
            Assert.That(Field<TMP_Text>(_cart, "_problem").text, Is.EqualTo(Text(ShopText.NotEnoughMoneyKey)));

            Click(checkout);
            checkout.onClick.Invoke(); // past the disabled button, straight to the coordinator
            yield return PlayModeWait.Frames(1);

            Assert.That(_wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(_balanceChanges, Is.Zero);
            Assert.That(_shop.Orders.Orders, Is.Empty);
            Assert.That(_shop.GetCartQuantity(GpuId), Is.EqualTo(1), "the cart is kept");
            Assert.That(_shopView.TitleKey, Is.EqualTo(ShopText.CartTitleKey));
        }

        [UnityTest]
        public IEnumerator ShopControlsNeverShowOutsideTheShop()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            Assert.That(ShopControls().Any(control => control.activeInHierarchy), Is.False, "nothing of the Shop before it is opened");

            yield return OpenShop();
            Assert.That(Field<Button>(_storefront, "_openCart").gameObject.activeInHierarchy, Is.True);
            Assert.That(Field<Button>(_storefront, "_openOrders").gameObject.activeInHierarchy, Is.True);

            Click(AddButton(BananaId));
            yield return OpenCartPage();
            yield return GoHome();

            Assert.That(_phone.CurrentScreen, Is.EqualTo(PhoneScreenId.Home));
            Assert.That(ShopControls().Where(control => control.activeInHierarchy).Select(control => control.name), Is.Empty, "no Shop page or shortcut on Home");

            Assert.That(_phone.TryOpenScreen(PhoneScreenId.Messages), Is.True);
            yield return PlayModeWait.Frames(1);
            Assert.That(ShopControls().Where(control => control.activeInHierarchy).Select(control => control.name), Is.Empty, "nor on Messages");
        }

        [UnityTest]
        public IEnumerator HomeTakesNoClicksMeantForTheShop()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return OpenShop();

            Camera eventCamera = _canvas.GetComponent<Canvas>().worldCamera;
            Vector2[] shortcuts =
            {
                RectTransformUtility.WorldToScreenPoint(eventCamera, Field<Button>(_storefront, "_openCart").transform.position),
                RectTransformUtility.WorldToScreenPoint(eventCamera, Field<Button>(_storefront, "_openOrders").transform.position)
            };

            Assert.That(shortcuts.All(point => HitsUnderShop(point)), Is.True, "in the Shop the shortcuts are hit");

            yield return GoHome();

            foreach (Vector2 point in shortcuts)
                Assert.That(HitsUnderShop(point), Is.False, "on Home nothing of the Shop is there to be clicked");
        }

        [UnityTest]
        public IEnumerator ReenteringTheShopStartsAtTheStorefront()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return OpenShop();

            yield return OpenCartPage();
            yield return GoHome();
            Assert.That(_phone.TryOpenScreen(PhoneScreenId.Shop), Is.True);
            yield return PlayModeWait.Frames(2);

            Assert.That(_shopView.TitleKey, Is.EqualTo(ShopText.ShopTitleKey));
            Assert.That(_storefront.gameObject.activeInHierarchy, Is.True);
            Assert.That(_cart.gameObject.activeInHierarchy || _details.gameObject.activeInHierarchy || _orders.gameObject.activeInHierarchy, Is.False);

            // Closing the phone on a Shop page also starts the next visit at the storefront.
            yield return OpenCartPage();
            Assert.That(_phone.RequestClose(), Is.True);
            yield return PlayModeWait.Until(() => _phone.PresentationState == PhonePresentationState.Hidden, "the phone to be put away");
            yield return OpenShop();

            Assert.That(_shopView.TitleKey, Is.EqualTo(ShopText.ShopTitleKey));
            Assert.That(_storefront.gameObject.activeInHierarchy && !_cart.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator CartAndOrderRowsAreReadableInRussian()
        {
            yield return RowsAreReadable(GameLanguage.Russian);
        }

        [UnityTest]
        public IEnumerator CartAndOrderRowsAreReadableInEnglish()
        {
            yield return RowsAreReadable(GameLanguage.English);
        }

        private IEnumerator RowsAreReadable(GameLanguage language)
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            _localization.SetLanguage(language);
            yield return OpenShop();

            // One line.
            Click(AddButton(MicrophoneId));
            yield return OpenCartPage();
            AssertRowsReadable(CartRows(), 1, "one cart line");
            AssertFooterClearOfBack();

            // Enough lines to scroll, the longest names included.
            string[] more = { GpuId, BananaId, MugId, "used-keyboard", "used-motherboard", "used-psu", "router" };

            foreach (string productId in more)
                Assert.That(_shop.TryAddToCart(productId), Is.EqualTo(ShopPurchaseResultCode.Success), productId);

            Assert.That(_shop.TryAddToCart(BananaId), Is.EqualTo(ShopPurchaseResultCode.Success));
            yield return PlayModeWait.Frames(1);

            ShopCartRowView[] rows = CartRows();
            AssertRowsReadable(rows, 8, "eight cart lines");
            AssertScrollable(Field<ScrollRect>(_cart, "_list"), rows.Length);
            AssertFooterClearOfBack();

            Click(Field<Button>(_cart, "_checkout"));
            yield return PlayModeWait.Frames(1);
            Assert.That(_shopView.TitleKey, Is.EqualTo(ShopText.OrdersTitleKey));

            ShopOrderRowView[] orders = OrderRows();
            AssertRowsReadable(orders, 9, "nine orders on their way");
            AssertScrollable(Field<ScrollRect>(_orders, "_list"), orders.Length);

            // Delivered orders drop their delivery line and stay readable.
            Object.FindAnyObjectByType<GameClockBehaviour>().Clock.AdvanceMinutes(151);
            yield return PlayModeWait.Frames(2);
            AssertRowsReadable(OrderRows(), 9, "delivered orders");
            Assert.That(OrderRows().Select(row => Field<TMP_Text>(row, "_delivery").gameObject.activeSelf), Has.All.False);
        }

        // ---------- helpers ----------

        private IEnumerator Boot()
        {
            yield return PlayModeWait.Frames(10);

            _phone = Object.FindAnyObjectByType<PhoneBehaviour>(FindObjectsInactive.Include);
            _shopView = Object.FindAnyObjectByType<PhoneShopView>(FindObjectsInactive.Include);
            _shop = Object.FindAnyObjectByType<ShopBehaviour>();
            _wallet = Object.FindAnyObjectByType<WalletBehaviour>().Wallet;
            _localization = Object.FindAnyObjectByType<LocalizationContext>();
            _storefront = Field<ShopStorefrontView>(_shopView, "_storefront");
            _details = Field<ShopProductDetailsView>(_shopView, "_details");
            _cart = Field<ShopCartView>(_shopView, "_cart");
            _orders = Field<ShopOrdersView>(_shopView, "_orders");
            _canvas = (RectTransform)Field<Canvas>(_phone, "phoneCanvas").transform;
            _back = Field<Button>(Object.FindAnyObjectByType<PhoneScreenView>(FindObjectsInactive.Include), "_back");

            Assert.That(_phone.isActiveAndEnabled && _shop.IsReady, Is.True, "the phone and the Shop are running");
            Assert.That(_storefront.IsConfigured && _details.IsConfigured && _cart.IsConfigured && _orders.IsConfigured, Is.True, "GL wires every Shop page");

            _localization.SetLanguage(GameLanguage.Russian);
            _wallet.Restore(10000);
            _balanceChanges = 0;
            _wallet.BalanceChanged += _ => _balanceChanges++;
        }

        private IEnumerator OpenShop()
        {
            Assert.That(_phone.Open(), Is.True);
            yield return PlayModeWait.Until(() => _phone.IsInteractive, "the phone to be held");
            Assert.That(_phone.TryOpenScreen(PhoneScreenId.Shop), Is.True);
            yield return PlayModeWait.Frames(2);
            Assert.That(_shopView.isActiveAndEnabled && _storefront.gameObject.activeInHierarchy, Is.True, "the storefront is shown");
        }

        private IEnumerator OpenCartPage()
        {
            Click(Field<Button>(_storefront, "_openCart"));
            yield return PlayModeWait.Frames(2);
            Assert.That(_shopView.TitleKey, Is.EqualTo(ShopText.CartTitleKey));
        }

        private IEnumerator GoHome()
        {
            for (int i = 0; i < 4 && _phone.CurrentScreen != PhoneScreenId.Home; i++)
            {
                Assert.That(_phone.HandleBack(), Is.True);
                yield return PlayModeWait.Frames(1);
            }

            Assert.That(_phone.CurrentScreen, Is.EqualTo(PhoneScreenId.Home));
            yield return PlayModeWait.Frames(1);
        }

        private Button[] Tabs()
        {
            IEnumerable tabs = Field<IEnumerable>(_storefront, "_tabs");
            return tabs.Cast<object>().Select(tab => (Button)tab.GetType().GetProperty("Button").GetValue(tab)).ToArray();
        }

        private ShopProductCardView[] Cards()
        {
            return Field<ScrollRect>(_storefront, "_catalog").content.GetComponentsInChildren<ShopProductCardView>(false);
        }

        private ShopProductCardView Card(string productId)
        {
            return Cards().Single(card => card.ProductId == productId);
        }

        private Button AddButton(string productId)
        {
            return Field<Button>(Card(productId), "_add");
        }

        private ShopCartRowView[] CartRows()
        {
            return Field<ScrollRect>(_cart, "_list").content.GetComponentsInChildren<ShopCartRowView>(false);
        }

        private ShopOrderRowView[] OrderRows()
        {
            return Field<ScrollRect>(_orders, "_list").content.GetComponentsInChildren<ShopOrderRowView>(false);
        }

        // Every Shop page and the shortcuts, which must never be shown outside the Shop screen.
        private IEnumerable<GameObject> ShopControls()
        {
            yield return _storefront.gameObject;
            yield return _details.gameObject;
            yield return _cart.gameObject;
            yield return _orders.gameObject;
            yield return Field<Button>(_storefront, "_openCart").gameObject;
            yield return Field<Button>(_storefront, "_openOrders").gameObject;
            yield return Field<Button>(_cart, "_checkout").gameObject;
        }

        private bool HitsUnderShop(Vector2 screenPoint)
        {
            List<RaycastResult> results = new();
            _canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) { position = screenPoint }, results);
            return results.Any(result => result.gameObject.transform.IsChildOf(_shopView.transform));
        }

        private string Text(string key)
        {
            return _localization.Text(key);
        }

        // Every shown text of a row has its own rectangle (no two overlap), fits it vertically on one line, and is either
        // whole or cut with an ellipsis; nothing sticks out of the row.
        private void AssertRowsReadable(Component[] rows, int expected, string what)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(rows, Has.Length.EqualTo(expected), what);

            foreach (Component row in rows)
            {
                Rect rowRect = CanvasRect((RectTransform)row.transform);
                TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(false).Where(text => !string.IsNullOrEmpty(text.text)).ToArray();

                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text text = texts[i];
                    Rect rect = CanvasRect(text.rectTransform);
                    string label = $"{what}: '{text.text}' ({text.name})";

                    Assert.That(rect.width > 0f && rect.height > 0f, Is.True, label);
                    Assert.That(Contains(rowRect, rect), Is.True, $"{label} stays inside its row");
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(rect.height + 0.5f), $"{label} fits its line");
                    Assert.That(text.GetPreferredValues(text.text).x <= rect.width + 0.5f || text.overflowMode == TextOverflowModes.Ellipsis, Is.True, $"{label} fits or ends with an ellipsis");

                    for (int j = i + 1; j < texts.Length; j++)
                    {
                        Rect other = CanvasRect(texts[j].rectTransform);
                        Assert.That(Shrink(rect).Overlaps(Shrink(other)), Is.False, $"{label} overlaps '{texts[j].text}' ({texts[j].name})");
                    }
                }
            }

            for (int i = 1; i < rows.Length; i++)
                Assert.That(Shrink(CanvasRect((RectTransform)rows[i - 1].transform)).Overlaps(Shrink(CanvasRect((RectTransform)rows[i].transform))), Is.False, $"{what}: rows {i - 1} and {i} overlap");
        }

        private void AssertScrollable(ScrollRect list, int rows)
        {
            Canvas.ForceUpdateCanvases();
            float content = list.content.rect.height;
            float viewport = list.viewport.rect.height;
            Rect last = CanvasRect((RectTransform)list.content.GetChild(list.content.childCount - 1));
            Rect bottom = CanvasRect(list.content);

            Assert.That(content, Is.GreaterThan(viewport), $"{rows} rows are more than one screen and scroll");
            Assert.That(last.yMin, Is.GreaterThanOrEqualTo(bottom.yMin - 0.5f), "the content is sized to hold its last row");
        }

        private void AssertFooterClearOfBack()
        {
            Canvas.ForceUpdateCanvases();
            Rect checkout = CanvasRect((RectTransform)Field<Button>(_cart, "_checkout").transform);
            Rect back = CanvasRect((RectTransform)_back.transform);
            Rect list = CanvasRect((RectTransform)Field<ScrollRect>(_cart, "_list").transform);

            Assert.That(_back.gameObject.activeInHierarchy && Field<Button>(_cart, "_checkout").gameObject.activeInHierarchy, Is.True);
            Assert.That(Shrink(checkout).Overlaps(Shrink(back)), Is.False, "the checkout button is clear of the shared Back button");
            Assert.That(checkout.yMin, Is.GreaterThan(back.yMax), "and sits above it");
            Assert.That(list.yMin, Is.GreaterThan(checkout.yMax), "the list ends above the checkout row");
        }

        private Rect CanvasRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = _canvas.InverseTransformPoint(corners[0]);
            Vector3 max = _canvas.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;
        }

        private static Rect Shrink(Rect rect)
        {
            return Rect.MinMaxRect(rect.xMin + 0.5f, rect.yMin + 0.5f, rect.xMax - 0.5f, rect.yMax - 0.5f);
        }

        private static void Click(Button button)
        {
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }

        private static T Field<T>(object owner, string name)
        {
            return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        }
    }
}
