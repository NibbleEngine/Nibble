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
        public Engine EngineRef = null;

        //ImguiPalette Colors
        //Blue
        public static System.Numerics.Vector4 DarkBlue = new(0.04f, 0.2f, 0.96f, 1.0f);

        public ImGuiManager(int width, int height, Engine engine)
        {
            EngineRef = engine;
            _controller = new ImGuiController(width, height); //Init with a start size

            //Initialize items
            LogViewer = new();
        }

        //Resize available imgui space
        public virtual void Resize(int x, int y)
        {
            _controller.WindowResized(x, y);
        }

        public virtual void Update(double dt)
        {
            _controller.Update((float)dt);
        }

        public virtual void SetWindowRef(NbWindow win)
        {
            _controller.SetWindowRef(win);
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