using ImGuiNET;
using NbCore.Platform.Windowing;
using System;


namespace NbCore.UI.ImGui
{
    public class ImGuiManager
    {
        //ImGui Variables
        private readonly ImGuiLog LogViewer;
        private ImGuiController _controller;
        public NbWindow WindowRef { get; } = null;

        //ImguiPalette Colors
        //Blue
        public static System.Numerics.Vector4 DarkBlue = new(0.04f, 0.2f, 0.96f, 1.0f);

        public ImGuiManager(NbWindow win)
        {
            WindowRef = win;
            _controller = new ImGuiController(win); //Init with a start size
            LogViewer = new();
        }

        public virtual void Update(double dt)
        {
            _controller.Update((float)dt);
        }

        public virtual void SetWindowRef(NbWindow win)
        {
            
        }

        public virtual void Render()
        {
            _controller.Render();
        }

        public virtual void SendChar(char e)
        {
            _controller.PressChar(e);
        }

        //Logger
        public virtual void DrawLogger()
        {
            LogViewer?.Draw();
        }

        public virtual void Log(LogElement msg)
        {
            LogViewer?.AddLog(msg);
        }

        public virtual void ProcessModals(object ob, ref string current_file_path, ref bool closed)
        {
            //Override to provide modal processing
            throw new Exception("Not Implented!");
        }



    }





}