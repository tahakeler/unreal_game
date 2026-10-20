using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>Focus and cursor helpers shared by all screens.</summary>
    public static class UiFocus
    {
        /// <summary>Number of open overlays that need a free cursor / own the Escape key.</summary>
        public static int ModalCount => HarvestPanic.Core.InputModal.Count;
        public static void Push(object owner) => HarvestPanic.Core.InputModal.Push(owner);
        public static void Pop(object owner) => HarvestPanic.Core.InputModal.Pop(owner);

        public static void FocusLater(VisualElement el)
        {
            if (el == null) return;
            el.schedule.Execute(() => el.Focus()).StartingIn(40);
        }

        public static void FocusFirstButton(VisualElement container)
        {
            if (container == null) return;
            var b = container.Q<Button>(className: "hp-btn");
            FocusLater(b);
        }
    }
}
