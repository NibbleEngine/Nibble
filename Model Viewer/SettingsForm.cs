﻿using System;
using System.Windows.Forms;
using System.Xml;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Diagnostics;
using RenderState = MVCore.Common.RenderState;


namespace Model_Viewer
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //Save Settings
            XmlDocument xml = new XmlDocument();
            XmlElement settings = xml.CreateElement("SETTINGS");
            XmlElement elem;
            //Gamepath
            elem = xml.CreateElement("GAMEPATH");
            elem.InnerText = textBox1.Text;
            settings.AppendChild(elem);
            //ProcGenNum
            elem = xml.CreateElement("PROCGENNUM");
            elem.InnerText = procGenNum.Value.ToString();
            settings.AppendChild(elem);
            xml.AppendChild(settings);
            //Force ProcGEN
            elem = xml.CreateElement("FORCEPROCGEN");
            elem.InnerText = forceProcGen.Value.ToString();
            settings.AppendChild(elem);
            xml.AppendChild(settings);

            //Save
            xml.Save("settings.xml");
        }

        public void loadSettings()
        {
            XmlDocument xml = new XmlDocument();
            try
            {
                xml.Load("settings.xml");
            }
            catch (System.IO.FileNotFoundException)
            {
                DialogResult res = MessageBox.Show("Settings File Missing. Please choose your unpacked files folder...", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (res == DialogResult.OK)
                {
                    System.Diagnostics.Debug.WriteLine("Settings File doesn't Exist");
                    textBox1_MouseClick(this, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 1));
                    //Set procGen Num explicitly
                    Util.procGenNum = 15;
                    RenderState.forceProcGen = true;
                    //Save Settings automatically
                    if (!(textBox1.Text == ""))
                        button1_Click(this, new EventArgs());
                    
                    return;
                }
            }

            //If file exists then parse the settings
            
            
            XmlElement settings = (XmlElement) xml.SelectSingleNode("SETTINGS");
            XmlElement gamepath = (XmlElement) settings.SelectSingleNode("GAMEPATH");
            XmlElement pGenNum = (XmlElement)settings.SelectSingleNode("PROCGENNUM");
            XmlElement forcepGen = (XmlElement)settings.SelectSingleNode("FORCEPROCGEN");

            //Set Gamepath
            textBox1.Text = gamepath.InnerText;
            MVCore.FileUtils.dirpath = gamepath.InnerText;
            //Set ProcGenNum
            procGenNum.Value = int.Parse(pGenNum.InnerText);
            forceProcGen.Value = int.Parse(forcepGen.InnerText);
            Util.procGenNum = (int) procGenNum.Value;
            RenderState.forceProcGen = ((int) forceProcGen.Value >0) ? true : false ;

            System.Diagnostics.Debug.WriteLine(gamepath.InnerText);
        
        }

        private void textBox1_MouseClick(object sender, MouseEventArgs e)
        {
            System.Windows.Forms.DialogResult res =  folderBrowserDialog1.ShowDialog();

            if (res == DialogResult.Cancel) return;

            //Set Gamepath
            textBox1.Text = folderBrowserDialog1.SelectedPath;
            MVCore.FileUtils.dirpath = folderBrowserDialog1.SelectedPath;
        }

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void procGenNum_ValueChanged(object sender, EventArgs e)
        {
            Util.procGenNum = (int) procGenNum.Value;
        }
    }
}
