#if OPENGL
using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common;



namespace NbCore.Platform.Windowing
{
    public class NbTextInputArgs
    {
        private TextInputEventArgs _textInputEventArgs;

        public NbTextInputArgs(TextInputEventArgs args)
        {
            _textInputEventArgs = args;
        }


        public int Unicode
        {
            get
            {
                return _textInputEventArgs.Unicode;
            }
        }

    }
}

#endif

