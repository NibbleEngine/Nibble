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



namespace SimpleTextureRenderer
{
    public class TextureRenderer : OpenTK.Windowing.Desktop.GameWindow
    {
        private Texture _texture;
        private Engine _engine;
        private DDSImage _ddsImage;
        private int mipmap_id = 0;
        private int depth_id = 0;
        private GLSLShaderConfig shader;
        private int quad_vao_id;
        AppImGuiManager _ImGuiManager;
        
        //Mouse States
        private NbMouseState currentMouseState = new();
        private NbMouseState prevMouseState = new();

        //Imgui stuff
        private bool IsOpenFileDialogOpen = false;
        
        public TextureRenderer(): base(OpenTK.Windowing.Desktop.GameWindowSettings.Default,
            OpenTK.Windowing.Desktop.NativeWindowSettings.Default)
        {
            Title = "DDS Texture Viewer v1.0";
            VSync = VSyncMode.On;
            RenderFrequency = 30;
            
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
            _ImGuiManager.Resize(ClientSize.X, ClientSize.Y);
            base.OnResize(e);
        }

        private void OpenFile(string filepath)
        {

            if (_texture != null)
                _texture.Dispose();
            
            _texture = new Texture(filepath, true);
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            Callbacks.SetDefaultCallbacks();

            //Initialize Engine
            _engine = new Engine(this);
            RenderState.engineRef = _engine;
            _engine.init(ClientSize.X, ClientSize.Y);
            _ImGuiManager = new(this, _engine);

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
            
            _texture = null;

            //Compile Necessary Shaders

            string vs_path = "Shaders/Gbuffer_VS.glsl";
            vs_path = Path.GetFullPath(vs_path);
            vs_path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, vs_path);


            string fs_path = "Shaders/texture_shader_fs.glsl";
            fs_path = Path.GetFullPath(fs_path);
            fs_path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, fs_path);


            GLSLShaderSource vs = _engine.GetShaderSourceByFilePath(vs_path);
            GLSLShaderSource fs = _engine.GetShaderSourceByFilePath(fs_path);

            //Pass Shader
            shader = GLShaderHelper.compileShader(vs, fs, null, null, null,
                new(), SHADER_TYPE.MATERIAL_SHADER, SHADER_MODE.DEFAULT);
        
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            //Update Imgui

            //Send Input
            _ImGuiManager.SetMouseState(currentMouseState);
            _engine.SetMouseState(currentMouseState);
            
            prevMouseState = currentMouseState;
            currentMouseState.PositionDelta.X = 0.0f;
            currentMouseState.PositionDelta.Y = 0.0f;
            currentMouseState.Scroll.X = 0.0f;
            currentMouseState.Scroll.Y = 0.0f;

            _ImGuiManager.Update(e.Time);

            NbCore.Platform.Graphics.IGraphicsApi renderer = _engine.renderSys.Renderer;


            renderer.Viewport(ClientSize.X, ClientSize.Y);
            renderer.ClearColor(new NbCore.Math.NbVector4(1.0f, 1.0f, 0.0f, 0.0f));
            renderer.ClearDrawBuffer(NbCore.Platform.Graphics.NbBufferMask.Color |
                                    NbCore.Platform.Graphics.NbBufferMask.Depth);

            if (_texture != null)
            {
                //Set Shader State
                shader.ClearCurrentState();

                shader.CurrentState.AddSampler("InTex", new()
                {
                    Target = _texture.target,
                    TextureID = _texture.texID
                });

                shader.CurrentState.AddUniform("texture_depth", (float)depth_id);
                shader.CurrentState.AddUniform("mipmap", (float)mipmap_id);

                renderer.EnableShaderProgram(shader);

                NbMesh nm = _engine.GetPrimitiveMesh((ulong)"default_renderquad".GetHashCode());
                renderer.RenderQuad(nm, shader, shader.CurrentState);
            }

            //Draw UI
            DrawUI();
            //ImGui.ShowDemoWindow();
            _ImGuiManager.Render();

            SwapBuffers();
            base.OnRenderFrame(e);
        }

        private void DrawUI()
        {
            //Draw Main MenuBar
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open"))
                    {
                        _ImGuiManager.ShowOpenFileDialog();
                        IsOpenFileDialogOpen = true;
                    }

                    if (ImGui.MenuItem("Close"))
                    {
                        //Dispose stuff and close
                        _texture.Dispose();
                        _engine.CleanUp();
                        Close();
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }


            if (_texture != null) {

                if (ImGui.Begin("Texture Properties"))
                {
                    ImGui.Text("Info:");
                    ImGui.Columns(2);
                    ImGui.Text("Width");
                    ImGui.Text("Height");
                    ImGui.Text("Depth");
                    ImGui.Text("MipMapCount");
                    ImGui.Text("Format");
                    ImGui.NextColumn();
                    ImGui.Text(_texture.Width.ToString());
                    ImGui.Text(_texture.Height.ToString());
                    ImGui.Text(_texture.Depth.ToString());
                    ImGui.Text(_texture.MipMapCount.ToString());

                    //Make format output a bit friendlier
                    switch (_texture.pif)
                    {
                        case NbTextureInternalFormat.DXT5:
                            ImGui.Text("DXT5");
                            break;
                        case NbTextureInternalFormat.DXT1:
                            ImGui.Text("DXT1");
                            break;
                        case NbTextureInternalFormat.RGTC2:
                            ImGui.Text("ATI2A2XY");
                            break;
                        case NbTextureInternalFormat.BC7:
                            ImGui.Text("BC7 (DX10 Header)");
                            break;
                        default:
                            ImGui.Text("UNKNOWN");
                            break;
                    }

                    ImGui.NextColumn();
                    ImGui.Separator();
                    //Prepare depth options
                    ImGui.Text("Active Depth:");
                    ImGui.NextColumn();


                    string[] opts = new string[_texture.Depth];
                    for (int i = 0; i < opts.Length; i++)
                        opts[i] = i.ToString();
                    ImGui.Combo("##0", ref depth_id, opts, _texture.Depth, 12);

                    ImGui.NextColumn();
                    ImGui.Text("Active Mipmap:");

                    opts = new string[_texture.MipMapCount];
                    for (int i = 0; i < opts.Length; i++)
                        opts[i] = i.ToString();

                    ImGui.NextColumn();
                    ImGui.Combo("##1", ref mipmap_id, opts, _texture.MipMapCount, 12);

                    ImGui.Columns(1);
                    ImGui.End();
                }

            }

            //Main StatusBar
            float textHeight = ImGui.GetTextLineHeight();
            ImGuiViewportPtr vp = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, vp.Size.Y - 1.4f * textHeight));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(vp.Size.X, 1.6f * textHeight));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new System.Numerics.Vector2(0f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.0f);

            ImGuiWindowFlags sbarFlags = ImGuiWindowFlags.NoResize |
                                         ImGuiWindowFlags.NoScrollbar |     
                                         ImGuiWindowFlags.NoTitleBar;
            bool sbar_open = true;

            if (ImGui.Begin("##TestWindow", ref sbar_open, sbarFlags))
            {
                //StatusBar Texts
                string statusText = "Ready";
                string copyrightText = "Created by gregkwaste©";
                ImGui.Columns(2, "#statusbar", false);
                ImGui.SetCursorPosY(2.0f);
                ImGui.Text(statusText);
                ImGui.NextColumn();
                
                ImGui.SetColumnOffset(ImGui.GetColumnIndex(), vp.Size.X - ImGui.CalcTextSize(copyrightText).X);
                ImGui.SetCursorPosY(2.0f);
                ImGui.Text("Made by gregkwaste");
                ImGui.Columns(1);
                ImGui.End();
            }

            ImGui.PopStyleVar(4);


            //Process Modals
            bool oldOpenDialogStatus = IsOpenFileDialogOpen;
            string filePath = "";
            _ImGuiManager.ProcessModals(this, ref filePath, ref IsOpenFileDialogOpen);

            if (oldOpenDialogStatus == true && IsOpenFileDialogOpen == false)
            {
                //Open File
                OpenFile(filePath);
            }



        }

        [STAThread]
        public static void Main()
        {
            using (TextureRenderer tx = new TextureRenderer())
            {
                tx.Run();
            }
            
            Console.WriteLine("All Good");
        }
    }
}
