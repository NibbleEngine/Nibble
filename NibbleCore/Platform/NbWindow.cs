using NbCore.Common;
using NbCore;

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
        //public NbKeyEventHandler OnKeyUp;
        //public NbKeyEventHandler OnKeyDown;
        public NbMouseButtonEventHandler OnMouseButtonDown;
        public NbMouseButtonEventHandler OnMouseButtonUp;
        public NbMouseMoveEventHandler OnMouseMove;
        public NbMouseWheelEventHandler OnMouseWheel;
        public NbResizeEventHandler OnResize;
        public NbTextInputEventHandler OnTextInput;
        public NbKeyEventHandler OnKeyPressed;

        public virtual NbVector2i Size { get; set; }
        public virtual NbVector2 MouseDelta { get; }
        public NbVector2 MouseScroll;
        public NbVector2 MouseScrollPrevious;
        public NbVector2 MousePosition = new NbVector2(0);
        public NbVector2 MousePositionPrevious = new NbVector2(0);
        public virtual NbVector2 MouseScrollDelta { get; }
        public virtual NbVector2i ClientSize { get; }

        public virtual bool IsKeyDown(NbKey key) { throw new System.NotImplementedException(); }
        public virtual bool IsKeyPressed(NbKey key) { throw new System.NotImplementedException(); }
        public virtual bool IsKeyReleased(NbKey key) { throw new System.NotImplementedException(); }
        public virtual bool IsMouseButtonDown(NbMouseButton btn) { throw new System.NotImplementedException(); }
        public virtual bool IsMouseButtonPressed(NbMouseButton btn) { throw new System.NotImplementedException(); }
        public virtual bool IsMouseButtonReleased(NbMouseButton btn) { throw new System.NotImplementedException(); }

        public virtual void SetRenderFrameFrequency(int fps) { throw new System.NotImplementedException(); }
        public virtual void SetUpdateFrameFrequency(int fps) { throw new System.NotImplementedException(); }
        public virtual void SetVSync(bool status) { throw new System.NotImplementedException(); }

        public virtual void PauseRendering() { throw new System.NotImplementedException(); }
        public virtual void ResumeRendering() { throw new System.NotImplementedException(); }

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

        public void InvokeKeyPressed(NbKeyArgs args)
        {
            OnKeyPressed?.Invoke(args);
        }

        public void Log(string msg, LogVerbosityLevel lvl)
        {
            Callbacks.Log(this, msg, lvl);
        }

    }
}