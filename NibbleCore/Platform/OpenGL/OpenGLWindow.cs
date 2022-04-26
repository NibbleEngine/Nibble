#if OPENGL
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace NbCore.Platform.Graphics
{

    public delegate void NbWindowOnRender(double dt);
    public delegate void NbWindowOnLoad();
    public delegate void NbWindowOnFrameUpdate(double dt);
    
    public class NbWindow : GameWindow
    {
        public NbWindowOnRender OnRenderUpdate;
        public NbWindowOnFrameUpdate OnFrameUpdate;
        public NbWindowOnLoad OnWindowLoad;
        
        public NbWindow() : base(GameWindowSettings.Default,
            new NativeWindowSettings() { Size = new Vector2i(800, 600), APIVersion = new System.Version(4, 5) })
        {
            
        }

        public void SetRenderFrameFrequency(int freq)
        {
            RenderFrequency = freq;
        }

        public void SetFrameUpdateFrequency(int freq)
        {
            UpdateFrequency = freq;
        }
        
        public void SetVSync(bool status)
        {
            if (status)
                VSync = VSyncMode.On;
            else
                VSync = VSyncMode.Off;
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            OnWindowLoad();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            OnRenderUpdate(args.Time);
            SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            OnFrameUpdate(args.Time);
        }

    }
}


#endif