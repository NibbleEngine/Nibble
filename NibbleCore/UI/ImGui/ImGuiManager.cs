﻿using ImGuiNET;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using OpenTK.Windowing.Desktop;
using System;
using NbCore;

namespace NbCore.UI.ImGui
{   
    public class ImGuiManager
    {
        //ImGui Variables
        private readonly ImGuiObjectViewer ObjectViewer;
        private readonly ImGuiSceneGraphViewer SceneGraphViewer;
        private readonly ImGuiMaterialEditor MaterialEditor;
        private readonly ImGuiTextureEditor TextureEditor;
        private readonly ImGuiShaderEditor ShaderEditor;
        private readonly ImGuiLog LogViewer;
        private ImGuiController _controller;
        public GameWindow WindowRef = null;
        public Engine EngineRef = null;
        
        //ImguiPalette Colors
        //Blue
        public static System.Numerics.Vector4 DarkBlue = new(0.04f, 0.2f, 0.96f, 1.0f);

        public ImGuiManager(GameWindow win, Engine engine)
        {
            WindowRef = win;
            EngineRef = engine;
            _controller = new ImGuiController(win.ClientSize.X, win.ClientSize.Y); //Init with a start size
            
            //Enable docking by default
            ImGuiIOPtr io = ImGuiNET.ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; //Enable Docking
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable; //Enable MultipleViewport

            //Initialize items
            ObjectViewer = new(this);
            SceneGraphViewer = new(this);
            MaterialEditor = new();
            ShaderEditor = new();
            TextureEditor = new();
            LogViewer = new();
        }

        //Resize available imgui space
        public virtual void Resize(int x, int y)
        {
            _controller.WindowResized(x, y);
        }

        public virtual void Update(double dt)
        {
            _controller.Update(WindowRef, (float) dt);
        }

        public virtual void SetMouseState(NbMouseState state)
        {
            _controller.SetMouseState(state);
        }

        public virtual void SetKeyboardState(NbKeyboardState state)
        {
            _controller.SetKeyboardState(state);
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

        //Texture Viewer Related Methods
        public virtual void DrawTextureEditor()
        {
            TextureEditor?.Draw();
        }

        public virtual void SetActiveTexture(NbTexture t)
        {
            TextureEditor.SetTexture(t);
        }

        //Material Viewer Related Methods
        public virtual void DrawMaterialEditor()
        {
            MaterialEditor?.Draw();
        }

        public virtual void SetActiveMaterial(Entity m)
        {
            if (m.HasComponent<MeshComponent>())
            {
                MeshComponent mc = m.GetComponent<MeshComponent>() as MeshComponent;
                MaterialEditor.SetMaterial(mc.Mesh.Material);
            }
        }

        //Shader Editor Related Methods
        public virtual void DrawShaderEditor()
        {
            ShaderEditor?.Draw();
        }

        public virtual void SetActiveShaderConfig(GLSLShaderConfig s)
        {
            ShaderEditor.SetShader(s);
        }

        //Object Viewer Related Methods

        public virtual void DrawObjectInfoViewer()
        {
            ObjectViewer?.Draw();
        }

        public virtual void SetObjectReference(SceneGraphNode m)
        {
            ObjectViewer.SetModel(m);
        }

        //SceneGraph Related Methods

        public void DrawSceneGraph()
        {
            SceneGraphViewer?.Draw();
        }

        public void PopulateSceneGraph(SceneGraph scn)
        {
            SceneGraphViewer.Init(scn.Root);
        }

        public void ClearSceneGraph()
        {
            SceneGraphViewer.Clear();
        }

        public virtual void ProcessModals(object ob, ref string current_file_path, ref bool closed)
        {
            //Override to provide modal processing
            throw new Exception("Not Implented!");
        }



    }





}
