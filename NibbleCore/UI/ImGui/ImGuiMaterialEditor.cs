using System;
using NbCore;
using NbCore.Common;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace NbCore.UI.ImGui
{
    public class ImGuiMaterialEditor
    {
        private NbMaterial _ActiveMaterial = null;
        private static int current_material_flag = 0;
        private static int current_material_sampler = 0;
        private int _SelectedId = -1;


        public void Draw()
        {
            var io = ImGuiNET.ImGui.GetIO();
            //Items
            List<NbMaterial> materialList = RenderState.engineRef.GetSystem<Systems.RenderingSystem>().MaterialMgr.Entities;
            string[] items = new string[materialList.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = materialList[i].Name == "" ? "Material_" + i : materialList[i].Name;

            if (ImGuiNET.ImGui.Combo("##1", ref _SelectedId, items, items.Length))
                _ActiveMaterial = materialList[_SelectedId];

            ImGuiNET.ImGui.SameLine();

            if (ImGuiNET.ImGui.Button("Add"))
            {
                string name = "Material_" + (new Random()).Next(0x1000, 0xFFFF).ToString();
                NbMaterial mat = new();
                mat.Name = name;
                RenderState.engineRef.RegisterEntity(mat);
                SetMaterial(mat);
            }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.Button("Del"))
            {
                NbMaterial mat = _ActiveMaterial;
                SetMaterial(null);
                RenderState.engineRef.DestroyEntity(mat);
            }

            if (_ActiveMaterial is null)
            {
                ImGuiNET.ImGui.Text("NULL");
                return;
            }

            if (ImGuiNET.ImGui.TreeNode("Material Info"))
            {
                if (ImGuiNET.ImGui.BeginTable("##MatInfo", 2, ImGuiTableFlags.Resizable))
                {
                    ImGuiNET.ImGui.TableNextRow();
                    ImGuiNET.ImGui.TableSetColumnIndex(0);
                    ImGuiNET.ImGui.Text("Name");
                    ImGuiNET.ImGui.TableSetColumnIndex(1);
                    ImGuiNET.ImGui.SetNextItemWidth(-1);
                    ImGuiNET.ImGui.InputText("", ref _ActiveMaterial.Name, 30);
                    
                    ImGuiNET.ImGui.TableNextRow();
                    ImGuiNET.ImGui.TableSetColumnIndex(0);
                    ImGuiNET.ImGui.Text("Class");
                    ImGuiNET.ImGui.TableSetColumnIndex(1);
                    ImGuiNET.ImGui.SetNextItemWidth(-1);
                    ImGuiNET.ImGui.Text(_ActiveMaterial.Class);

                    ImGuiNET.ImGui.TableNextRow();
                    ImGuiNET.ImGui.TableSetColumnIndex(0);
                    ImGuiNET.ImGui.Text("Mode");
                    ImGuiNET.ImGui.TableSetColumnIndex(1);
                    ImGuiNET.ImGui.SetNextItemWidth(-1);

                    ImGuiNET.ImGui.Text(_ActiveMaterial.Shader != null ? _ActiveMaterial.Shader.GetShaderConfig().ShaderMode.ToString() : "None");

                    ImGuiNET.ImGui.TableNextRow();
                    ImGuiNET.ImGui.TableSetColumnIndex(0);
                    ImGuiNET.ImGui.Text("Shader");
                    ImGuiNET.ImGui.TableSetColumnIndex(1);
                    ImGuiNET.ImGui.SetNextItemWidth(-1);

                    List<Entity> shaderconfs = RenderState.engineRef.GetEntityTypeList(EntityType.ShaderConfig);
                    string[] shaderconfItems = new string[shaderconfs.Count];
                    
                    for (int i = 0; i < shaderconfs.Count; i++)
                        shaderconfItems[i] = ((GLSLShaderConfig)shaderconfs[i]).Name;
                    
                    int currentShaderConfigId = _ActiveMaterial.Shader != null ? shaderconfs.IndexOf(_ActiveMaterial.Shader.GetShaderConfig()) : -1;
                    if (ImGuiNET.ImGui.Combo("##MaterialShader", ref currentShaderConfigId, shaderconfItems, shaderconfs.Count))
                    {
                        RenderState.engineRef.SetMaterialShader(_ActiveMaterial, shaderconfs[currentShaderConfigId] as GLSLShaderConfig);
                    }

                    ImGuiNET.ImGui.TableNextRow();
                    ImGuiNET.ImGui.TableSetColumnIndex(0);
                    ImGuiNET.ImGui.Text("Shader Hash");
                    ImGuiNET.ImGui.TableSetColumnIndex(1);
                    ImGuiNET.ImGui.SetNextItemWidth(-1);
                    ImGuiNET.ImGui.Text((_ActiveMaterial.Shader != null) ? 
                                         _ActiveMaterial.Shader.Hash.ToString() : "-1");
                    ImGuiNET.ImGui.SameLine();
                    if (ImGuiNET.ImGui.Button("Reload"))
                    {
                        Console.WriteLine("Recompile Shader Here");
                    }
                    
                    ImGuiNET.ImGui.TableNextRow();
                    ImGuiNET.ImGui.TableSetColumnIndex(0);
                    ImGuiNET.ImGui.Text("Flags");
                    ImGuiNET.ImGui.TableSetColumnIndex(1);

                    //Flags
                    //Create string list of flags
                    List<string> flags = new();
                    List<MaterialFlagEnum> mat_flags = _ActiveMaterial.GetFlags();
                    for (int i = 0; i < mat_flags.Count; i++)
                        flags.Add(mat_flags[i].ToString());

                    string[] allflags = Enum.GetNames(typeof(MaterialFlagEnum));
                    
                    //ImGuiNET.ImGui.SetNextItemWidth(-1);
                    ImGuiNET.ImGui.Combo("##FlagSelector", ref current_material_flag, allflags, allflags.Length);
                    ImGuiNET.ImGui.SameLine();

                    //TODO Add combobox here with all the available flags that can be selected and added to the material
                    if (ImGuiNET.ImGui.Button("Add"))
                    {
                        MaterialFlagEnum new_flag = (MaterialFlagEnum) current_material_flag;
                        _ActiveMaterial.AddFlag(new_flag);
                        //Compile a new shader only if a shader exists
                        if (_ActiveMaterial.Shader != null)
                            RenderState.engineRef.SetMaterialShader(_ActiveMaterial, _ActiveMaterial.Shader.GetShaderConfig());
                    }

                    ImGuiNET.ImGui.SetNextItemWidth(-1);
                    if (ImGuiNET.ImGui.BeginListBox("##FlagsListBox"))
                    {
                        foreach (string flag in flags)
                        {
                            ImGuiNET.ImGui.Selectable(flag);

                            if (ImGuiNET.ImGui.BeginPopupContextItem(flag, ImGuiPopupFlags.MouseButtonRight))
                            {
                                if (ImGuiNET.ImGui.MenuItem("Remove ##flag"))
                                {
                                    _ActiveMaterial.RemoveFlag((MaterialFlagEnum)Enum.Parse(typeof(MaterialFlagEnum), flag));

                                    //Compile a new shader only if a shader exists
                                    if (_ActiveMaterial.Shader != null)
                                        RenderState.engineRef.SetMaterialShader(_ActiveMaterial, _ActiveMaterial.Shader.GetShaderConfig());
                                }
                                ImGuiNET.ImGui.EndPopup();
                            }
                        }

                        ImGuiNET.ImGui.EndListBox();
                    }


                    ImGuiNET.ImGui.EndTable();
                }

                ImGuiNET.ImGui.TreePop();
            }

            //Draw using ImGUI
            ImGuiTreeNodeFlags base_flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.AllowItemOverlap;
            
            if (_ActiveMaterial.Samplers.Count == 0)
                base_flags |= ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Leaf;
            
            Vector2 node_rect_pos = ImGuiNET.ImGui.GetCursorPos();
            bool samplers_node_open = ImGuiNET.ImGui.TreeNodeEx("MatSamplers", base_flags, "Samplers");
            Vector2 node_rect_size = ImGuiNET.ImGui.GetItemRectSize();
            float button_width = node_rect_size.Y;
            float button_height = button_width;
            
            if (_ActiveMaterial.Shader != null)
            {
                ImGuiNET.ImGui.SameLine();
                ImGuiNET.ImGui.SetCursorPosX(node_rect_pos.X + node_rect_size.X - button_width);
                ImGuiNET.ImGui.PushFont(io.Fonts.Fonts[1]);
                if (ImGuiNET.ImGui.Button($"+##MatSamplers", new Vector2(button_width, button_height)))
                {
                    Console.WriteLine($"Creating New sampler");
                    NbSampler new_sampler = new();
                    _ActiveMaterial.Samplers.Add(new_sampler);
                }
                ImGuiNET.ImGui.PopFont();
            }

            if (samplers_node_open)
            {
                for (int i = 0; i < _ActiveMaterial.Samplers.Count; i++)
                {
                    NbSampler current_sampler = _ActiveMaterial.Samplers[i];

                    node_rect_pos = ImGuiNET.ImGui.GetCursorPos();
                    bool node_open = ImGuiNET.ImGui.TreeNodeEx(current_sampler.Name + "###Sampler" + i, ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.AllowItemOverlap);
                    node_rect_size = ImGuiNET.ImGui.GetItemRectSize();
                    button_width = node_rect_size.Y;
                    button_height = button_width;
                    ImGuiNET.ImGui.SameLine();
                    ImGuiNET.ImGui.SetCursorPosX(node_rect_pos.X + node_rect_size.X - button_width);
                    ImGuiNET.ImGui.PushFont(io.Fonts.Fonts[1]);
                    if (ImGuiNET.ImGui.Button($"-##Sampler{i}", new Vector2(button_width, button_height)))
                    {
                        Callbacks.Log(this, "Removing Sampler " + current_sampler.Name, LogVerbosityLevel.INFO);
                        _ActiveMaterial.RemoveSampler(current_sampler);
                    }
                    ImGuiNET.ImGui.PopFont();


                    if (node_open)
                    {
                        if (ImGuiNET.ImGui.BeginTable("##SamplerTable"+i, 2))
                        {
                            ImGuiNET.ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.WidthFixed, 120.0f);
                            ImGuiNET.ImGui.TableSetupColumn("Data");
                            //ImGuiNET.ImGui.TableSetColumnWidth(1, -1);

                            //Sampler Name
                            ImGuiNET.ImGui.TableNextRow();
                            ImGuiNET.ImGui.TableSetColumnIndex(0);
                            ImGuiNET.ImGui.Text("Name");
                            ImGuiNET.ImGui.TableSetColumnIndex(1);
                            ImGuiNET.ImGui.SetNextItemWidth(-1);
                            ImGuiNET.ImGui.InputText("##SamplerName" + i, ref current_sampler.Name, 30);

                            //Image Preview
                            ImGuiNET.ImGui.TableNextRow();
                            ImGuiNET.ImGui.TableSetColumnIndex(0);
                            ImGuiNET.ImGui.Text("Preview");
                            ImGuiNET.ImGui.TableSetColumnIndex(1);
                            NbTexture samplerTex = current_sampler.Texture;
                            if (samplerTex is not null && samplerTex.Data.target != NbTextureTarget.Texture2DArray)
                            {
                                ImGuiNET.ImGui.Image((IntPtr)samplerTex.texID, new Vector2(64, 64));

                                if (ImGuiNET.ImGui.IsItemHovered())
                                {
                                    ImGuiNET.ImGui.BeginTooltip();
                                    if (samplerTex.Data.target != NbTextureTarget.Texture2DArray)
                                        ImGuiNET.ImGui.Image((IntPtr)samplerTex.texID, new Vector2(512, 512));
                                    ImGuiNET.ImGui.Text(current_sampler.Name);
                                    ImGuiNET.ImGui.Text(current_sampler.Texture.Path);
                                    ImGuiNET.ImGui.EndTooltip();
                                }
                            }

                            //Texture Selector
                            //Get All Textures
                            List<Entity> textureList = RenderState.engineRef.GetEntityTypeList(EntityType.Texture);
                            string[] textureItems = new string[textureList.Count];
                            for (int j = 0; j < textureItems.Length; j++)
                            {
                                NbTexture tex = (NbTexture)textureList[j];
                                textureItems[j] = tex.Path == "" ? "Texture_" + j : tex.Path;
                            }
                            

                            int currentTexImageID = textureList.IndexOf(samplerTex);
                            ImGuiNET.ImGui.TableNextRow();
                            ImGuiNET.ImGui.TableSetColumnIndex(0);
                            ImGuiNET.ImGui.Text("Texture");
                            ImGuiNET.ImGui.TableSetColumnIndex(1);
                            ImGuiNET.ImGui.SetNextItemWidth(-1);
                            if (ImGuiNET.ImGui.Combo("##SamplerTexture" + i, ref currentTexImageID, textureItems, textureItems.Length))
                            {
                                current_sampler.Texture = (NbTexture) textureList[currentTexImageID];
                            }

                            if (samplerTex != null)
                            {
                                //Sampler ID
                                ImGuiNET.ImGui.TableNextRow();
                                ImGuiNET.ImGui.TableSetColumnIndex(0);
                                ImGuiNET.ImGui.Text("Sampler ID");
                                ImGuiNET.ImGui.TableSetColumnIndex(1);
                                ImGuiNET.ImGui.SetNextItemWidth(-1);
                                ImGuiNET.ImGui.Combo("##SamplerID" + i, ref current_sampler.SamplerID,
                                    new string[] { "0", "1", "2", "3", "4", "5", "6", "7" }, 8);
                            }
                                
                            if (_ActiveMaterial.Shader != null && samplerTex != null)
                            {
                                //Sampler Shader Binding
                                List<string> compatibleShaderBindings = new();
                                foreach (var pair in _ActiveMaterial.Shader.uniformLocations)
                                {
                                    if (pair.Value.type == NbUniformType.Sampler2D)
                                        compatibleShaderBindings.Add(pair.Key);
                                }

                                int currentShaderBinding = compatibleShaderBindings.IndexOf(current_sampler.ShaderBinding);
                                ImGuiNET.ImGui.TableNextRow();
                                ImGuiNET.ImGui.TableSetColumnIndex(0);
                                ImGuiNET.ImGui.Text("Shader Binding");
                                ImGuiNET.ImGui.TableSetColumnIndex(1);
                                ImGuiNET.ImGui.SetNextItemWidth(-1);
                                if (ImGuiNET.ImGui.Combo("##SamplerBinding", ref currentShaderBinding, compatibleShaderBindings.ToArray(),
                                    compatibleShaderBindings.Count))
                                {

                                    Console.WriteLine("Change sampler shader binding");
                                    current_sampler.ShaderBinding = compatibleShaderBindings[currentShaderBinding];
                                    _ActiveMaterial.UpdateSampler(current_sampler);
                                }
                            }


                            ImGuiNET.ImGui.EndTable();
                        }

                        ImGuiNET.ImGui.TreePop();
                    }
                    
                }

                ImGuiNET.ImGui.TreePop();
            }

            base_flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.AllowItemOverlap;
            if (_ActiveMaterial.Uniforms.Count == 0)
                base_flags |= ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Leaf;

            node_rect_pos = ImGuiNET.ImGui.GetCursorPos();
            bool mat_uniform_tree_open = ImGuiNET.ImGui.TreeNodeEx("MatUniforms", base_flags, "Uniforms");
            node_rect_size = ImGuiNET.ImGui.GetItemRectSize();
            button_width = node_rect_size.Y;
            button_height = button_width;
            
            
            if (_ActiveMaterial.Shader != null)
            {
                ImGuiNET.ImGui.SameLine();
                ImGuiNET.ImGui.SetCursorPosX(node_rect_pos.X + node_rect_size.X - button_width);
                ImGuiNET.ImGui.PushFont(io.Fonts.Fonts[1]);

                if (ImGuiNET.ImGui.Button($"+##MatUniforms", new Vector2(button_width, button_height)))
                {
                    Console.WriteLine($"Creating New Uniform");
                    NbUniform uf = new();
                    _ActiveMaterial.Uniforms.Add(uf);
                }
                ImGuiNET.ImGui.PopFont();
            }
            
            if (mat_uniform_tree_open)
            {
                for (int i = 0; i < _ActiveMaterial.Uniforms.Count; i++)
                {
                    NbUniform current_uf = _ActiveMaterial.Uniforms[i];

                    
                    node_rect_pos = ImGuiNET.ImGui.GetCursorPos();
                    bool node_open = ImGuiNET.ImGui.TreeNodeEx(current_uf.Name + "###Uniform" + i, ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.AllowItemOverlap);
                    node_rect_size = ImGuiNET.ImGui.GetItemRectSize();
                    button_width = node_rect_size.Y;
                    button_height = button_width;
                    ImGuiNET.ImGui.SameLine();
                    ImGuiNET.ImGui.SetCursorPosX(node_rect_pos.X + node_rect_size.X - button_width);
                    ImGuiNET.ImGui.PushFont(io.Fonts.Fonts[1]);
                    if (ImGuiNET.ImGui.Button("-##Uniform{i}", new Vector2(button_width, button_height)))
                    {
                        Callbacks.Log(this, "Removing Uniform " + current_uf.Name, LogVerbosityLevel.INFO);
                        _ActiveMaterial.RemoveUniform(_ActiveMaterial.Uniforms[i]);
                    }
                    ImGuiNET.ImGui.PopFont();
                    

                    if (node_open)
                    {
                        if (ImGuiNET.ImGui.BeginTable("##UniformTable" + i, 2))
                        {
                            ImGuiNET.ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.WidthFixed, 120.0f);
                            ImGuiNET.ImGui.TableSetupColumn("Data");
                            //ImGuiNET.ImGui.TableSetColumnWidth(1, -1);

                            //Name
                            ImGuiNET.ImGui.TableNextRow();
                            ImGuiNET.ImGui.TableSetColumnIndex(0);
                            ImGuiNET.ImGui.Text("Name");
                            ImGuiNET.ImGui.TableSetColumnIndex(1);
                            ImGuiNET.ImGui.SetNextItemWidth(-1);
                            ImGuiNET.ImGui.InputText("Name##Uniform" + i, ref current_uf.Name, 30);
                            
                            //Format
                            ImGuiNET.ImGui.TableNextRow();
                            ImGuiNET.ImGui.TableSetColumnIndex(0);
                            ImGuiNET.ImGui.Text("Format");
                            ImGuiNET.ImGui.TableSetColumnIndex(1);
                            ImGuiNET.ImGui.SetNextItemWidth(-1);

                            List<string> formats = Enum.GetNames(typeof(NbUniformType)).ToList();
                            int currentFormat = formats.IndexOf(current_uf.State.Type.ToString());
                            if (ImGuiNET.ImGui.Combo("##UniformFormat" + i, ref currentFormat, formats.ToArray(), formats.Count))
                            {
                                current_uf.State.Type = (NbUniformType)currentFormat;
                                current_uf.State.ShaderBinding = "";
                                current_uf.State.ShaderLocation = -1;
                                _ActiveMaterial.ActiveUniforms.Remove(current_uf);
                            }

                            //Values
                            ImGuiNET.ImGui.TableNextRow();
                            ImGuiNET.ImGui.TableSetColumnIndex(0);
                            ImGuiNET.ImGui.Text("Values");
                            ImGuiNET.ImGui.TableSetColumnIndex(1);
                            ImGuiNET.ImGui.SetNextItemWidth(-1);

                            Math.NbVector4 vec = new(current_uf.Values);
                            switch (current_uf.State.Type)
                            {
                                case NbUniformType.Float:
                                    {
                                        float val = vec.X;
                                        if (ImGuiNET.ImGui.DragFloat("##UniformValues" + i, ref val, 0.001f))
                                            vec.X = val;
                                        break;
                                    }
                                case NbUniformType.Vector2:
                                    {
                                        Vector2 val = new(vec.X, vec.Y);
                                        if (ImGuiNET.ImGui.DragFloat2("##UniformValues" + i, ref val, 0.001f))
                                        {
                                            vec.X = val.X;
                                            vec.Y = val.Y;
                                        }
                                        break;
                                    }
                                case NbUniformType.Vector3:
                                    {
                                        Vector3 val = new(vec.X, vec.Y, vec.Z);
                                        if (ImGuiNET.ImGui.DragFloat3("##UniformValues" + i, ref val, 0.01f))
                                        {
                                            vec.X = val.X;
                                            vec.Y = val.Y;
                                            vec.Z = val.Z;
                                        }
                                        break;
                                    }
                                case NbUniformType.Vector4:
                                    {
                                        Vector4 val = new(vec.X, vec.Y, vec.Z, vec.W);
                                        if (ImGuiNET.ImGui.DragFloat4("##UniformValues" + i, ref val, 0.01f))
                                        {
                                            vec.X = val.X;
                                            vec.Y = val.Y;
                                            vec.Z = val.Z;
                                            vec.W = val.W;
                                        }
                                        break;
                                    }
                            }

                            current_uf.Values = vec;


                            //Sampler Shader Binding
                            List<string> compatibleShaderBindings = new();
                            foreach (var pair in _ActiveMaterial.Shader.uniformLocations)
                            {
                                if (pair.Value.type == current_uf.State.Type)
                                    compatibleShaderBindings.Add(pair.Key);
                            }

                            int currentShaderBinding = compatibleShaderBindings.IndexOf(current_uf.State.ShaderBinding);
                            ImGuiNET.ImGui.TableNextRow();
                            ImGuiNET.ImGui.TableSetColumnIndex(0);
                            ImGuiNET.ImGui.Text("Shader Binding");
                            ImGuiNET.ImGui.TableSetColumnIndex(1);
                            ImGuiNET.ImGui.SetNextItemWidth(-1);
                            if (ImGuiNET.ImGui.Combo("##SamplerBinding", ref currentShaderBinding, compatibleShaderBindings.ToArray(),
                                compatibleShaderBindings.Count))
                            {
                                current_uf.State.ShaderBinding = compatibleShaderBindings[currentShaderBinding];
                                current_uf.State.ShaderLocation = _ActiveMaterial.Shader.uniformLocations[compatibleShaderBindings[currentShaderBinding]].loc;
                                if (!_ActiveMaterial.ActiveUniforms.Contains(current_uf))
                                    _ActiveMaterial.ActiveUniforms.Add(current_uf);
                            }

                            ImGuiNET.ImGui.EndTable();
                        }
                        
                        ImGuiNET.ImGui.TreePop();
                    }
                }
                ImGuiNET.ImGui.TreePop();
            }
        }

        public void SetMaterial(NbMaterial mat)
        {
            _ActiveMaterial = mat;
            List<NbMaterial> materialList = RenderState.engineRef.GetSystem<Systems.RenderingSystem>().MaterialMgr.Entities;
            _SelectedId = materialList.IndexOf(mat);
        }
    }
}
