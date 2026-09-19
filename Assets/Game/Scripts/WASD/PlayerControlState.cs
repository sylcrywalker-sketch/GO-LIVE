using System;
using System.Collections.Generic;

namespace GoLive.Player
{
    [Flags]
    public enum PlayerControlMask
    {
        None = 0,
        Movement = 1 << 0,
        Look = 1 << 1,
        Jump = 1 << 2,
        Interaction = 1 << 3,
        All = Movement | Look | Jump | Interaction
    }

    public sealed class PlayerControlState
    {
        private readonly Dictionary<int, PlayerControlMask> _blocks = new();

        private int _nextBlockId = 1;
        private PlayerControlMask _blocked;

        public bool IsAllowed(PlayerControlMask mask) => (_blocked & mask) == 0;

        public IDisposable Block(PlayerControlMask mask)
        {
            if (mask == PlayerControlMask.None)
                throw new ArgumentOutOfRangeException(nameof(mask));

            int id = _nextBlockId++;

            _blocks.Add(id, mask);
            _blocked |= mask;

            return new BlockHandle(this, id);
        }

        private void Release(int id)
        {
            if (!_blocks.Remove(id))
                return;

            _blocked = PlayerControlMask.None;

            foreach (PlayerControlMask mask in _blocks.Values)
                _blocked |= mask;
        }

        private sealed class BlockHandle : IDisposable
        {
            private PlayerControlState _owner;
            private readonly int _id;

            public BlockHandle(PlayerControlState owner, int id)
            {
                _owner = owner;
                _id = id;
            }

            public void Dispose()
            {
                if (_owner is null)
                    return;

                _owner.Release(_id);
                _owner = null;
            }
        }
    }
}