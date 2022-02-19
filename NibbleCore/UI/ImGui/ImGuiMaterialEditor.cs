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
        private MeshMaterial _ActiveMaterial = null;
        private static int current_material_flag = 0;
        private static int current_material_sampler = 0;
        private int _SelectedId = -1;


        static void HelpMarker(string desc)
        {
            ImGuiNET.ImGui.TextDisabled("(?)");
            if (ImGuiNET.ImGui.IsItemHovered())
            {
                ImGuiNET.ImGui.BeginTooltip();
                ImGuiNET.ImGui.PushTextWrapPos(ImGuiNET.ImGui.GetFontSize()* 35.0f);
                ImGuiNET.ImGui.TextUnformatted(desc);
                ImGuiNET.ImGui.PopTextWrapPos();
                ImGuiNET.ImGui.EndTooltip();
            }
        }


        public void Draw()
        {
            //Items
            List<MeshMaterial> materialList = RenderState.engineRef.renderSys.MaterialMgr.Entities;
            string[] items = new string[materialList.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = materialList[i].Name == "" ? "Material_" + i : materialList[i].Name;


            if (ImGuiNET.ImGui.Combo("##1", ref _SelectedId, items, items.Length))
                _ActiveMaterial = materialList[_SelectedId];

            ImGuiNET.ImGui.SameLine();

            if (ImGuiNET.ImGui.Button("Add"))
            {
                string name = "Material_" + (new Random()).Next(0x1000, 0xFFFF).ToString();
                MeshMaterial mat = new();
                mat.Name = name;
                RenderState.engineRef.RegisterEntity(mat);
                SetMaterial(mat);
            }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.Button("Del"))
            {
                MeshMaterial mat = _ActiveMaterial;
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
                    ImGuiNET.ImGui.Text("Shader");
                    ImGuiNET.ImGui.TableSetColumnIndex(1);
                    ImGuiNET.ImGui.SetNextItemWidth(-1);

                    List<Entity> shaderconfs = RenderState.engineRef.GetEntityTypeList(EntityType.ShaderConfig);
                    string[] shaderconfItems = new string[shaderconfs.Count];
                    for (int i = 0; i < shaderconfs.Count; i++)
                    {
                        shaderconfItems[i] = ((GLSLShaderConfig)shaderconfs[i]).Name;
                    }
                    int currentShaderConfigId = shaderconfs.IndexOf(_ActiveMaterial.ShaderConfig);
                    if (ImGuiNET.ImGui.Combo("##MaterialShader", ref currentShaderConfigId, shaderconfItems, shaderconfs.Count))
                    {
                        GLSLShaderConfig old = _ActiveMaterial.ShaderConfig;
                        _ActiveMaterial.ShaderConfig = shaderconfs[currentShaderConfigId] as GLSLShaderConfig;
                        NbShader new_shader = RenderState.engineRef.CompileMaterialShader(_ActiveMaterial);

                        if (new_shader == null)
                        {
                            Console.WriteLine("Unable to compile shader for material");
                            _ActiveMaterial.ShaderConfig = old;
                        }
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
                    for (int i = 0; i < _ActiveMaterial.Flags.Count; i++)
                        flags.Add(_ActiveMaterial.Flags[i].ToString());

                    string[] allflags = Enum.GetNames(typeof(MaterialFlagEnum));
                    
                    //ImGuiNET.ImGui.SetNextItemWidth(-1);
                    ImGuiNET.ImGui.Combo("##FlagSelector", ref current_material_flag, allflags, allflags.Length);
                    ImGuiNET.ImGui.SameLine();

                    //TODO Add combobox here with all the available flags that can be selected and added to the material
                    if (ImGuiNET.ImGui.Button("Add"))
                    {
                        MaterialFlagEnum new_flag = (MaterialFlagEnum) current_material_flag;
                        if (!_ActiveMaterial.Flags.Contains(new_flag))
                            _ActiveMaterial.Flags.Add(new_flag);
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
                                    _ActiveMaterial.Flags.Remove((MaterialFlagEnum) Enum.Parse(typeof(MaterialFlagEnum), flag));
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

            if (ImGuiNET.ImGui.TreeNode("Material Samplers"))
            {
                if (ImGuiNET.ImGui.BeginPopupContextItem("SamplerTableCTX", ImGuiPopupFlags.MouseButtonRight))
                {
                    if (ImGuiNET.ImGui.MenuItem("Add Sampler ##flag"))
                    {
                        Console.WriteLine($"Creating New sampler");
                        NbSampler new_sampler = new();
                        _ActiveMaterial.Samplers.Add(new_sampler);
                    }
                    ImGuiNET.ImGui.EndPopup();
                }

                for (int i = 0; i < _ActiveMaterial.Samplers.Count; i++)
                {
                    NbSampler current_sampler = _ActiveMaterial.Samplers[i];

                    if (ImGuiNET.ImGui.TreeNode("Sampler " + i))
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
                            NbTexture samplerTex = current_sampler.GetTexture();
                            if (samplerTex is not null && samplerTex.Data.target != NbTextureTarget.Texture2DArray)
                            {
                                ImGuiNET.ImGui.Image((IntPtr)samplerTex.texID, new Vector2(64, 64));

                                if (ImGuiNET.ImGui.IsItemHovered())
                                {
                                    ImGuiNET.ImGui.BeginTooltip();
                                    if (samplerTex.Data.target != NbTextureTarget.Texture2DArray)
                                        ImGuiNET.ImGui.Image((IntPtr)samplerTex.texID, new Vector2(512, 512));
                                    ImGuiNET.ImGui.Text(current_sampler.Name);
                                    ImGuiNET.ImGui.Text(current_sampler.Map);
                                    ImGuiNET.ImGui.EndTooltip();
                                }
                            }

                            //Texture Selector
                            //Get All Textures
                            List<NbTexture> textureList = RenderState.engineRef.renderSys.TextureMgr.Entities;
                            string[] textureItems = new string[textureList.Count];
                            for (int j = 0; j < textureItems.Length; j++)
                                textureItems[j] = textureList[j].Path == "" ? "Texture_" + j : textureList[j].Path;

                            int currentTexImageID = textureList.IndexOf(samplerTex);
                            ImGuiNET.ImGui.TableNextRow();
                            ImGuiNET.ImGui.TableSetColumnIndex(0);
                            ImGuiNET.ImGui.Text("Sampler Texture");
                            ImGuiNET.ImGui.TableSetColumnIndex(1);
                            ImGuiNET.ImGui.SetNextItemWidth(-1);
                            if (ImGuiNET.ImGui.Combo("##SamplerTexture" + i, ref currentTexImageID, textureItems, textureItems.Length))
                            {
                                current_sampler.SetTexture(textureList[currentTexImageID]);
                            }

                            if (samplerTex != null)
                            {
                                //Sampler ID
                                ImGuiNET.ImGui.TableNextRow();
                                ImGuiNET.ImGui.TableSetColumnIndex(0);
                                ImGuiNET.ImGui.Text("Sampler ID");
                                ImGuiNET.ImGui.TableSetColumnIndex(1);
                                ImGuiNET.ImGui.SetNextItemWidth(-1);
                                ImGuiNET.ImGui.Combo("##SamplerID" + i, ref current_sampler.State.SamplerID,
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

                                int currentShaderBinding = compatibleShaderBindings.IndexOf(current_sampler.State.ShaderBinding);
                                ImGuiNET.ImGui.TableNextRow();
                                ImGuiNET.ImGui.TableSetColumnIndex(0);
                                ImGuiNET.ImGui.Text("Shader Binding");
                                ImGuiNET.ImGui.TableSetColumnIndex(1);
                                ImGuiNET.ImGui.SetNextItemWidth(-1);
                                if (ImGuiNET.ImGui.Combo("##SamplerBinding", ref currentShaderBinding, compatibleShaderBindings.ToArray(),
                                    compatibleShaderBindings.Count))
                                {

                                    Console.WriteLine("Change sampler shader binding");
                                    current_sampler.State.ShaderBinding = compatibleShaderBindings[currentShaderBinding];
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

            if (ImGuiNET.ImGui.TreeNode("Material Uniforms"))
            {
                if (ImGuiNET.ImGui.BeginPopupContextItem("UniformTableCTX", ImGuiPopupFlags.MouseButtonRight))
                {
                    if (ImGuiNET.ImGui.MenuItem("Add Uniform ##flag"))
                    {
                        Console.WriteLine($"Creating New uniform");
                        NbUniform new_uniform = new();
                        _ActiveMaterial.Uniforms.Add(new_uniform);
                    }
                    ImGuiNET.ImGui.EndPopup();
                }

                for (int i = 0; i < _ActiveMaterial.Uniforms.Count; i++)
                {
                    NbUniform current_uf = _ActiveMaterial.Uniforms[i];

                    if (ImGuiNET.ImGui.TreeNode(current_uf.Name + "##" + i))
                    {
                        if (ImGuiNET.ImGui.BeginTable("##SamplerTable" + i, 2))
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
                            string temp_name = current_uf.Name;
                            if (ImGuiNET.ImGui.InputText("UniformName" + i, ref temp_name, 30,
                                ImGuiInputTextFlags.EnterReturnsTrue))
                            {
                                current_uf.Name = temp_name;
                            };
                            
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

                            NbCore.Math.NbVector4 vec = new(current_uf.Values);
                            switch (current_uf.State.Type)
                            {
                                case NbUniformType.Float:
                                    {
                                        float val = vec.X;
                                        if (ImGuiNET.ImGui.InputFloat("##UniformValues" + i, ref val))
                                            vec.X = val;
                                        break;
                                    }
                                case NbUniformType.Vector2:
                                    {
                                        Vector2 val = new(vec.X, vec.Y);
                                        if (ImGuiNET.ImGui.InputFloat2("##UniformValues" + i, ref val))
                                        {
                                            vec.X = val.X;
                                            vec.Y = val.Y;
                                        }
                                        break;
                                    }
                                case NbUniformType.Vector3:
                                    {
                                        Vector3 val = new(vec.X, vec.Y, vec.Z);
                                        if (ImGuiNET.ImGui.InputFloat3("##UniformValues" + i, ref val))
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
                                        if (ImGuiNET.ImGui.InputFloat4("##UniformValues" + i, ref val))
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

        public void SetMaterial(MeshMaterial mat)
        {
            _ActiveMaterial = mat;
            List<MeshMaterial> materialList = RenderState.engineRef.renderSys.MaterialMgr.Entities;
            _SelectedId = materialList.IndexOf(mat);
        }
    }
}
