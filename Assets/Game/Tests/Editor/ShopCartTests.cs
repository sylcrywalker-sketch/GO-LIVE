using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Shop;
using NUnit.Framework;

namespace GoLive.Tests
{
    // ShopCart without Unity: Product ID → quantity, one Changed per real mutation, nothing on a no-op.
    public sealed class ShopCartTests
    {
        private ShopCart _cart;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _cart = new ShopCart();
            _changes = 0;
            _cart.Changed += () => _changes++;
        }

        [Test]
        public void FirstAddCreatesALineOfOne()
        {
            Assert.That(_cart.TryAdd("banana"), Is.True);

            Assert.That(Snapshot(), Is.EqualTo(new[] { ("banana", 1) }));
            Assert.That(_cart.TotalQuantity, Is.EqualTo(1));
            Assert.That(_cart.IsEmpty, Is.False);
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void AddingAgainIncrementsTheSameLine()
        {
            _cart.TryAdd("banana");
            _cart.TryAdd("mug");
            _cart.TryAdd("banana");

            Assert.That(Snapshot(), Is.EqualTo(new[] { ("banana", 2), ("mug", 1) }), "first-added order is kept");
            Assert.That(_cart.GetQuantity("banana"), Is.EqualTo(2));
            Assert.That(_cart.TotalQuantity, Is.EqualTo(3));
            Assert.That(_changes, Is.EqualTo(3));
        }

        [Test]
        public void RemoveOneDecrementsAndDropsTheLineAtZero()
        {
            _cart.TryAdd("banana");
            _cart.TryAdd("banana");
            _changes = 0;

            Assert.That(_cart.TryRemoveOne("banana"), Is.True);
            Assert.That(_cart.GetQuantity("banana"), Is.EqualTo(1));

            Assert.That(_cart.TryRemoveOne("banana"), Is.True);
            Assert.That(_cart.GetQuantity("banana"), Is.Zero);
            Assert.That(_cart.Lines, Is.Empty);
            Assert.That(_cart.TotalQuantity, Is.Zero);
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void RemoveAllDropsTheWholeLine()
        {
            _cart.TryAdd("banana");
            _cart.TryAdd("banana");
            _cart.TryAdd("mug");
            _changes = 0;

            Assert.That(_cart.TryRemoveAll("banana"), Is.True);

            Assert.That(Snapshot(), Is.EqualTo(new[] { ("mug", 1) }));
            Assert.That(_cart.TotalQuantity, Is.EqualTo(1));
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void ClearEmptiesTheCartWithOneNotification()
        {
            _cart.TryAdd("banana");
            _cart.TryAdd("mug");
            _changes = 0;

            Assert.That(_cart.Clear(), Is.True);

            Assert.That(_cart.IsEmpty, Is.True);
            Assert.That(_cart.TotalQuantity, Is.Zero);
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void NoOpsChangeNothingAndStayQuiet()
        {
            Assert.That(_cart.TryRemoveOne("banana"), Is.False);
            Assert.That(_cart.TryRemoveAll("banana"), Is.False);
            Assert.That(_cart.Clear(), Is.False);

            _cart.TryAdd("mug");
            _changes = 0;

            Assert.That(_cart.TryRemoveOne("banana"), Is.False);
            Assert.That(_cart.TryRemoveAll("banana"), Is.False);
            Assert.That(Snapshot(), Is.EqualTo(new[] { ("mug", 1) }));
            Assert.That(_changes, Is.Zero);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Budget GPU")]
        public void MalformedProductIdIsRefused(string productId)
        {
            Assert.That(_cart.TryAdd(productId), Is.False);
            Assert.That(_cart.GetQuantity(productId), Is.Zero);
            Assert.That(_cart.IsEmpty, Is.True);
            Assert.That(_changes, Is.Zero);
        }

        [Test]
        public void LinesAreAReadOnlyProjection()
        {
            _cart.TryAdd("banana");
            IReadOnlyList<ShopCartLine> lines = _cart.Lines;

            Assert.That(lines, Is.InstanceOf<IList<ShopCartLine>>());
            Assert.That(((IList<ShopCartLine>)lines).IsReadOnly, Is.True);
            Assert.That(() => ((IList<ShopCartLine>)lines).Add(new ShopCartLine("mug", 1)), Throws.InstanceOf<NotSupportedException>());
            Assert.That(() => ((IList<ShopCartLine>)lines).Clear(), Throws.InstanceOf<NotSupportedException>());

            _cart.TryAdd("mug");
            Assert.That(lines, Has.Count.EqualTo(2), "a live view of the cart, not a copy");
        }

        private (string, int)[] Snapshot()
        {
            return _cart.Lines.Select(line => (line.ProductId, line.Quantity)).ToArray();
        }
    }
}
