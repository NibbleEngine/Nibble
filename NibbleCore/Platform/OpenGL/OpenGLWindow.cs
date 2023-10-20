#if OPENGL
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using NbCore;
using ImGuiNET;
using System.Reflection;
using System.Runtime.CompilerServices;
using NbCore.Common;
using System.Timers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using System;

namespace NbCore.Platform.Windowing
{
    
    public class NbOpenGLWindow : NbWindow
    {
        private GameWindow _win;
        private Stopwatch _resizeWatch;

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

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        private static extern bool SetCursorPos(int X, int Y);

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

        public override NbVector2 MouseScrollDelta
        {
            get
            {
                return MouseScroll - MouseScrollPrevious;
            }
        }

        public override NbVector2 MouseDelta
        {
            get
            {
                return MousePosition - MousePositionPrevious;
            }
        }

        //Constructor
        public NbOpenGLWindow(NbVector2i WindowSize, Engine e, int opengl_major = 4, int opengl_minor = 5)
        {
            Engine = e; //Set Engine Reference
            _resizeWatch = new Stopwatch();
            _win = new GameWindow(GameWindowSettings.Default,
            new()
            {
                StencilBits = 8,
                DepthBits = 24,
                IsEventDriven = false,
                WindowBorder = WindowBorder.Resizable,
                StartFocused = true,
                WindowState = WindowState.Normal,
                Title = "Test",
                Size = new Vector2i(WindowSize.X, WindowSize.Y),
                APIVersion = new System.Version(opengl_major, opengl_minor),
                API = ContextAPI.OpenGL,
                Profile = ContextProfile.Core,
                NumberOfSamples = 4,
#if OPENGL_DEBUG
                Flags = ContextFlags.Debug
#else
                Flags = ContextFlags.Default
#endif
            });
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
                _resizeWatch.Restart();
            };
            
            //OnRender
            _win.RenderFrame += RenderFrameDelegate;
            _win.UpdateFrame += FrameUpdateDelegate;
            _win.Minimized += MinimizedDelegate;
            
            _win.TextInput += (TextInputEventArgs a) =>
            {
                //InvokeKeyPressed(new NbKeyArgs(a));
                InvokeTextInput(new NbTextInputArgs(a));
            };
        }

        //Delegates
        private void RenderFrameDelegate(FrameEventArgs args)
        {
            //Check if we need to invoke the resize event
            if (_resizeWatch.ElapsedMilliseconds > 60)
            {
                NbResizeArgs new_args = new(new ResizeEventArgs(Size.X, Size.Y));
                InvokeResizeEvent(new_args);
                _resizeWatch.Stop();
                _resizeWatch.Reset();
            }

            OnRenderUpdate(args.Time);
            //Explicitly Handle Mouse Scroll and Pose
            MouseScrollPrevious = MouseScroll;
            MouseScroll.X = _win.MouseState.Scroll.X;
            MouseScroll.Y = _win.MouseState.Scroll.Y;
            MousePositionPrevious = MousePosition;
            MousePosition.X = _win.MousePosition.X;
            MousePosition.Y = _win.MousePosition.Y;

            MouseWrap();

            

            _win.SwapBuffers();
        }

        private void MouseWrap()
        {
            //Clamp Mouse Horizontally
            if (_win.MouseState.IsAnyButtonDown)
            {
                Vector2i screenPos = _win.PointToScreen(new Vector2i((int)_win.MousePosition.X, (int)_win.MousePosition.Y));
                bool update = false;
                
                if (_win.MousePosition.X > Size.X - 2)
                {
                    screenPos.X -= Size.X - 3;
                    update = true;
                }
                else if (_win.MousePosition.X < 2)
                {
                    screenPos.X += Size.X - 3;
                    update = true;
                }
                
                if (_win.MousePosition.Y > Size.Y - 2)
                {
                    screenPos.Y -= Size.Y - 3;
                    update = true;
                }
                else if (_win.MousePosition.Y < 2)
                {
                    screenPos.Y += Size.Y - 3;
                    update = true;
                }
                
                if (update)
                {
                    SetCursorPos(screenPos.X, screenPos.Y);
                    Vector2i newLocalPos = _win.PointToClient(screenPos);
                    MousePosition.X = newLocalPos.X;
                    MousePosition.Y = newLocalPos.Y;
                    MousePositionPrevious = MousePosition;
                }
                
            }
        }

        private void FrameUpdateDelegate(FrameEventArgs args)
        {
            OnFrameUpdate(args.Time);
        }

        private void MinimizedDelegate(MinimizedEventArgs args)
        {
            if (args.IsMinimized)
                _win.RenderFrame -= RenderFrameDelegate;
            else
                _win.RenderFrame += RenderFrameDelegate;
        }

        public override bool IsKeyDown(NbKey key)
        {
            return _win.IsKeyDown(NbKeyArgs.NbKeyToOpenTKMap[key]);
        }

        public override bool IsKeyPressed(NbKey key)
        {
            return _win.IsKeyPressed(NbKeyArgs.NbKeyToOpenTKMap[key]);
        }

        public override bool IsKeyReleased(NbKey key)
        {
            return _win.IsKeyReleased(NbKeyArgs.NbKeyToOpenTKMap[key]);
        }

        public override bool IsMouseButtonDown(NbMouseButton btn)
        {
            return _win.IsMouseButtonDown(NbMouseButtonArgs.NbKeyToOpenTKMap[btn]);
        }

        public override bool IsMouseButtonPressed(NbMouseButton btn)
        {
            return _win.IsMouseButtonPressed(NbMouseButtonArgs.NbKeyToOpenTKMap[btn]);
        }

        public override bool IsMouseButtonReleased(NbMouseButton btn)
        {
            return _win.IsMouseButtonReleased(NbMouseButtonArgs.NbKeyToOpenTKMap[btn]);
        }

        public override void SetRenderFrameFrequency(int freq)
        {
            _win.RenderFrequency = freq;
            NbRenderState.settings.RenderSettings.FPS = freq;
        }

        public override void SetUpdateFrameFrequency(int freq)
        {
            _win.UpdateFrequency = freq;
            NbRenderState.settings.TickRate = freq;
        }

        public override void SetVSync(bool status)
        {
            if (status)
                _win.VSync = VSyncMode.On;
            else
                _win.VSync = VSyncMode.Off;
            
            NbRenderState.settings.RenderSettings.UseVSync = status;
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