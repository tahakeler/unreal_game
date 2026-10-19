using System;
using System.Collections.Generic;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Navigation stack for menu screens: Push opens on top, Pop returns, Replace swaps the top, ClearTo resets.
    /// Pure C# so the flow is unit-testable. <see cref="Changed"/> reports (from, to) with default(T) for "none".
    /// </summary>
    public sealed class ScreenStack<T> where T : struct, Enum
    {
        readonly List<T> _stack = new List<T>();

        public event Action<T?, T?> Changed;

        public int Depth => _stack.Count;
        public bool HasCurrent => _stack.Count > 0;
        public T Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : default;
        public bool CanPop => _stack.Count > 1;

        public void Push(T screen)
        {
            T? from = HasCurrent ? Current : (T?)null;
            _stack.Add(screen);
            Changed?.Invoke(from, screen);
        }

        /// <summary>Returns to the previous screen. False (and no change) when already at the root.</summary>
        public bool Pop()
        {
            if (!CanPop) return false;
            T from = Current;
            _stack.RemoveAt(_stack.Count - 1);
            Changed?.Invoke(from, Current);
            return true;
        }

        public void Replace(T screen)
        {
            T? from = HasCurrent ? Current : (T?)null;
            if (HasCurrent) _stack[_stack.Count - 1] = screen; else _stack.Add(screen);
            Changed?.Invoke(from, screen);
        }

        public void ClearTo(T screen)
        {
            T? from = HasCurrent ? Current : (T?)null;
            _stack.Clear();
            _stack.Add(screen);
            Changed?.Invoke(from, screen);
        }

        public bool Contains(T screen) => _stack.Contains(screen);
    }

    public enum MenuScreen { Title, Play, Host, Join, Settings }
}
