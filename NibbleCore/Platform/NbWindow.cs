using NbCore.Platform.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Math;

namespace NbCore.Platform.Windowing
{
    public delegate void NbWindowOnRender(double dt);
    public delegate void NbWindowOnLoad();
    public delegate void NbWindowOnFrameUpdate(double dt);

    public delegate void NbKeyEventHandler(Windowing.NbKeyArgs args);
    public delegate void NbMouseButtonEventHandler(NbMouseButtonArgs args);
    public delegate void NbMouseMoveEventHandler(NbMouseMoveArgs args);
    public delegate void NbMouseWheelEventHandler(NbMouseWheelArgs args);
    public delegate void NbResizeEventHandler(NbResizeArgs args);

    public abstract class NbWindow
    {
        public NbWindowOnRender OnRenderUpdate;
        public NbWindowOnFrameUpdate OnFrameUpdate;
        public NbWindowOnLoad OnWindowLoad;

        public event NbKeyEventHandler OnKeyUp;
        public event NbKeyEventHandler OnKeyDown;
        public event NbMouseButtonEventHandler OnMouseButtonDown;
        public event NbMouseButtonEventHandler OnMouseButtonUp;
        public event NbMouseMoveEventHandler OnMouseMove;
        public event NbMouseWheelEventHandler OnMouseWheel;
        public event NbResizeEventHandler OnResize;
        
        public virtual NbVector2i Size { get; set; }

        public virtual NbVector2i ClientSize { get; }

        public void InvokeKeyUpEvent(Windowing.NbKeyArgs args)
        {
            OnKeyUp?.Invoke(args);
        }

        public void InvokeKeyDownEvent(Windowing.NbKeyArgs args)
        {
            OnKeyDown?.Invoke(args);
        }

        public void InvokeMouseButtonDownEvent(NbMouseButtonArgs args)
        {
            OnMouseButtonDown?.Invoke(args);
        }

        public void InvokeMouseButtonUpEvent(NbMouseButtonArgs args)
        {
            OnMouseButtonUp?.Invoke(args);
        }

        public void InvokeMouseMoveEvent(NbMouseMoveArgs args)
        {
            OnMouseMove?.Invoke(args);
        }

        public void InvokeMouseWheelEvent(NbMouseWheelArgs args)
        {
            OnMouseWheel?.Invoke(args);
        }

        public void InvokeResizeEvent(NbResizeArgs args)
        {
            OnResize?.Invoke(args);
        }


    }
}
