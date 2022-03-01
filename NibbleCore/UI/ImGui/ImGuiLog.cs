using System;
using System.Collections.Generic;
using NbCore.Common;
using ImGuiCore = ImGuiNET.ImGui;


namespace NbCore.UI.ImGui
{
    public class ImGuiLog
    {
        private System.Numerics.Vector4 sys_col = new(1.0f, 1.0f, 0.0f, 1.0f);
        private System.Numerics.Vector4 type_col = new(0.0f, 1.0f, 0.0f, 1.0f);
        private System.Numerics.Vector4 msg_col = new(1.0f, 1.0f, 1.0f, 1.0f);

        bool Autoscroll = true;
        List<LogElement> LineBuffer = new();

        public ImGuiLog()
        {
            Autoscroll = true;
            Clear();
        }

        public void Clear()
        {
            LineBuffer.Clear();
        }

        public void AddLog(LogElement el)
        {
            LineBuffer.Add(el);
        }

        public void Draw()
        {
            if (ImGuiCore.BeginChild("Scrolling", new System.Numerics.Vector2(0,0), false, ImGuiNET.ImGuiWindowFlags.HorizontalScrollbar))
            {
                int i = 0;
                while ( i < LineBuffer.Count)
                {
                    ImGuiCore.TextColored(sys_col, LineBuffer[i].sender);
                    ImGuiCore.SameLine();
                    ImGuiCore.TextColored(type_col, LineBuffer[i].type);
                    ImGuiCore.SameLine();
                    ImGuiCore.TextColored(msg_col, LineBuffer[i].message);
                    i++;
                }

                if (Autoscroll && ImGuiCore.GetScrollY() >= ImGuiCore.GetScrollMaxY())
                    ImGuiCore.SetScrollHereY(1.0f);
                
                ImGuiCore.EndChild();
            }

        }


    }
}
