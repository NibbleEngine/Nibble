using NbCore.Math;

namespace NbCore
{
    public enum NbKey
    {
        Numpad1,
        Numpad2,
        Numpad3,
        Numpad4,
        Numpad5,
        Numpad6,
        Numpad7,
        Numpad8,
        Numpad9,
        Numpad0,
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
        I,
        J,
        K,
        L,
        M,
        N,
        O,
        P,
        Q,
        R,
        S,
        T,
        U,
        V,
        W,
        X,
        Y,
        Z,
        Tab,
        LeftCtrl,
        RightCtrl,
        LeftShift,
        RightShift,
        LeftAlt,
        RightAlt,
        LeftSuper,
        RightSuper,
        LeftArrow,
        RightArrow,
        UpArrow,
        DownArrow,
        Home,
        End,
        Insert,
        Delete,
        PageUp,
        PageDown,
        Space,
        Backspace,
        Enter,
        Escape,
        KeyPad1,
        KeyPad2,
        KeyPad3,
        KeyPad4,
        KeyPad5,
        KeyPad6,
        KeyPad7,
        KeyPad8,
        KeyPad9,
        KeyPad0,
        KeyPadEnter,
        KeyPadPlus,
        KeyPadMinus,
        KeyPadSlash,
        KeyPadColon,
        KeyPadAsterisk,
    }

    public unsafe struct NbKeyboardState
    {
        public fixed bool KeyDown[80];
        public bool UpdateScene;

        public bool IsKeyDown(NbKey key)
        {
            return KeyDown[(int)key];
        }

        public void SetKeyDownStatus(NbKey button, bool state)
        {
            KeyDown[(int)button] = state;
        }
    }



}
