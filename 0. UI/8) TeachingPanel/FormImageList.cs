using MvcVisionSystem._1._Core;
using Lib.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Manina.Windows.Forms;
using SortOrder = Manina.Windows.Forms.SortOrder;
using View = Manina.Windows.Forms.View;

namespace MvcVisionSystem
{
    public partial class FormImageList : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        private CGlobal Global = CGlobal.Inst;
        private int PanelCount = 0;
        public FormImageList()
        {
            InitializeComponent();

            CloseButton = false;
            CloseButtonVisible = false;
            this.Resize += ResizeEvent;
        }

        // If the content won't display nicely, hide it
        private void ResizeEvent(object sender, EventArgs e)
        {
            this.Visible = this.Width > this.MinimumSize.Width && this.Height > this.MinimumSize.Height;
            this.Refresh();
        }

        private bool ChangeSize = false;

        private void Form_VisibleChanged(object sender, EventArgs e)
        {
            //dgvImagesList.Update();

            if (!ChangeSize)
            {
                if (DockHandler.FloatPane == null) { return; }
                DockHandler.FloatPane.FloatWindow.Bounds = new Rectangle(DockHandler.FloatPane.FloatWindow.Bounds.X, DockHandler.FloatPane.FloatWindow.Bounds.Y, 800, 400);
                this.Refresh();
                ChangeSize = true;
            }
        }

        private void Form_Load(object sender, EventArgs e)
        {            
            CDisplayManager.EventUpdateCam += OnCamUpdate;
            ShowImageDgv(new List<string>());

            imageListView1.AllowDuplicateFileNames = true;
            imageListView1.SetRenderer(new Manina.Windows.Forms.ImageListViewRenderers.DefaultRenderer());
            imageListView1.SortColumn = 0;
            imageListView1.SortOrder = SortOrder.AscendingNatural;

            Assembly assembly = Assembly.GetAssembly(typeof(ImageListView));

            int i = 0;
            foreach (Type type in assembly.GetTypes())
            {
                if (type.BaseType == typeof(ImageListView.ImageListViewRenderer))
                {
                    renderertoolStripComboBox.Items.Add(new RendererComboBoxItem(type));
                    if (type.Name == "DefaultRenderer")
                        renderertoolStripComboBox.SelectedIndex = i;
                    i++;
                }
            }
            // Find and add custom colors
            Type colorType = typeof(ImageListViewColor);
            i = 0;
            foreach (PropertyInfo field in colorType.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                colorToolStripComboBox.Items.Add(new ColorComboBoxItem(field));
                if (field.Name == "Default")
                    colorToolStripComboBox.SelectedIndex = i;
                i++;
            }

            string cacheDir = Path.Combine(
                Path.GetDirectoryName(new Uri(assembly.GetName().CodeBase).LocalPath),
                "Cache"
                );
            imageListView1.Columns.Add(ColumnType.Name);
            imageListView1.Columns.Add(ColumnType.Dimensions);
            imageListView1.Columns.Add(ColumnType.FileSize);
            imageListView1.Columns.Add(ColumnType.FolderName);
            imageListView1.Columns.Add(ColumnType.DateModified);
            imageListView1.Columns.Add(ColumnType.FileType);
            var col = new ImageListView.ImageListViewColumnHeader(ColumnType.Custom, "random", "Random");
            col.Comparer = new RandomColumnComparer();
            imageListView1.Columns.Add(col);
        }

        private void OnCamUpdate(object sender, EventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
                //tbExposure.Text = Global.Device.CAMERAS[CDisplayManager.CameraIndex].Property.EXPOSURETIME_US.ToString();
                //tbGain.Text = Global.Device.CAMERAS[CDisplayManager.CameraIndex].Property.GAIN.ToString();
            });
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            LoadFolderPath(out string folderPath);
            if (folderPath != "")
            {
                List<string> imagePaths = GetImageFiles(folderPath);
                ShowImageDgv(imagePaths);
            }            
        }

        private string lastPath = string.Empty;

        private bool LoadFolderPath(out string folderPath)
        {
            folderPath = "";
            try
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    // 이전에 저장된 경로가 있다면 사용합니다.
                    if (!string.IsNullOrEmpty(lastPath))
                    {
                        fbd.SelectedPath = lastPath;
                    }

                    DialogResult result = fbd.ShowDialog();

                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        folderPath = fbd.SelectedPath;
                        lastPath = folderPath;  // 선택된 경로를 저장합니다.
                    }
                }

                CLOG.NORMAL($"[OK] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}");
                return true;
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
                return false;
            }
        }

        private void ShowImageDgv(List<string> imagePaths)
        {
            // DataGridView 설정
            // dgvImagesList.Columns.Clear();  // 기존의 모든 컬럼을 제거
            //DataGridViewImageColumn imgColumn = new DataGridViewImageColumn();  // 이미지 컬럼을 추가
            //imgColumn.HeaderText = "이미지";
            //imgColumn.Name = "이미지";
            //imgColumn.ImageLayout = DataGridViewImageCellLayout.Stretch;
            imageListView1.Items.Clear();
            imageListView1.ThumbnailSize = new System.Drawing.Size(200, 200);
            
            for (int i = 0; i < imagePaths.Count; i++)
            {
                ImageListViewItem imageListViewItem = new ImageListViewItem(imagePaths[i]);
                //Image image = Image.FromFile(imagePaths[i]);
                string fileName = Path.GetFileName(imagePaths[i]);
                //imageListView1.Items.Add(fileName, image);                
                imageListView1.Items.Add(imageListViewItem);
                Application.DoEvents();
            }

            //dgvImagesList.Columns.Add("No", "No");
            //dgvImagesList.Columns.Add(imgColumn);
            //dgvImagesList.Columns.Add("파일 경로", "파일 경로");
            //dgvImagesList.RowTemplate.Height = 100;  // 적절한 높이를 설정하세요


            foreach (var path in imagePaths)
            {
                // 파일 경로에서 이미지를 로드
                Image image = Image.FromFile(path);
                string fileName = Path.GetFileName(path);                
                // DataGridView에 이미지와 경로를 추가
               // int index = dgvImagesList.Rows.Count + 1;
                //object[] row = new object[] { index.ToString(), image, fileName };
                //dgvImagesList.Rows.Add(row);
            }

            //dgvImagesList.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            //dgvImagesList.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            //dgvImagesList.Columns[0].Width = 40;
        }

        public  List<string> GetImageFiles(string folderPath)
        {
            string[] SUPPORTED_EXTENSIONS = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            // 파일 목록을 가져올 리스트를 생성
            List<string> imageFiles = new List<string>();

            // 폴더가 존재하는지 확인
            if (Directory.Exists(folderPath))
            {
                // 해당 폴더에 있는 모든 파일들을 가져온다
                string[] files = Directory.GetFiles(folderPath);

                // 각 파일에 대해
                foreach (var file in files)
                {
                    // 파일 확장자를 가져온다
                    var extension = Path.GetExtension(file).ToLower();

                    // 이미지 파일이면 리스트에 추가한다
                    if (Array.IndexOf(SUPPORTED_EXTENSIONS, extension) > -1)
                    {
                        imageFiles.Add(file);
                    }
                }
            }
            else { Console.WriteLine("Directory does not exist."); }            
            return imageFiles;
        }

        private void uiSplitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void dgvImagesList_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                // 선택한 셀의 위치를 확인
                int columnIndex = e.ColumnIndex;
                int rowIndex = e.RowIndex;

                // 올바른 위치에서 더블 클릭했는지 확인
                if (columnIndex >= 0 && rowIndex >= 0)
                {
                    //if(dgvImagesList.Columns[columnIndex].Name == "이미지")
                    //{
                    //    // 선택한 셀의 이미지 경로를 가져옵니다.
                    //    Image image = (Image)dgvImagesList.Rows[rowIndex].Cells[columnIndex].Value;
                    //    string fileName = (string)dgvImagesList.Rows[rowIndex].Cells[columnIndex + 1].Value;
                    //    CGlobal.Inst.Data.LastSelectImageName = fileName;
                    //    CDisplayManager.ImageSrc = Lib.Common.CImageConverter.ToMat((Bitmap)image);                        
                    //    CDisplayManager.CreateLayerDisplay((Bitmap)image, "Main", true);
                    //    CDisplayManager.ZoomToFit("Main");
                    //}                    
                }


            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
                CCommon.ShowMessageBox("EXCEPTION", $"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
        }

        private void renderertoolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Assembly assembly = Assembly.GetAssembly(typeof(ImageListView));
            RendererComboBoxItem item = (RendererComboBoxItem)renderertoolStripComboBox.SelectedItem;
            ImageListView.ImageListViewRenderer renderer = (ImageListView.ImageListViewRenderer)assembly.CreateInstance(item.FullName);
            if (renderer == null)
            {
                assembly = Assembly.GetExecutingAssembly();
                renderer = (ImageListView.ImageListViewRenderer)assembly.CreateInstance(item.FullName);
            }
            colorToolStripComboBox.Enabled = renderer.CanApplyColors;
            imageListView1.SetRenderer(renderer);
            imageListView1.Focus();
        }

        private void colorToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            PropertyInfo field = ((ColorComboBoxItem)colorToolStripComboBox.SelectedItem).Field;
            ImageListViewColor color = (ImageListViewColor)field.GetValue(null, null);
            imageListView1.Colors = color;
        }

        private void thumbnailsToolStripButton_Click(object sender, EventArgs e)
        {
            imageListView1.View = View.Thumbnails;
        }

        private void galleryToolStripButton_Click(object sender, EventArgs e)
        {
            imageListView1.View = View.Gallery;
        }

        private void horizontalStripToolStripButton_Click(object sender, EventArgs e)
        {
            imageListView1.View = View.HorizontalStrip;
        }

        private void verticalStripToolStripButton_Click(object sender, EventArgs e)
        {
            imageListView1.View = View.VerticalStrip;
        }

        private void clearThumbsToolStripButton_Click(object sender, EventArgs e)
        {
            imageListView1.ClearThumbnailCache();
        }

        private void x96ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            imageListView1.ThumbnailSize = new System.Drawing.Size(96, 96);
        }

        private void x200ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            imageListView1.ThumbnailSize = new System.Drawing.Size(200, 200);
        }

        private void imageListView1_ItemClick(object sender, ItemClickEventArgs e)
        {

            var image = Image.FromFile(e.Item.FileName);
            string fileName = e.Item.Text;
            CGlobal.Inst.Data.LastSelectImageName = fileName;
            CDisplayManager.ImageSrc = Lib.Common.CImageConverter.ToMat((Bitmap)image);
            CDisplayManager.CreateLayerDisplay((Bitmap)image, "Main", true);
            CDisplayManager.ZoomToFit("Main");
        }
    }

    internal class RendererComboBoxItem
    {
        public RendererComboBoxItem(Type rendererType)
        {
            FullName = rendererType.FullName;
            Name = rendererType.Name;
        }

        public string FullName { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }

    internal class ColorComboBoxItem
    {
        public ColorComboBoxItem(PropertyInfo field)
        {
            Field = field;
        }

        public PropertyInfo Field { get; }
        public override string ToString() => Field.Name;
    }

    internal class RandomColumnComparer : IComparer<ImageListViewItem>
    {
        public int Compare(ImageListViewItem x, ImageListViewItem y)
        {
            return string.Compare(x?.Text, y?.Text, StringComparison.Ordinal);
        }
    }
}
