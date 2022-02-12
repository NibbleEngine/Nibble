using OpenTK.Windowing.Desktop;
using System;
using ImGuiNET;
using NbCore;
using NbCore.UI.ImGui;

namespace NibbleTextureViewer
{
    public class AppImGuiManager : ImGuiManager
    {
        private bool show_open_file_dialog = false;
        private string current_file_path = "";

        public AppImGuiManager(GameWindow win, Engine engine) : base(win, engine)
        {

        }

        public void ShowOpenFileDialog()
        {
            show_open_file_dialog = true;
        }

        public override void ProcessModals(object ob, ref string current_file_path, ref bool isDialogOpen)
        {
            //Functionality

            if (show_open_file_dialog)
            {
                ImGui.OpenPopup("open-file");
                show_open_file_dialog = false;
            }

            var winsize = new System.Numerics.Vector2(500, 250);
            ImGui.SetNextWindowSize(winsize);
            if (ImGui.BeginPopupModal("open-file", ref isDialogOpen, ImGuiWindowFlags.NoTitleBar))
            {
                var picker = FilePicker.GetFilePicker(ob, current_file_path, ".dds");
                if (picker.Draw(new System.Numerics.Vector2(winsize.X - 15, winsize.Y - 60)))
                {
                    Console.WriteLine(picker.SelectedFile);
                    current_file_path = picker.SelectedFile;
                    FilePicker.RemoveFilePicker(ob);
                    isDialogOpen = false;
                } 
                ImGui.EndPopup();
            }

        }
    }
}
