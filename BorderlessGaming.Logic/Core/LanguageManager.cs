﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using BorderlessGaming.Logic.Models;
using BorderlessGaming.Logic.System;

namespace BorderlessGaming.Logic.Core
{
    public class LanguageManager
    {
        public static string CurrentCulture { get; set; }

        private static readonly HashSet<string> CultureNames = CreateCultureNames();

        private static readonly string _archiveName = "Languages.zip";

        private static Dictionary<string, Language> Languages { get; set; }

        private static HashSet<string> CreateCultureNames()
        {
            var cultureInfos = CultureInfo.GetCultures(CultureTypes.AllCultures)
                .Where(x => !string.IsNullOrEmpty(x.Name))
                .ToArray();
            var allNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            allNames.UnionWith(cultureInfos.Select(x => x.TwoLetterISOLanguageName));
            allNames.UnionWith(cultureInfos.Select(x => x.Name));
            return allNames;
        }

        public static void Translate(Control.ControlCollection controls)
        {
            foreach (Control control in controls.OfType<Control>())
            {
                Translate(control);
            }
        }

        public static void Translate(Control control)
        {
            if (control == null)
            {
                return;
            }

            string data = Data(control.Name);
            if (!string.IsNullOrWhiteSpace(data))
            {
                control.Text = data;
            }

            foreach (Control c in control.Controls)
            {
                Translate(c);
                Translate(c.ContextMenuStrip);
            }

            ToolStrip toolStrip = control as ToolStrip;
            if (toolStrip != null)
            {
                foreach (ToolStripItem t in toolStrip.Items)
                {
                    Translate(t);
                }
            }
        }

        public static void Translate(ContextMenuStrip contextMenuStrip)
        {
            if (contextMenuStrip != null)
            {
                foreach (ToolStripItem t in contextMenuStrip.Items)
                {
                    Translate(t);
                }
            }
        }

        public static void Translate(ToolStripItem toolStripItem)
        {
            string data = Data(toolStripItem.Name);
            if (!string.IsNullOrWhiteSpace(data))
            {
                toolStripItem.Text = data;
            }

            ToolStripMenuItem toolStripMenuItem = toolStripItem as ToolStripMenuItem;
            if (toolStripMenuItem != null)
            {
                foreach (ToolStripItem t in toolStripMenuItem.DropDownItems)
                {
                    Translate(t);
                }
            }
        }

        private static bool CultureExists(string name)
        {
            return CultureNames.Contains(name);
        }

        public static string Data(string key)
        {
            key = key.ToLower();
            var lang = Languages[CurrentCulture];
            var data = lang.Data(key);
            if (string.IsNullOrWhiteSpace(data))
            {
                // TODO log missing translation, instead of closing the application
                //MessageBox.Show($"{lang.Culture} is missing a translation for {key}");
                //Environment.Exit(0);
            }
            return data;
        }

        public static void Load()
        {
            Languages = new Dictionary<string, Language>();
            if (File.Exists(_archiveName))
            {
                try
                {
                    if (Directory.Exists(AppEnvironment.LanguagePath))
                    {
                        Directory.Delete(AppEnvironment.LanguagePath, true);
                        Directory.CreateDirectory(AppEnvironment.LanguagePath);
                    }
                    Tools.ExtractZipFile(_archiveName, string.Empty, AppEnvironment.LanguagePath);
                    File.Delete(_archiveName);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Failed to extract the language pack. Please report this: " + e.Message);
                    Environment.Exit(1);
                }
            }
            if (!Directory.Exists(AppEnvironment.LanguagePath))
            {
                MessageBox.Show("UI Translations are missing from disk.");
                Environment.Exit(1);
            }
            foreach (var langFile in Directory.GetFiles(AppEnvironment.LanguagePath, "*.lang"))
            {
                var culture = Path.GetFileNameWithoutExtension(langFile);
                if (culture != null && CultureExists(culture) && !Languages.ContainsKey(culture))
                {
                    var lang = new Language {Culture = culture};
                    lang.LoadData(langFile);
                    if (lang.LanguageData != null)
                    {
                        Languages.Add(culture, lang);
                    }
                }
            }
            if (Languages.Count <= 0)
            {
               MessageBox.Show($"No Langauges have been loaded! Ensure {AppEnvironment.LanguagePath} exist with at least one .lang file.");
               Environment.Exit(0);
            }
            var defaultLang = Languages.Values.FirstOrDefault(lang => lang.Culture.Equals(Config.Instance.AppSettings.DefaultCulture));
            defaultLang?.Set();
        }

        public static void Setup(ToolStripMenuItem toolStripLanguages)
        {

            foreach (var lang in Languages.Values)
            {
                var item = toolStripLanguages.DropDownItems.Add(new ToolStripMenuItem
                {
                    Text = lang.ToString(),
                    CheckOnClick = true,
                    Checked = IsDefault(lang.DisplayName)
                });
                toolStripLanguages.DropDownItems[item].Click += (s, ea) =>
                {
                    var tt = (ToolStripMenuItem)s;
                    if (!tt.Checked)
                    {
                        if (IsDefault(tt.Text) && !LanguageSelected(toolStripLanguages.DropDownItems))
                        {
                            tt.Checked = true;
                        }
                        return;
                    }
                    foreach (ToolStripMenuItem dropItem in toolStripLanguages.DropDownItems)
                    {
                        if (dropItem != tt)
                        {
                            dropItem.Checked = false;
                        }
                    }
                    SetDefaultLanguage(tt.Text);
                };
            }
        }

        private static bool LanguageSelected(ToolStripItemCollection dropDownItems)
        {
            var anyChecked = false;
            foreach (ToolStripMenuItem dropItem in dropDownItems)
            {
                anyChecked = dropItem.Checked;
            }
            return anyChecked;
        }

        private static bool IsDefault(string displayName)
        {
            try
            {
                var defaultCulture = Config.Instance.AppSettings.DefaultCulture;
                var langauge = Languages.Values.FirstOrDefault(lang => lang.DisplayName.Equals(displayName));
                return langauge != null && langauge.Culture.Equals(defaultCulture);
            }
            catch
            {

                return false;
            }
        }

        private static void SetDefaultLanguage(string tsiText)
        {
            var langauge = Languages.Values.FirstOrDefault(lang => lang.DisplayName.Equals(tsiText));
            if (langauge != null)
            {
                Config.Instance.AppSettings.DefaultCulture = langauge.Culture;
                Config.Save();
                langauge.Set();
                var dialogResult = MessageBox.Show(Data("settingConfirmationPrompt"), Data("settingConfirmationTitle"),
                    MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    string batchContent = "/c \"@ECHO OFF & timeout /t 6 > nul & start \"\" \"$[APPPATH]$\" & exit\"";
                    batchContent = batchContent.Replace("$[APPPATH]$", Application.ExecutablePath);
                    Process.Start("cmd", batchContent);
                    Application.Exit();
                }
            }
        }
    }
}