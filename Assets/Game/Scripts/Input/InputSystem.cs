using System;
using July.Arch;
using July.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameTemplate
{
    public class InputSystem : SystemBase, IInputSystem, IInputGate
    {
        private int _blockCount;

        public bool IsBlocked => _blockCount > 0;
        public void Block() => _blockCount++;
        public void Unblock() => _blockCount = Math.Max(0, _blockCount - 1);

        public bool ShouldBlockInput(int fingerId = -1)
        {
            if (_blockCount > 0) return true;
            return IsPointerOverGameObject(fingerId);
        }

        public bool GetPointerDown(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            if (_blockCount > 0) return false;
            if (!GetRawPointerDown(out screenPos)) return false;
            if (IsPointerOverGameObject(GetCurrentFingerId()))
            {
                screenPos = Vector2.zero;
                return false;
            }
            return true;
        }

        public bool GetPointerHeld(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            if (_blockCount > 0) return false;
            return GetRawPointerHeld(out screenPos);
        }

        public bool GetPointerUp(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            if (_blockCount > 0) return false;
            return GetRawPointerUp(out screenPos);
        }

        public Vector2 PointerScreenPosition
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                return Input.mousePosition;
#else
                return Input.touchCount > 0 ? Input.GetTouch(0).position : Vector2.zero;
#endif
            }
        }

        public int TouchCount => _blockCount > 0 ? 0 : Input.touchCount;

        public bool TryGetTouch(int index, out Touch touch)
        {
            if (_blockCount > 0) { touch = default; return false; }
            if (index >= 0 && index < Input.touchCount)
            {
                touch = Input.GetTouch(index);
                return true;
            }
            touch = default;
            return false;
        }

        private bool GetRawPointerDown(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!Input.GetMouseButtonDown(0)) return false;
            screenPos = Input.mousePosition;
            return true;
#else
            if (Input.touchCount <= 0) return false;
            var touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began) return false;
            screenPos = touch.position;
            return true;
#endif
        }

        private bool GetRawPointerHeld(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!Input.GetMouseButton(0)) return false;
            screenPos = Input.mousePosition;
            return true;
#else
            if (Input.touchCount <= 0) return false;
            var touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Moved && touch.phase != TouchPhase.Stationary) return false;
            screenPos = touch.position;
            return true;
#endif
        }

        private bool GetRawPointerUp(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!Input.GetMouseButtonUp(0)) return false;
            screenPos = Input.mousePosition;
            return true;
#else
            if (Input.touchCount <= 0) return false;
            var touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) return false;
            screenPos = touch.position;
            return true;
#endif
        }

        private bool IsPointerOverGameObject(int fingerId = -1)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return fingerId >= 0 ? es.IsPointerOverGameObject(fingerId) : es.IsPointerOverGameObject();
        }

        private int GetCurrentFingerId()
        {
            return Input.touchCount > 0 ? Input.GetTouch(0).fingerId : -1;
        }
    }
}
