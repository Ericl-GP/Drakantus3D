using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Drakantus
{
    /// <summary>Teclas usadas pelo jogo (independe do sistema de input ativo no projeto).</summary>
    public enum K
    {
        W, A, S, D, Up, Down, Left, Right, Space, Shift, Q, E, F, I, Kk, Z, X, C, V,
        Alpha1, Alpha2, Escape, Enter, Tab, F10, T, M, R
    }

    /// <summary>
    /// Leitura de teclado/mouse que funciona tanto com o "Input System" novo
    /// quanto com o Input Manager antigo (o Unity define ENABLE_INPUT_SYSTEM /
    /// ENABLE_LEGACY_INPUT_MANAGER conforme Project Settings > Player > Active Input Handling).
    /// </summary>
    public static class InputW
    {
#if ENABLE_INPUT_SYSTEM
        static Key Map(K k)
        {
            switch (k)
            {
                case K.W: return Key.W;
                case K.A: return Key.A;
                case K.S: return Key.S;
                case K.D: return Key.D;
                case K.Up: return Key.UpArrow;
                case K.Down: return Key.DownArrow;
                case K.Left: return Key.LeftArrow;
                case K.Right: return Key.RightArrow;
                case K.Space: return Key.Space;
                case K.Shift: return Key.LeftShift;
                case K.Q: return Key.Q;
                case K.E: return Key.E;
                case K.F: return Key.F;
                case K.I: return Key.I;
                case K.Kk: return Key.K;
                case K.Z: return Key.Z;
                case K.X: return Key.X;
                case K.C: return Key.C;
                case K.V: return Key.V;
                case K.Alpha1: return Key.Digit1;
                case K.Alpha2: return Key.Digit2;
                case K.Escape: return Key.Escape;
                case K.Enter: return Key.Enter;
                case K.Tab: return Key.Tab;
                case K.F10: return Key.F10;
                case K.T: return Key.T;
                case K.M: return Key.M;
                case K.R: return Key.R;
            }
            return Key.None;
        }

        public static bool Held(K k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (k == K.Shift) return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            return kb[Map(k)].isPressed;
        }

        public static bool Down(K k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (k == K.Enter) return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            return kb[Map(k)].wasPressedThisFrame;
        }

        public static Vector2 MousePos => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool MouseHeld(int b)
        {
            var m = Mouse.current; if (m == null) return false;
            return b == 0 ? m.leftButton.isPressed : b == 1 ? m.rightButton.isPressed : m.middleButton.isPressed;
        }
        public static bool MouseDown(int b)
        {
            var m = Mouse.current; if (m == null) return false;
            return b == 0 ? m.leftButton.wasPressedThisFrame : b == 1 ? m.rightButton.wasPressedThisFrame : m.middleButton.wasPressedThisFrame;
        }
        public static float Scroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        static KeyCode Map(K k)
        {
            switch (k)
            {
                case K.W: return KeyCode.W;
                case K.A: return KeyCode.A;
                case K.S: return KeyCode.S;
                case K.D: return KeyCode.D;
                case K.Up: return KeyCode.UpArrow;
                case K.Down: return KeyCode.DownArrow;
                case K.Left: return KeyCode.LeftArrow;
                case K.Right: return KeyCode.RightArrow;
                case K.Space: return KeyCode.Space;
                case K.Shift: return KeyCode.LeftShift;
                case K.Q: return KeyCode.Q;
                case K.E: return KeyCode.E;
                case K.F: return KeyCode.F;
                case K.I: return KeyCode.I;
                case K.Kk: return KeyCode.K;
                case K.Z: return KeyCode.Z;
                case K.X: return KeyCode.X;
                case K.C: return KeyCode.C;
                case K.V: return KeyCode.V;
                case K.Alpha1: return KeyCode.Alpha1;
                case K.Alpha2: return KeyCode.Alpha2;
                case K.Escape: return KeyCode.Escape;
                case K.Enter: return KeyCode.Return;
                case K.Tab: return KeyCode.Tab;
                case K.F10: return KeyCode.F10;
                case K.T: return KeyCode.T;
                case K.M: return KeyCode.M;
                case K.R: return KeyCode.R;
            }
            return KeyCode.None;
        }

        public static bool Held(K k)
        {
            if (k == K.Shift) return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return Input.GetKey(Map(k));
        }
        public static bool Down(K k)
        {
            if (k == K.Enter) return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            return Input.GetKeyDown(Map(k));
        }
        public static Vector2 MousePos => Input.mousePosition;
        public static bool MouseHeld(int b) => Input.GetMouseButton(b);
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b);
        public static float Scroll => Input.mouseScrollDelta.y;
#endif

        /// <summary>Vetor de movimento (WASD/setas) no plano da tela.</summary>
        public static Vector2 Move()
        {
            Vector2 v = Vector2.zero;
            if (Held(K.A) || Held(K.Left)) v.x -= 1;
            if (Held(K.D) || Held(K.Right)) v.x += 1;
            if (Held(K.W) || Held(K.Up)) v.y += 1;
            if (Held(K.S) || Held(K.Down)) v.y -= 1;
            return v.sqrMagnitude > 1 ? v.normalized : v;
        }
    }
}
