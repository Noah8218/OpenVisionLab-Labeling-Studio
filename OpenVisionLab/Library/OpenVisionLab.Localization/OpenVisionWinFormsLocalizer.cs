using System;
using System.Reflection;
using System.Windows.Forms;

namespace OpenVisionLab
{
    public static class OpenVisionWinFormsLocalizer
    {
        public static void Apply(Form form, string prefix, ToolTip toolTip = null)
        {
            if (form == null)
            {
                return;
            }

            ApplyControlText(form, prefix + ".Text");
            Apply((Control)form, prefix, toolTip);
        }

        public static void Apply(Control root, string prefix, ToolTip toolTip = null)
        {
            if (root == null || string.IsNullOrWhiteSpace(prefix))
            {
                return;
            }

            ApplyControlRecursive(root, prefix.Trim(), toolTip);
        }

        private static void ApplyControlRecursive(Control control, string prefix, ToolTip toolTip)
        {
            ApplyNamedControl(control, prefix, toolTip);

            if (control is DataGridView grid)
            {
                ApplyGridColumns(grid, prefix);
            }

            if (control is ToolStrip toolStrip)
            {
                ApplyToolStrip(toolStrip, prefix, toolTip);
            }

            if (control is TabControl tabControl)
            {
                ApplyTabPages(tabControl, prefix);
            }

            foreach (Control child in control.Controls)
            {
                ApplyControlRecursive(child, prefix, toolTip);
            }
        }

        private static void ApplyNamedControl(Control control, string prefix, ToolTip toolTip)
        {
            if (string.IsNullOrWhiteSpace(control.Name))
            {
                return;
            }

            string keyBase = prefix + "." + control.Name;
            ApplyControlText(control, keyBase + ".Text");
            ApplyPlaceholderText(control, keyBase + ".Placeholder");

            if (toolTip != null && OpenVisionLanguageService.TryT(keyBase + ".ToolTip", out string tip))
            {
                toolTip.SetToolTip(control, tip);
            }
        }

        private static void ApplyControlText(Control control, string key)
        {
            if (control == null || !OpenVisionLanguageService.TryT(key, out string text))
            {
                return;
            }

            control.Text = text;
        }

        private static void ApplyPlaceholderText(Control control, string key)
        {
            if (!OpenVisionLanguageService.TryT(key, out string text))
            {
                return;
            }

            PropertyInfo property = control.GetType().GetProperty("PlaceholderText", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
            {
                property.SetValue(control, text);
            }
        }

        private static void ApplyGridColumns(DataGridView grid, string prefix)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column == null)
                {
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(column.Name) ? column.DataPropertyName : column.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string scopedKey = prefix + "." + grid.Name + "." + name + ".HeaderText";
                string sharedKey = prefix + "." + name + ".HeaderText";
                if (OpenVisionLanguageService.TryT(scopedKey, out string text)
                    || OpenVisionLanguageService.TryT(sharedKey, out text))
                {
                    column.HeaderText = text;
                }
            }
        }

        private static void ApplyTabPages(TabControl tabControl, string prefix)
        {
            foreach (TabPage page in tabControl.TabPages)
            {
                if (page == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(page.Name)
                    && OpenVisionLanguageService.TryT(prefix + "." + page.Name + ".Text", out string text))
                {
                    page.Text = text;
                }
            }
        }

        private static void ApplyToolStrip(ToolStrip toolStrip, string prefix, ToolTip toolTip)
        {
            foreach (ToolStripItem item in toolStrip.Items)
            {
                ApplyToolStripItem(item, prefix, toolTip);
            }
        }

        private static void ApplyToolStripItem(ToolStripItem item, string prefix, ToolTip toolTip)
        {
            if (item == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(item.Name))
            {
                string keyBase = prefix + "." + item.Name;
                if (OpenVisionLanguageService.TryT(keyBase + ".Text", out string text))
                {
                    item.Text = text;
                }

                if (OpenVisionLanguageService.TryT(keyBase + ".ToolTip", out string tip))
                {
                    item.ToolTipText = tip;
                }
            }

            if (item is ToolStripDropDownItem dropDown)
            {
                foreach (ToolStripItem child in dropDown.DropDownItems)
                {
                    ApplyToolStripItem(child, prefix, toolTip);
                }
            }
        }
    }
}
