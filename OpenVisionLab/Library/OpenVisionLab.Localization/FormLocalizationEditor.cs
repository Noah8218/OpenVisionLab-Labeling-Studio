using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenVisionLab
{
    public partial class FormLocalizationEditor : Form
    {
        private readonly List<OpenVisionLocalizationEntry> allEntries = new List<OpenVisionLocalizationEntry>();
        private bool applyingLanguage;

        public FormLocalizationEditor()
        {
            InitializeComponent();
        }

        private void FormLocalizationEditor_Load(object sender, EventArgs e)
        {
            OpenVisionLanguageService.Load();
            StyleControls();
            BindLanguage();
            LoadCatalogRows();
            ApplyLocalization();
        }

        private void cbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (applyingLanguage)
            {
                return;
            }

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguageService.GetLanguageFromCombo(cbLanguage));
            ApplyLocalization();
        }

        private void tbSearch_TextChanged(object sender, EventArgs e)
        {
            PopulateRows();
        }

        private void chkMissingOnly_CheckedChanged(object sender, EventArgs e)
        {
            PopulateRows();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            OpenVisionLanguageService.SaveEntries(ReadGridEntries());
            LoadCatalogRows();
            ApplyLocalization();
            MessageBox.Show(this, OpenVisionLanguageService.T("Localization.Saved"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            OpenVisionLanguageService.ReloadCatalog();
            LoadCatalogRows();
            ApplyLocalization();
            MessageBox.Show(this, OpenVisionLanguageService.T("Localization.Reloaded"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void BindLanguage()
        {
            applyingLanguage = true;
            try
            {
                OpenVisionLanguageService.BindLanguageCombo(cbLanguage);
            }
            finally
            {
                applyingLanguage = false;
            }
        }

        private void LoadCatalogRows()
        {
            allEntries.Clear();
            allEntries.AddRange(OpenVisionLanguageService.GetEntries());
            PopulateRows();
        }

        private void PopulateRows()
        {
            string filter = tbSearch.Text?.Trim() ?? string.Empty;
            IEnumerable<OpenVisionLocalizationEntry> entries = allEntries;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                entries = entries.Where(entry =>
                    Contains(entry.Key, filter)
                    || Contains(entry.Korean, filter)
                    || Contains(entry.English, filter));
            }

            if (chkMissingOnly?.Checked == true)
            {
                entries = entries.Where(IsMissingTranslation);
            }

            List<OpenVisionLocalizationEntry> visibleEntries = entries.ToList();
            int missingCount = allEntries.Count(IsMissingTranslation);

            gridCatalog.SuspendLayout();
            try
            {
                gridCatalog.Rows.Clear();
                foreach (OpenVisionLocalizationEntry entry in visibleEntries)
                {
                    gridCatalog.Rows.Add(entry.Key, entry.Korean, entry.English);
                }
            }
            finally
            {
                gridCatalog.ResumeLayout();
            }

            UpdateSummary(visibleEntries.Count, allEntries.Count, missingCount);
        }

        private IEnumerable<OpenVisionLocalizationEntry> ReadGridEntries()
        {
            Dictionary<string, OpenVisionLocalizationEntry> entries = allEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(entry => entry.Key, entry => new OpenVisionLocalizationEntry
                {
                    Key = entry.Key,
                    Korean = entry.Korean,
                    English = entry.English
                }, StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in gridCatalog.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                string key = Convert.ToString(row.Cells[colKey.Index].Value)?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                entries[key] = new OpenVisionLocalizationEntry
                {
                    Key = key,
                    Korean = Convert.ToString(row.Cells[colKorean.Index].Value) ?? string.Empty,
                    English = Convert.ToString(row.Cells[colEnglish.Index].Value) ?? string.Empty
                };
            }

            return entries.Values;
        }

        private void ApplyLocalization()
        {
            Text = OpenVisionLanguageService.T("Localization.Title");
            lblLanguage.Text = OpenVisionLanguageService.T("Localization.Language");
            lblSearch.Text = OpenVisionLanguageService.T("Localization.Search");
            tbSearch.PlaceholderText = OpenVisionLanguageService.T("Localization.FilterHint");
            colKey.HeaderText = OpenVisionLanguageService.T("Localization.Key");
            colKorean.HeaderText = OpenVisionLanguageService.T("Localization.Korean");
            colEnglish.HeaderText = OpenVisionLanguageService.T("Localization.English");
            chkMissingOnly.Text = OpenVisionLanguageService.T("Localization.MissingOnly");
            btnSave.Text = OpenVisionLanguageService.T("Localization.Save");
            btnReload.Text = OpenVisionLanguageService.T("Localization.Reload");
            btnClose.Text = OpenVisionLanguageService.T("Localization.Close");
            BindLanguage();
            UpdateSummary(
                gridCatalog.Rows.Cast<DataGridViewRow>().Count(row => !row.IsNewRow),
                allEntries.Count,
                allEntries.Count(IsMissingTranslation));
        }

        private void StyleControls()
        {
            Color accent = Color.FromArgb(47, 111, 171);
            foreach (Button button in new[] { btnSave, btnReload, btnClose })
            {
                button.BackColor = Color.FromArgb(250, 252, 253);
                button.ForeColor = Color.FromArgb(35, 85, 132);
                button.FlatAppearance.BorderColor = accent;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 241, 250);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(216, 232, 247);
            }

            gridCatalog.EnableHeadersVisualStyles = false;
            gridCatalog.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 240, 248);
            gridCatalog.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(35, 85, 132);
            gridCatalog.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            gridCatalog.DefaultCellStyle.SelectionBackColor = Color.FromArgb(211, 231, 249);
            gridCatalog.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 45, 70);
            gridCatalog.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 253);
        }

        private static bool Contains(string value, string filter)
        {
            return (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsMissingTranslation(OpenVisionLocalizationEntry entry)
        {
            return entry != null
                && (string.IsNullOrWhiteSpace(entry.Korean) || string.IsNullOrWhiteSpace(entry.English));
        }

        private void UpdateSummary(int visibleCount, int totalCount, int missingCount)
        {
            if (lblSummary == null)
            {
                return;
            }

            lblSummary.Text = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                OpenVisionLanguageService.T("Localization.Summary"),
                visibleCount,
                totalCount,
                missingCount);
        }
    }
}
