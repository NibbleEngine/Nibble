using System;
using System.Collections.Generic;
using System.Linq;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using NbCore;
using NbCore.Common;
using ImGuiCore = ImGuiNET.ImGui;

namespace NbCore.UI.ImGui
{
    public class ImGuiShaderEditor
    {
        private GLSLShaderConfig ActiveShader = null;
        private int selectedShaderId = -1;
        private int selectedVSSource = -1;
        private int selectedFSSource = -1;
        private int selectedGSSource = -1;
        private int selectedTCSSource = -1;
        private int selectedTESSource = -1;
        private bool showSourceEditor = false;
        private ImGuiShaderSourceEditor sourceEditor = new();

        public ImGuiShaderEditor()
        {
            
        }
        
        public void Draw()
        {
            //TODO: Make this static if possible or maybe maintain a list of shaders in the resource manager
            
            //Items
            List<Entity> shaderList = RenderState.engineRef.GetEntityTypeList(EntityType.Shader);
            string[] items = new string[shaderList.Count];
            for (int i = 0; i < items.Length; i++)
            {
                GLSLShaderConfig ss = (GLSLShaderConfig)shaderList[i];
                items[i] = ss.Name;
            }
            
            if (ImGuiCore.Combo("##1", ref selectedShaderId, items, items.Length))
            {
                SetShader((GLSLShaderConfig) shaderList[selectedShaderId]);
            }
            
            ImGuiCore.SameLine();

            if (ImGuiCore.Button("Add"))
            {
                Console.WriteLine("Todo Create Shader");
            }
            ImGuiCore.SameLine();
            if (ImGuiCore.Button("Del"))
            {
                Console.WriteLine("Todo Delete Shader");
            }

            if (ActiveShader is null)
                return;

            bool Updated = false;
            if (ImGuiCore.BeginTable("##ShaderTable", 3))
            {
                //Cache ShaderSources
                List<Entity> shaderSourceList = RenderState.engineRef.GetEntityTypeList(EntityType.ShaderSource);
                string[] sourceItems = new string[shaderSourceList.Count];
                for (int i = 0; i < sourceItems.Length; i++)
                {
                    GLSLShaderSource ss = (GLSLShaderSource)shaderSourceList[i];
                    sourceItems[i] = ss.SourceFilePath;
                }

                int OriginalVSSourceIndex = shaderSourceList.IndexOf(ActiveShader.Sources[NbShaderType.VertexShader]);
                int OriginalFSSourceIndex = shaderSourceList.IndexOf(ActiveShader.Sources[NbShaderType.FragmentShader]);
                
                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("Vertex Shader");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.PushItemWidth(-1);
                ImGuiCore.Combo("##VSCombo", ref selectedVSSource, sourceItems, sourceItems.Length);
                ImGuiCore.PopItemWidth();
                ImGuiCore.TableSetColumnIndex(2);
                if (ImGuiCore.Button("Edit##1"))
                {
                    sourceEditor.SetShader(ActiveShader.Sources[NbShaderType.VertexShader]);
                    showSourceEditor = true;
                }

                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("Fragment Shader");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.PushItemWidth(-1);
                ImGuiCore.Combo("##FSCombo", ref selectedFSSource, sourceItems, sourceItems.Length);
                ImGuiCore.PopItemWidth();
                ImGuiCore.TableSetColumnIndex(2);
                if (ImGuiCore.Button("Edit##2"))
                {
                    sourceEditor.SetShader(ActiveShader.Sources[NbShaderType.VertexShader]);
                    showSourceEditor = true;
                }

                if (OriginalFSSourceIndex != selectedFSSource ||
                    OriginalVSSourceIndex != selectedVSSource)
                {
                    Updated = true;
                }

                ImGuiCore.EndTable();
            }

            if (Updated)
            {
                if (ImGuiCore.Button("Recompile Shader"))
                {
                    Console.WriteLine("Shader recompilation not supported yet");
                }
            }
            
            if (showSourceEditor)
            {
                bool open = true;
                if (ImGuiCore.Begin("Source Editor", ref open, ImGuiNET.ImGuiWindowFlags.NoScrollbar))
                {
                    sourceEditor.Draw();
                    

                    ImGuiCore.End();
                }
            }
            
        }

        public void SetShader(GLSLShaderConfig conf)
        {
            ActiveShader = conf;
            List<Entity> shaderList = RenderState.engineRef.GetEntityTypeList(EntityType.Shader);
            List<Entity> shaderSourceList = RenderState.engineRef.GetEntityTypeList(EntityType.ShaderSource);
            selectedShaderId = shaderList.IndexOf(conf);
            selectedVSSource = shaderSourceList.IndexOf(conf.Sources[NbShaderType.VertexShader]);
            selectedFSSource = shaderSourceList.IndexOf(conf.Sources[NbShaderType.FragmentShader]);
        }
    }
    
    
}