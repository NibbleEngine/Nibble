using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NbCore;
using OpenTK;
using NbCore.UI.ImGui;
using NbCore.Common;
using NbCore.Platform.Graphics.OpenGL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using ImGuiNET;
using System.Collections.Generic;

namespace NibbleTextureViewer
{
    public class TextureRenderer : OpenTK.Windowing.Desktop.GameWindow
    {
        private Engine _engine;
        private NbTexture _texture; //Texture handle
        private NbCore.Math.NbVector2 offset = new(0.0f);
        
        //Mouse States
        private NbMouseState currentMouseState = new();
        private NbMouseState prevMouseState = new();

        //Application Layers
        private ApplicationLayerStack stack;
        private RenderLayer _renderLayer;
        private UILayer _UILayer;

        public TextureRenderer(): base(OpenTK.Windowing.Desktop.GameWindowSettings.Default,
            OpenTK.Windowing.Desktop.NativeWindowSettings.Default)
        {
            Title = "Nibble Texture Viewer " + Version.GetString();
            VSync = VSyncMode.On;
            RenderFrequency = 30;
            Callbacks.Logger = new NbLogger();

        }

        private void OnCloseWindowEvent(object sender, string data)
        {
            Console.WriteLine("EVENT TRIGGERED");
            Console.WriteLine(data);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            switch (e.Button)
            {
                case OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left:
                    currentMouseState.SetButtonStatus(NbMouseButton.LEFT, true);
                    break;
                case OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right:
                    currentMouseState.SetButtonStatus(NbMouseButton.RIGHT, true);
                    break;
                case OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Middle:
                    currentMouseState.SetButtonStatus(NbMouseButton.MIDDLE, true);
                    break;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            switch (e.Button)
            {
                case OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left:
                    currentMouseState.SetButtonStatus(NbMouseButton.LEFT, false);
                    break;
                case OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right:
                    currentMouseState.SetButtonStatus(NbMouseButton.RIGHT, false);
                    break;
                case OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Middle:
                    currentMouseState.SetButtonStatus(NbMouseButton.MIDDLE, false);
                    break;
            }
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            currentMouseState.Position.X = e.X;
            currentMouseState.Position.Y = e.Y;
            currentMouseState.PositionDelta.X = e.X - prevMouseState.Position.X;
            currentMouseState.PositionDelta.Y = e.Y - prevMouseState.Position.Y;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            currentMouseState.Scroll.X += e.OffsetX;
            currentMouseState.Scroll.Y += e.OffsetY;
        }


        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
        }

        private void OpenFile(object sender, string filepath)
        {
            if (_texture != null)
                _texture.Dispose();
            
            _texture = new NbTexture(filepath);
            NbCore.Platform.Graphics.GraphicsAPI.GenerateTexture(_texture);
            NbCore.Platform.Graphics.GraphicsAPI.UploadTexture(_texture);
            
            _renderLayer.SetTexture(_texture);
            _UILayer.SetTexture(_texture);
        }
        
        private void ImportTextureLayer(object sender, string filepath, int depth_id)
        {
            //Layer Texture
            NbTexture layer = new NbTexture(filepath);
            DDSImage.ReplaceTextureLayer((DDSImage) layer.Data,
                                         (DDSImage) _texture.Data, 
                                         depth_id);
            layer.Dispose();
            NbCore.Platform.Graphics.GraphicsAPI.UploadTexture(_texture);
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            Callbacks.SetDefaultCallbacks();
            
            //Initialize Engine
            _engine = new Engine(this);
            RenderState.engineRef = _engine;
            _engine.init(ClientSize.X, ClientSize.Y);
            
            //Initialize Application Layers
            _renderLayer = new(_engine); //render layer
            _UILayer = new(this, _engine);

            //Organize them into a stack
            stack = new();
            stack.AddApplicationLayer(_renderLayer);
            stack.AddApplicationLayer(_UILayer);

            //Subscribe to layer events
            _UILayer.CloseWindowEvent += OnCloseWindowEvent;
            _UILayer.OpenFileEvent += OpenFile;
            _UILayer.ImportLayerEvent += ImportTextureLayer;
            _UILayer.ConsumeInputEvent += _renderLayer.CaptureInput;
            _UILayer.RenderTextureDataChanged += _renderLayer.OnRenderTextureDataChanged;
            Resize += _UILayer.OnResize;
            Resize += _renderLayer.OnResize;
            
            //GL.Enable(EnableCap.DepthTest);

            //Setup Texture
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\BEAMGRADIENT.DDS";
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\SCROLLINGCLOUD.DDS";
            //string texturepath = "E:\\SSD_SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\COMMON\\ROBOTS\\QUADRUPED.DDS";
            //string texturepath = "D:\\Downloads\\TILEMAP.DDS";
            //string texturepath = "D:\\Downloads\\TILEMAP.HSV.DDS";
            //string texturepath = "D:\\Downloads\\TILEMAP.NORMAL.DDS";

            //_texture = new Texture(Callbacks.getResource("default.dds"), 
            //                       true, "default");

        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            //Prepare Engine Layer Data Queue
            Queue<object> data = new();
            data.Enqueue(currentMouseState);

            stack.OnRenderFrame(data, e.Time);
            
            //Reset Input Data
            prevMouseState = currentMouseState;
            currentMouseState.PositionDelta.X = 0.0f;
            currentMouseState.PositionDelta.Y = 0.0f;
            currentMouseState.Scroll.X = 0.0f;
            currentMouseState.Scroll.Y = 0.0f;


            SwapBuffers();
            base.OnRenderFrame(e);
        }

        
        public void DisposeLayers()
        {
            _renderLayer.Dispose();

        }

        [STAThread]
        public static void Main()
        {
            using (TextureRenderer tx = new TextureRenderer())
            {
                tx.Run();
            }
        }
    }
}
