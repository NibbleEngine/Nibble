#if OPENGL
using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common;



namespace NbCore.Platform.Windowing
{
    public class NbMouseMoveArgs
    {
        private MouseMoveEventArgs _mouseMoveEventArgs;

        public NbMouseMoveArgs(MouseMoveEventArgs args)
        {
            _mouseMoveEventArgs = args;
        }

        //Props
        public float X
        {
            get
            {
                return _mouseMoveEventArgs.X;
            } 
        }

        public float Y
        {
            get
            {
                return _mouseMoveEventArgs.Y;
            }
        }



    }
}

#endif
