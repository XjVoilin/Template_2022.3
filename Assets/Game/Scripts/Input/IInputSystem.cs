using UnityEngine;

namespace GameTemplate
{
    /// <summary>
    /// 输入系统接口 — 屏蔽管理、过滤后的指针/触摸查询。
    /// 通过 Scope.GetSystem&lt;IInputSystem&gt;() 获取。
    /// </summary>
    public interface IInputSystem
    {
        #region Block

        bool IsBlocked { get; }
        void Block();
        void Unblock();

        #endregion

        #region Pointer

        bool ShouldBlockInput(int fingerId = -1);
        bool GetPointerDown(out Vector2 screenPos);
        bool GetPointerHeld(out Vector2 screenPos);
        bool GetPointerUp(out Vector2 screenPos);
        Vector2 PointerScreenPosition { get; }

        #endregion

        #region Touch

        int TouchCount { get; }
        bool TryGetTouch(int index, out Touch touch);

        #endregion
    }
}
