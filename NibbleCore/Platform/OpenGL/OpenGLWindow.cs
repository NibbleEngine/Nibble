﻿#if OPENGL
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using NbCore.Math;
using ImGuiNET;
using System.Reflection;
using System.Runtime.CompilerServices;
using NbCore.Common;

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

        public override NbVector2 MousePosition
        {
            get
            {
                return new NbVector2(_win.MousePosition.X, _win.MousePosition.Y);
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
                return new NbVector2(_win.MouseState.Delta.X, _win.MouseState.Delta.Y);
            }
        }

        //Constructor
        public NbOpenGLWindow(NbVector2i WindowSize, Engine e, int opengl_major = 4, int opengl_minor = 5)
        {
            Engine = e; //Set Engine Reference
            _win = new GameWindow(GameWindowSettings.Default,
            new()
            {
                IsEventDriven = false,
                WindowBorder = WindowBorder.Resizable,
                StartFocused = true,
                WindowState = WindowState.Normal,
                Size = new Vector2i(WindowSize.X, WindowSize.Y),
                APIVersion = new System.Version(opengl_major, opengl_minor),
                API = ContextAPI.OpenGL,
                Profile = ContextProfile.Core,
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
                InvokeResizeEvent(new NbResizeArgs(a));
            };

            //OnRender
            _win.RenderFrame += RenderFrameDelegate;
            _win.UpdateFrame += FrameUpdateDelegate;

            _win.TextInput += (TextInputEventArgs a) =>
            {
                InvokeTextInput(new NbTextInputArgs(a));
            };
        }

        //Delegates
        private void RenderFrameDelegate(FrameEventArgs args)
        {
            OnRenderUpdate(args.Time);
            //Explicitly Handle Mouse Scroll
            MouseScrollPrevious = MouseScroll;
            MouseScroll.X = _win.MouseState.Scroll.X;
            MouseScroll.Y = _win.MouseState.Scroll.Y;

            _win.SwapBuffers();
        }

        private void FrameUpdateDelegate(FrameEventArgs args)
        {
            OnFrameUpdate(args.Time);
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
            RenderState.settings.RenderSettings.FPS = freq;
        }

        public override void SetUpdateFrameFrequency(int freq)
        {
            _win.UpdateFrequency = freq;
            RenderState.settings.TickRate = freq;
        }

        public override void SetVSync(bool status)
        {
            if (status)
                _win.VSync = VSyncMode.On;
            else
                _win.VSync = VSyncMode.Off;
            
            RenderState.settings.RenderSettings.UseVSync = status;
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