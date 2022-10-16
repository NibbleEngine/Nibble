#if OPENGL
using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace NbCore.Platform.Windowing
{
    public class NbMouseButtonArgs
    {
        public static readonly Dictionary<MouseButton, NbMouseButton> OpenTKButtonMap = new()
        {
            { MouseButton.Left, NbMouseButton.LEFT},
            { MouseButton.Right, NbMouseButton.RIGHT},
            { MouseButton.Middle, NbMouseButton.MIDDLE},
        };

        private MouseButtonEventArgs _mouseButtonEventArgs;


        public NbMouseButtonArgs (MouseButtonEventArgs args)
        {
            _mouseButtonEventArgs = args;
        }

        public bool IsPressed
        {
            get
            {
                return _mouseButtonEventArgs.IsPressed;
            }
        }

        public NbMouseButton Button
        {
            get
            {
                return OpenTKButtonMap[_mouseButtonEventArgs.Button];
            }
        }

    }
}

#endif