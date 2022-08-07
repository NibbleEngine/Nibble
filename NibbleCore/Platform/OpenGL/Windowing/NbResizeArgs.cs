#if OPENGL
using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common;



namespace NbCore.Platform.Windowing
{
    public class NbResizeArgs
    {
        private ResizeEventArgs _resizeEventArgs;

        public NbResizeArgs(ResizeEventArgs args)
        {
            _resizeEventArgs = args;
        }

        //Props
        public int Width
        {
            get
            {
                return _resizeEventArgs.Width;
            }
        }

        public int Height
        {
            get
            {
                return _resizeEventArgs.Height;
            }
        }

    }
}

#endif

