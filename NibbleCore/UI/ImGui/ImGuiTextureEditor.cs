using System;
using NbCore;
using NbCore.Common;
using ImGuiCore = ImGuiNET.ImGui;
using System.Collections.Generic;


namespace NbCore.UI.ImGui
{
    public class ImGuiTextureEditor
    {
        private Texture _ActiveTexture = null;
        private int _SelectedId = -1;
        private string texture_path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private OpenFileDialog openFileDialog;

        public ImGuiTextureEditor()
        {
            openFileDialog = new("texture-open-file", ".dds|.png", false); //Initialize OpenFileDialog
            openFileDialog.SetDialogPath(texture_path);
        }

        public void Draw()
        {
            //Items
            List<Texture> textureList = RenderState.engineRef.renderSys.TextureMgr.Entities;
            string[] items = new string[textureList.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = textureList[i].Path == "" ? "Texture_" + i : textureList[i].Path; 

            if (ImGuiCore.Combo("##1", ref _SelectedId, items, items.Length))
                _ActiveTexture = textureList[_SelectedId];

            ImGuiCore.SameLine();

            if (ImGuiCore.Button("Add"))
            {
                openFileDialog.Open();
            }


            //Draw Open File Dialog
            if (openFileDialog.Draw(new() { X = 640, Y = 480 }))
            {
                texture_path = System.IO.Path.GetDirectoryName(openFileDialog.GetSelectedFile());
                Texture tex = new(openFileDialog.GetSelectedFile(), true);
                RenderState.engineRef.RegisterEntity(tex);
                SetTexture(tex);
            }

            ImGuiCore.SameLine();
            if (ImGuiCore.Button("Del"))
            {
                Console.WriteLine("Todo Delete Texture");
            }

            if (_ActiveTexture is null)
            {
                return;
            }


            if (_ActiveTexture.texID != -1 && _ActiveTexture.target != NbTextureTarget.Texture2DArray)
            {
                ImGuiCore.SetNextItemWidth(-1);
                float image_aspect = (float)_ActiveTexture.Width / _ActiveTexture.Height;
                var avail_size = ImGuiCore.GetContentRegionAvail();
                avail_size.X = System.Math.Min(avail_size.X, avail_size.Y);
                avail_size.Y = System.Math.Min(avail_size.X, avail_size.Y);
                
                System.Numerics.Vector2 vpsize;
                if (image_aspect > 1.0f)
                {
                    vpsize = new System.Numerics.Vector2(avail_size.X, avail_size.Y / image_aspect);
                }
                else
                {
                    vpsize = new System.Numerics.Vector2(avail_size.X * image_aspect, avail_size.Y);
                }
                
                ImGuiCore.Image((IntPtr)_ActiveTexture.texID, vpsize);
            }

            if (ImGuiCore.BeginTable("##TextureInfo", 2))
            {
                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("Path");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.SetNextItemWidth(-1);
                ImGuiCore.InputText("", ref _ActiveTexture.Path, 30);
                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("Width");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.SetNextItemWidth(-1);
                ImGuiCore.Text(_ActiveTexture.Width.ToString());

                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("Height");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.SetNextItemWidth(-1);
                ImGuiCore.Text(_ActiveTexture.Height.ToString());

                ImGuiCore.EndTable();

            }

        }

        public void SetTexture(Texture tex)
        {
            _ActiveTexture = tex;
            List<Texture> textureList = RenderState.engineRef.renderSys.TextureMgr.Entities;
            _SelectedId = textureList.IndexOf(tex);
        }
    }
}