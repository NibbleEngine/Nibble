#if OPENGL
using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;


namespace NbCore.Platform.Windowing
{
    public struct NbKeyArgs
    {
        public static readonly Dictionary<Keys, NbKey> OpenTKKeyMap = new()
        {
            { Keys.A, NbKey.A },
            { Keys.B, NbKey.B },
            { Keys.C, NbKey.C },
            { Keys.D, NbKey.D },
            { Keys.E, NbKey.E },
            { Keys.F, NbKey.F },
            { Keys.G, NbKey.G },
            { Keys.H, NbKey.H },
            { Keys.I, NbKey.I },
            { Keys.J, NbKey.J },
            { Keys.K, NbKey.K },
            { Keys.L, NbKey.L },
            { Keys.M, NbKey.M },
            { Keys.N, NbKey.N },
            { Keys.O, NbKey.O },
            { Keys.P, NbKey.P },
            { Keys.Q, NbKey.Q },
            { Keys.R, NbKey.R },
            { Keys.S, NbKey.S },
            { Keys.T, NbKey.T },
            { Keys.U, NbKey.U },
            { Keys.V, NbKey.V },
            { Keys.W, NbKey.W },
            { Keys.X, NbKey.X },
            { Keys.Y, NbKey.Y },
            { Keys.Z, NbKey.Z },
            { Keys.Left, NbKey.LeftArrow },
            { Keys.Right, NbKey.RightArrow },
            { Keys.Up, NbKey.UpArrow },
            { Keys.Down, NbKey.DownArrow },
            { Keys.LeftAlt, NbKey.LeftAlt },
            { Keys.RightAlt, NbKey.RightAlt },
            { Keys.LeftControl, NbKey.LeftCtrl },
            { Keys.RightControl, NbKey.RightCtrl },
            { Keys.LeftSuper, NbKey.LeftSuper },
            { Keys.RightSuper, NbKey.RightSuper },
            { Keys.Backspace, NbKey.Backspace },
            { Keys.Space, NbKey.Space },
            { Keys.Home, NbKey.Home },
            { Keys.End, NbKey.End },
            { Keys.Insert, NbKey.Insert },
            { Keys.Delete, NbKey.Delete },
            { Keys.PageUp, NbKey.PageUp },
            { Keys.PageDown, NbKey.PageDown },
            { Keys.Enter, NbKey.Enter },
            { Keys.Escape, NbKey.Escape },
            { Keys.KeyPadEnter, NbKey.KeyPadEnter },
        };

        public static readonly List<NbKey> SupportedKeys = new()
        {
            NbKey.A,
            NbKey.B,
            NbKey.C,
            NbKey.D,
            NbKey.E,
            NbKey.F,
            NbKey.G,
            NbKey.H,
            NbKey.I,
            NbKey.J,
            NbKey.K,
            NbKey.L,
            NbKey.M,
            NbKey.N,
            NbKey.O,
            NbKey.P,
            NbKey.Q,
            NbKey.R,
            NbKey.S,
            NbKey.T,
            NbKey.U,
            NbKey.V,
            NbKey.W,
            NbKey.X,
            NbKey.Y,
            NbKey.Z,
            NbKey.LeftArrow,
            NbKey.RightArrow,
            NbKey.UpArrow,
            NbKey.DownArrow,
            NbKey.LeftAlt,
            NbKey.RightAlt,
            NbKey.LeftCtrl,
            NbKey.RightCtrl,
            NbKey.LeftSuper,
            NbKey.RightSuper,
            NbKey.Backspace,
            NbKey.Space,
            NbKey.Home,
            NbKey.End,
            NbKey.Insert,
            NbKey.Delete,
            NbKey.PageUp,
            NbKey.PageDown,
            NbKey.Enter,
            NbKey.Escape,
            NbKey.KeyPadEnter
        };

        private KeyboardKeyEventArgs _keyboardKeyEventArgs;

        public NbKeyArgs(KeyboardKeyEventArgs args)
        {
            _keyboardKeyEventArgs = args;
        }

        public NbKey Key
        {
            get
            {
                return OpenTKKeyMap[_keyboardKeyEventArgs.Key];
            }
        }

    }


}

#endif
