using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using NbCore;
using NbCore.Common;
using System.Timers;
using System.Diagnostics;
using NbCore.Platform.Windowing;

namespace OpenTKWindowTest
{
    internal class Program
    {
        private static void CreateOpenTKWindow()
        {
            GameWindow game = new GameWindow(GameWindowSettings.Default,
                new NativeWindowSettings()
                {
                    Profile = ContextProfile.Core,
                });
            game.RenderFrequency = 0.0;
            game.UpdateFrequency = 0.0;
            game.VSync = VSyncMode.Off;
            double frametime = 0.006; //in s
            int fps = 0;
            double fps_time = 0.0;

            Timer fps_timer = new Timer();

            fps_timer.Interval = 1000.0;
            fps_timer.Elapsed += (object sender, ElapsedEventArgs args) =>
            {
                Callbacks.DefaultLog(game, $"FPS: {fps}", NbCore.LogVerbosityLevel.INFO);
                fps = 0;
            };
            fps_timer.Start();

            //Measure avg time for an addition
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int a = 0;
            int N = 100000000;
            for (int i = 0; i < N; i++)
                a++;
            watch.Stop();
            double time_for_addition = watch.ElapsedMilliseconds / (double)N;
            Callbacks.DefaultLog(game, $"Avg Time for an addition: {time_for_addition}", NbCore.LogVerbosityLevel.INFO);

            game.RenderFrame += (FrameEventArgs e) =>
            {
                fps++;
                OpenTK.Graphics.OpenGL4.GL.Clear(OpenTK.Graphics.OpenGL4.ClearBufferMask.DepthBufferBit | OpenTK.Graphics.OpenGL4.ClearBufferMask.ColorBufferBit);
                OpenTK.Graphics.OpenGL4.GL.ClearColor(1.0f, 0.0f, 1.0f, 1.0f);
                game.SwapBuffers();
                //skipTime((int)(frametime / time_for_addition));
                //Thread.Sleep((int)Math.Max(0.0, frametime - end_frame_time + start_frame_time));
            };

            game.MouseMove += (MouseMoveEventArgs args) =>
            {
                //Callbacks.DefaultLog(game, $"{args.X} {args.Y}", LogVerbosityLevel.DEBUG);
            };

            game.Run();
        }

        static void skipTime(int N)
        {
            int a = 0;
            for (int i = 0; i < N; i++)
                a++;
        }

        static void CreateNbWindow()
        {
            Engine e = new Engine();
            NbOpenGLWindow win = new NbOpenGLWindow(new NbCore.Math.NbVector2i(1024), e);

            //win.OnRenderUpdate += (double dt) =>
            //{
            //    Callbacks.DefaultLog(win, "RENDERIIIIIING", LogVerbosityLevel.DEBUG);
            //};

            win.OnKeyDown += (NbKeyArgs args) =>
            {
                Callbacks.DefaultLog(win, args.Key.ToString(), LogVerbosityLevel.DEBUG);
            };

            win.OnMouseMove += (NbMouseMoveArgs args) =>
            {
                Callbacks.DefaultLog(win, $"{args.X} {args.Y}", LogVerbosityLevel.DEBUG);
            };

            win.Run();
        }

        static void Main(string[] args)
        {
            CreateOpenTKWindow();
            //CreateNbWindow();
        }

        
    }
}
