using NbCore;

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
        Minus,
        Equal,
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
        CapsLock,
        NumLock,
        ScrlLock,
        PrintScreen,
        F1,F2,F3,F4,F5,F6,F7,F8,F9,F10,F11,F12,
        Period, Comma, Apostrophe, LBracket, RBracket
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
