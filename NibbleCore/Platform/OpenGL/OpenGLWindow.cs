#if OPENGL
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using NbCore.Math;

namespace NbCore.Platform.Windowing
{
    public class NbOpenGLWindow : NbWindow
    {
        private GameWindow _win;
        
        //Properties
        public string Title
        {
            get
            {
                return _win.Title;
            }

            set
            {
                _win.Title = value;
            }
        }

        public override NbVector2i Size
        {
            get
            {
                return new NbVector2i(_win.Size.X, _win.Size.Y);
            }

            set
            {
                _win.Size = new Vector2i(value.X, value.Y);
            }
        }

        public override NbVector2i ClientSize
        {
            get
            {
                return new NbVector2i(_win.ClientSize.X, _win.ClientSize.Y);
            }
        }

        //Constructor
        public NbOpenGLWindow(NbVector2i WindowSize, Engine e, int opengl_major = 4, int opengl_minor = 5)
        {
            Engine = e; //Set Engine Reference
            _win = new GameWindow(GameWindowSettings.Default,
            new NativeWindowSettings() { Size = new Vector2i(WindowSize.X, WindowSize.Y), 
                APIVersion = new System.Version(opengl_major, opengl_minor) });
            SetWindowCallbacks();
        }
        
        //Methods
        private void SetWindowCallbacks()
        {
            //OnLoad
            _win.Load += () => {
                OnWindowLoad();
            };

            //OnResize
            _win.Resize += (ResizeEventArgs a) =>
            {
                InvokeResizeEvent(new NbResizeArgs(a));
            };

            //OnRender
            _win.RenderFrame += (FrameEventArgs a) =>
            {
                OnRenderUpdate(a.Time);
                _win.SwapBuffers();
            };

            _win.UpdateFrame += (FrameEventArgs a) =>
            {
                OnFrameUpdate(a.Time);
            };

            _win.KeyDown += (KeyboardKeyEventArgs a) =>
            {
                OnKeyDown?.Invoke(new NbKeyArgs(a));
            };

            _win.KeyUp += (KeyboardKeyEventArgs a) =>
            {
                OnKeyUp?.Invoke(new NbKeyArgs(a));
            };

            _win.MouseMove += (MouseMoveEventArgs a) =>
            {
                OnMouseMove?.Invoke(new NbMouseMoveArgs(a));
            };

            _win.MouseWheel += (MouseWheelEventArgs a) =>
            {
                InvokeMouseWheelEvent(new NbMouseWheelArgs(a));
            };

            _win.MouseDown += (MouseButtonEventArgs a) =>
            {
                InvokeMouseButtonDownEvent(new NbMouseButtonArgs(a));
            };

            _win.MouseUp += (MouseButtonEventArgs a) =>
            {
                InvokeMouseButtonUpEvent(new NbMouseButtonArgs(a));
            };

            _win.TextInput += (TextInputEventArgs a) =>
            {
                InvokeTextInput(new NbTextInputArgs(a));
            };
        }

        public void SetRenderFrameFrequency(int freq)
        {
            _win.RenderFrequency = freq;
        }

        public void SetFrameUpdateFrequency(int freq)
        {
            _win.UpdateFrequency = freq;
        }
        
        public void SetVSync(bool status)
        {
            if (status)
                _win.VSync = VSyncMode.On;
            else
                _win.VSync = VSyncMode.Off;
        }

        public void Run()
        {
            _win.Run();
        }

        public void Close()
        {
            _win.Close();
        }

    }
}


#endif