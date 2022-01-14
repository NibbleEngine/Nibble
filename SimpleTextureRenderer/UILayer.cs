using System.Collections.Generic;
using NbCore;
using ImGuiNET;
using OpenTK.Windowing.Common;
using System;

namespace SimpleTextureRenderer
{
    public delegate void CloseWindowEventHandler(object sender, string data);
    

    public class UILayer : ApplicationLayer
    {
        public event CloseWindowEventHandler CloseWindowEvent;
        public event EventHandler<RenderTextureData> RenderTextureDataChanged;
        public event EventHandler<string> OpenFileEvent;
        public event EventHandler<bool> ConsumeInputEvent;
        
        private AppImGuiManager _ImGuiManager;
        private TextureRenderer _winRef;
        public Texture _texture; //Also keep texture Reference here
        private bool ConsumesInput = false;
        private int depth_id = 0;
        private int mipmap_id = 0;

        //Imgui stuff
        private bool IsOpenFileDialogOpen = false;
        
        public UILayer(TextureRenderer win, Engine engine) : base(engine)
        {
            _winRef = win;
            _ImGuiManager = new(win, engine); //TODO: check why the window is needed
        }

        public void OnResize(ResizeEventArgs args)
        {
            _ImGuiManager.Resize(args.Width, args.Height);
        }

        public void SetTexture(Texture tex)
        {
            if (_texture != null)
                _texture.Dispose();
            _texture = tex;
        }

        public void SetTextureDepth(int id)
        {
            depth_id = id;
        }

        public void SetMipmap(int id)
        {
            mipmap_id = id;
        }

        public override void OnRenderFrameUpdate(ref Queue<object> data, double dt)
        {
            //First argument should be the input state
            NbMouseState mouseState = (NbMouseState)data.Dequeue();

            //Send Input
            _ImGuiManager.SetMouseState(mouseState);
            _ImGuiManager.Update(dt);

            DrawUI(_texture);
            
            //ImGui.ShowDemoWindow();
            _ImGuiManager.Render();
        }

        private void DrawUI(Texture texture)
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
                        //Trigger WindowCloseEvent
                        CloseWindowEvent?.Invoke(this, "UI LAYER TRIGGERED THIS EVENT");
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }


            if (_texture != null)
            {

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

                    var io = ImGui.GetIO();
                    ConsumeInputEvent?.Invoke(this, io.WantCaptureMouse);
                    RenderTextureDataChanged?.Invoke(this, new() { depth_id = depth_id, mipmap_id = mipmap_id });
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
                string copyrightText = "Created by gregkwaste©  ";
                ImGui.Columns(2, "#statusbar", false);
                ImGui.SetCursorPosY(2.0f);
                ImGui.Text(statusText);
                ImGui.NextColumn();

                ImGui.SetColumnOffset(ImGui.GetColumnIndex(), vp.Size.X - ImGui.CalcTextSize(copyrightText).X);
                ImGui.SetCursorPosY(2.0f);
                ImGui.Text(copyrightText);
                ImGui.Columns(1);
                ImGui.End();
            }

            ImGui.PopStyleVar(4);


            //Process Modals
            bool oldOpenDialogStatus = IsOpenFileDialogOpen;
            string filePath = "";
            _ImGuiManager.ProcessModals(_winRef, ref filePath, ref IsOpenFileDialogOpen);

            if (oldOpenDialogStatus == true && IsOpenFileDialogOpen == false)
            {
                //Trigger OpenFileEvent
                OpenFileEvent?.Invoke(this, filePath);
                oldOpenDialogStatus = false;
                
            }

        }




    }
}
