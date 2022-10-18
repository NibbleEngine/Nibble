using NbCore.Math;

namespace NbCore.Platform.Windowing
{
    public delegate void NbWindowOnRender(double dt);
    public delegate void NbWindowOnLoad();
    public delegate void NbWindowOnFrameUpdate(double dt);

    public delegate void NbKeyEventHandler(NbKeyArgs args);
    public delegate void NbMouseButtonEventHandler(NbMouseButtonArgs args);
    public delegate void NbMouseMoveEventHandler(NbMouseMoveArgs args);
    public delegate void NbMouseWheelEventHandler(NbMouseWheelArgs args);
    public delegate void NbResizeEventHandler(NbResizeArgs args);
    public delegate void NbTextInputEventHandler(NbTextInputArgs args);

    public abstract class NbWindow
    {
        public Engine Engine;
        public NbWindowOnRender OnRenderUpdate;
        public NbWindowOnFrameUpdate OnFrameUpdate;
        public NbWindowOnLoad OnWindowLoad;
        public NbKeyEventHandler OnKeyUp;
        public NbKeyEventHandler OnKeyDown;
        public NbMouseButtonEventHandler OnMouseButtonDown;
        public NbMouseButtonEventHandler OnMouseButtonUp;
        public NbMouseMoveEventHandler OnMouseMove;
        public NbMouseWheelEventHandler OnMouseWheel;
        public NbResizeEventHandler OnResize;
        public NbTextInputEventHandler OnTextInput;
        
        public virtual NbVector2i Size { get; set; }

        public virtual NbVector2i ClientSize { get; }

        public void InvokeMouseButtonDownEvent(NbMouseButtonArgs args)
        {
            OnMouseButtonDown?.Invoke(args);
        }

        public void InvokeMouseButtonUpEvent(NbMouseButtonArgs args)
        {
            OnMouseButtonUp?.Invoke(args);
        }

        public void InvokeMouseWheelEvent(NbMouseWheelArgs args)
        {
            OnMouseWheel?.Invoke(args);
        }

        public void InvokeResizeEvent(NbResizeArgs args)
        {
            OnResize?.Invoke(args);
        }

        public void InvokeTextInput(NbTextInputArgs args)
        {
            OnTextInput?.Invoke(args);
        }


    }
}
