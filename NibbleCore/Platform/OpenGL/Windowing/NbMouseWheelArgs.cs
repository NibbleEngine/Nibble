#if OPENGL
using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common;



namespace NbCore.Platform.Windowing
{
    public class NbMouseWheelArgs
    {
        private MouseWheelEventArgs _mouseMoveEventArgs;

        public NbMouseWheelArgs(MouseWheelEventArgs args)
        {
            _mouseMoveEventArgs = args;
        }

        //Props
        public float OffsetX
        {
            get
            {
                return _mouseMoveEventArgs.OffsetX;
            }
        }

        public float OffsetY
        {
            get
            {
                return _mouseMoveEventArgs.OffsetY;
            }
        }

    }
}

#endif
