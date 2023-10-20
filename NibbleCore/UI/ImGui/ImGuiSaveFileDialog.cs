using ImGuiCore = ImGuiNET.ImGui;
using System;
using System.Collections.Generic;
using System.IO;
using Num = System.Numerics;
using System.Numerics;

namespace NbCore.UI.ImGui
{
    public class SaveFileDialog
    {

        static readonly Num.Vector4 YELLOW_TEXT_COLOR = new Num.Vector4(1.0f, 1.0f, 0.0f, 1.0f);

        private string _uid;
        private FilePicker filePicker = null;
        public bool IsOpen = false;
        private bool show_save_file_dialog = false;
        private string[] save_formats;
        private string[] save_formats_ext;
        private string save_file_name = "";
        private int save_file_extention_id = 0;
        public ImGuiSelectFileTriggerEventHandler OnFileSelect = null;

        public SaveFileDialog(string uid, string[] saveFormats, string[] saveFormatExtensions)
        {
            _uid = uid;
            filePicker = new();
            filePicker.SelectedFile = "";
            filePicker.OnlyAllowFolders = true;
            save_formats = saveFormats;
            save_formats_ext = saveFormatExtensions;
        }

        private void DrawDirectoryFiles(DirectoryInfo di)
        {
            var fileSystemEntries = GetFileSystemEntries(di.FullName);
            foreach (var fse in fileSystemEntries)
            {
                if (Directory.Exists(fse))
                {
                    var name = Path.GetFileName(fse);
                    ImGuiCore.PushStyleColor(ImGuiNET.ImGuiCol.Text, YELLOW_TEXT_COLOR);
                    if (ImGuiCore.Selectable(name + "/", false, ImGuiNET.ImGuiSelectableFlags.DontClosePopups))
                        filePicker.CurrentFolder = fse;
                    ImGuiCore.PopStyleColor();
                }
                else
                {
                    var name = Path.GetFileName(fse);
                    bool isSelected = filePicker.SelectedFile == fse;
                    if (ImGuiCore.Selectable(name, isSelected, ImGuiNET.ImGuiSelectableFlags.DontClosePopups) || ImGuiCore.IsMouseDoubleClicked(0))
                        filePicker.SelectedFile = fse;
                }
            }

        }

        public void Open()
        {
            filePicker.SelectedFile = "";
            save_file_name = "";
            show_save_file_dialog = true;
        }

        private void Close()
        {
            ImGuiCore.CloseCurrentPopup();
        }

        public string GetSaveFilePath()
        {
            return Path.Join(filePicker.SelectedFile, save_file_name.Split(".")[0] + "." + save_formats_ext[save_file_extention_id]);
        }
        
        public string GetSelectedFormat()
        {
            return save_formats[save_file_extention_id];
        }

        public bool Draw(Num.Vector2 winsize)
        {
            if (show_save_file_dialog)
            {
                ImGuiCore.OpenPopup(_uid);
                show_save_file_dialog = false;
            }

            bool isopen = true;
            ImGuiCore.SetNextWindowSizeConstraints(new(600, 300), new(1000, 1000));
            if (ImGuiCore.BeginPopupModal(_uid, ref isopen, ImGuiNET.ImGuiWindowFlags.None))
            {
                if (ImGuiCore.IsKeyPressed(ImGuiNET.ImGuiKey.Escape))
                    Close();

                if (filePicker.CurrentFolder == null)
                {
                    ImGuiCore.Text("My Computer");
                    if (ImGuiCore.BeginChildFrame(1, winsize))
                    {
                        //Draw Drives
                        var driveList = DriveInfo.GetDrives();
                        foreach (var de in driveList)
                        {
                            if (Directory.Exists(de.RootDirectory.FullName))
                            {
                                var name = de.RootDirectory.FullName;
                                ImGuiCore.PushStyleColor(ImGuiNET.ImGuiCol.Text, YELLOW_TEXT_COLOR);
                                if (ImGuiCore.Selectable(name, false, ImGuiNET.ImGuiSelectableFlags.DontClosePopups))
                                    filePicker.CurrentFolder = de.RootDirectory.FullName;
                                ImGuiCore.PopStyleColor();
                            }
                        }
                        ImGuiCore.EndChildFrame();
                    }

                    if (ImGuiCore.Button("Cancel"))
                    {
                        Close();
                    }

                    ImGuiCore.EndPopup();
                    return false;
                }

                ImGuiCore.Text("Current Path: " + filePicker.CurrentFolder);
                
                var current_size = ImGuiCore.GetWindowSize();
                current_size -= new Num.Vector2(0, 100);
                if (ImGuiCore.BeginChildFrame(1, current_size))
                {
                    var di = new DirectoryInfo(filePicker.CurrentFolder);
                    if (di.Exists)
                    {
                        if (ImGuiCore.Selectable("../", false, ImGuiNET.ImGuiSelectableFlags.DontClosePopups))
                        {
                            if (di.Parent != null)
                            {
                                filePicker.CurrentFolder = di.Parent.FullName;
                                DrawDirectoryFiles(di.Parent);
                            }
                            else
                            {
                                filePicker.CurrentFolder = null;
                            }
                        }
                        else
                        {
                            DrawDirectoryFiles(di);
                        }
                    }

                    ImGuiCore.EndChildFrame();
                }

                var pos = ImGuiCore.GetCursorPos();
                ImGuiCore.LabelText("##SaveFileNameLabel", "Filename");
                ImGuiCore.SameLine();
                ImGuiCore.SetCursorPos(new(pos.X + 80, pos.Y));
                ImGuiCore.SetNextItemWidth(current_size.X - 246);
                ImGuiCore.InputText("##" + _uid + "_finalPath", ref save_file_name, 300);
                
                ImGuiCore.SameLine();
                ImGuiCore.SetCursorPos(new(current_size.X - 152, pos.Y));
                ImGuiCore.SetNextItemWidth(100);
                ImGuiCore.Combo("##" + _uid + "_saveFileFormat", ref save_file_extention_id, save_formats, save_formats.Length);
                
                ImGuiCore.SameLine();
                if (ImGuiCore.Button("Save"))
                {
                    //Construct file path
                    string ext = save_formats_ext[save_file_extention_id];
                    filePicker.SelectedFile = Path.Combine(filePicker.CurrentFolder, 
                        save_file_name.Replace(ext, "") + ext);
                    OnFileSelect?.Invoke(filePicker.SelectedFile);
                    Close();
                    return true;
                }

                ImGuiCore.EndPopup();
            }

            return false;

        }

        public void SetDialogPath(string path)
        {
            filePicker.CurrentFolder = path;
        }

        bool TryGetFileInfo(string fileName, out FileInfo realFile)
        {
            try
            {
                realFile = new FileInfo(fileName);
                return true;
            }
            catch
            {
                realFile = null;
                return false;
            }
        }

        List<string> GetFileSystemEntries(string fullName)
        {
            var files = new List<string>();
            var dirs = new List<string>();

            foreach (var fse in Directory.GetFileSystemEntries(fullName, ""))
            {
                if (Directory.Exists(fse))
                {
                    dirs.Add(fse);
                }
                else if (!filePicker.OnlyAllowFolders)
                {
                    if (filePicker.AllowedExtensions != null)
                    {
                        foreach (string ext in filePicker.AllowedExtensions)
                        {
                            if (fse.ToLower().EndsWith(ext.ToLower()))
                                files.Add(fse);
                        }
                    }
                    else
                    {
                        files.Add(fse);
                    }
                }
            }

            var ret = new List<string>(dirs);
            ret.AddRange(files);

            return ret;
        }

    }
}