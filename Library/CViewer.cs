using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Drawing.Drawing2D;
using Cyotek.Windows.Forms;
using MvcVisionSystem._1._Core;
using System.Diagnostics;
using System.IO;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using MouseEventHandler = System.Windows.Forms.MouseEventHandler;
using Point = System.Drawing.Point;
using static MvcVisionSystem.DrawObject.CEnum;
using static MvcVisionSystem._2._Common.CParameterManager;
using MvcVisionSystem.DrawObject;
using Lib.Common;
using System.Web;

namespace MvcVisionSystem
{
    public partial class CViewer : Component
    {
        private PosSizableRect _MouseOperation = PosSizableRect.None;

        public CRectangleObject _TempOb = new CRectangleObject();
        private int _ExcuteCount { get; set; } = 0;
        private System.Drawing.Point _StartPt = new System.Drawing.Point(0, 0);
        private System.Drawing.Point _EndPt = new System.Drawing.Point(0, 0);
        private System.Drawing.Point _MouseDown = new System.Drawing.Point(0, 0);

        public bool _ViewCross { get; set; } = false;

        public RoiMode _Mode { get; set; } = RoiMode.Rectangle;

        public System.Drawing.Point _Position { get; set; } = new System.Drawing.Point();
        public System.Drawing.Color _Rgb { get; set; } = new System.Drawing.Color();
        public int _GrayValue { get; set; } = 0;

        public ImageBox _Ib = new ImageBox();
        /// <summary>
        /// 마우스의 마지막 위치
        /// </summary>
        private System.Drawing.Point _LastPoint = new System.Drawing.Point(0, 0);

        private Rectangle TempROI
        {
            get { return _TempOb.Roi; }
            set { _TempOb.Roi = value; }
        }

        public Dictionary<string, List<CRectangleObject>> _RoiDic = new Dictionary<string, List<CRectangleObject>>();

        public string _SelectedClass { get; set; } = "";
        
        private int _MinY = 0;
        private int _MaxY = 10000;
        private int _MinX = 0;
        private int _MaxX = 10000;
        private int _SelectROiIndex = 0;
        public bool _ImageChanged = false;
        private bool _OnlyDragMode = false;

        public CViewer(bool bCenter = true)
        {
            InitializeComponent();
        }

        private bool isDrawing = false;
        private List<Point> currentPoints = new List<Point>();
        private List<List<Point>> lines = new List<List<Point>>();

        private void Ib_Paint(object sender, PaintEventArgs e)
        {
            Graphics g;
            GraphicsState originalState;

            System.Drawing.Size scaledSizeTempROI;
            System.Drawing.Size drawSizeTempTrain;

            g = e.Graphics;

            originalState = g.Save();

            scaledSizeTempROI = new System.Drawing.Size(TempROI.Width, TempROI.Height);
            drawSizeTempTrain = _Ib.GetScaledSize(scaledSizeTempROI);

            System.Drawing.Point locationTemp;

            // Work out the location of the marker graphic according to the current zoom level and scroll offset
            locationTemp = _Ib.GetOffsetPoint(TempROI.X, TempROI.Y);

            foreach (var Roi in _RoiDic)
            {
                for (int i = 0; i < Roi.Value.Count; i++)
                {
                    System.Drawing.Point Location2 = _Ib.GetOffsetPoint(Roi.Value[i].Roi.X, Roi.Value[i].Roi.Y);
                    System.Drawing.Size scaledSize2 = _Ib.GetScaledSize(Roi.Value[i].Roi.Width, Roi.Value[i].Roi.Height);
                    System.Drawing.Color color = i != _SelectROiIndex ? System.Drawing.Color.Green : System.Drawing.Color.Red;
                    Roi.Value[i].SetParameter(color, scaledSize2, Location2, false);
                    Roi.Value[i].Draw(g, _Ib);
                }
            }

            _TempOb.SetParameter(drawSizeTempTrain, locationTemp);
            _TempOb.Draw(g, _Ib);

            if (_ViewCross)
            {
                int crossSize = (int)(3 * _Ib.ZoomFactor);

                System.Drawing.Point CrossLocationVerStart = _Ib.GetOffsetPoint(0, (_Ib.Image.Height / 2));
                System.Drawing.Point CrossLocationVerEnd = _Ib.GetOffsetPoint(_Ib.Image.Width, (_Ib.Image.Height / 2));
                System.Drawing.Point CrossLocationHorStart = _Ib.GetOffsetPoint((_Ib.Image.Width / 2), 0);
                System.Drawing.Point CrossLocationHorEnd = _Ib.GetOffsetPoint((_Ib.Image.Width / 2), _Ib.Image.Height);

                g.DrawLine(new System.Drawing.Pen(System.Drawing.Color.Yellow, crossSize), CrossLocationVerStart, CrossLocationVerEnd);
                g.DrawLine(new Pen(System.Drawing.Color.Yellow, crossSize), CrossLocationHorStart, CrossLocationHorEnd);
            }

            if (!_Position.IsEmpty && _Position.X > 0 && _Position.Y > 0
             && _Position.X < _Ib.Image.Width && _Position.Y < _Ib.Image.Height)
            {
                System.Drawing.Point CrossLocationVerStart = _Ib.GetOffsetPoint(new Point(0, _Position.Y));
                System.Drawing.Point CrossLocationVerEnd = _Ib.GetOffsetPoint(new Point(_Ib.Image.Width, _Position.Y));
                System.Drawing.Point CrossLocationHorStart = _Ib.GetOffsetPoint(new Point(_Position.X, 0));
                System.Drawing.Point CrossLocationHorEnd = _Ib.GetOffsetPoint(new Point(_Position.X, _Ib.Image.Height));

                int crossSize = (int)(1 * _Ib.ZoomFactor);

                g.DrawLine(new System.Drawing.Pen(System.Drawing.Color.Yellow, crossSize), CrossLocationVerStart, CrossLocationVerEnd);
                // y 축을 따라 라인을 그립니다.
                g.DrawLine(new System.Drawing.Pen(System.Drawing.Color.Yellow, crossSize), CrossLocationHorStart, CrossLocationHorEnd);
            }

            switch (_Mode)
            {               
                case RoiMode.Segmentation:
                    int brushSize = (int)(15 * _Ib.ZoomFactor);

                    // 그리는 영역을 저장하는 Rectangle 리스트를 생성합니다.
                    List<Rectangle> dirtyRectangles = new List<Rectangle>();

                    // 그리기 시작할 때마다 시작점과 끝점을 기준으로 Rectangle을 만들고 리스트에 추가합니다.
                    foreach (var line in lines)
                    {
                        for (int i = 0; i < line.Count - 1; i++)
                        {
                            var start = _Ib.GetOffsetPoint(line[i]);
                            var end = _Ib.GetOffsetPoint(line[i + 1]);

                            // Rectangle을 생성합니다. Rectangle의 좌표는 시작점과 끝점의 최소/최대 값입니다.
                            var rect = new Rectangle(
                                Math.Min(start.X, end.X),
                                Math.Min(start.Y, end.Y),
                                Math.Abs(start.X - end.X) + brushSize,
                                Math.Abs(start.Y - end.Y) + brushSize);

                            // Rectangle을 리스트에 추가합니다.
                            dirtyRectangles.Add(rect);
                        }
                    }

                    // 그리기 중일 때도 동일하게 적용합니다.
                    if (isDrawing && currentPoints.Count > 1)
                    {
                        for (int i = 0; i < currentPoints.Count - 1; i++)
                        {
                            var start = _Ib.GetOffsetPoint(currentPoints[i]);
                            var end = _Ib.GetOffsetPoint(currentPoints[i + 1]);

                            var rect = new Rectangle(
                                Math.Min(start.X, end.X),
                                Math.Min(start.Y, end.Y),
                                Math.Abs(start.X - end.X) + brushSize,
                                Math.Abs(start.Y - end.Y) + brushSize);

                            dirtyRectangles.Add(rect);
                        }
                    }

                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(64, Color.GreenYellow)))  // 브러시 색상 설정
                    {
                        // 변경된 부분만 그립니다.
                        foreach (var rect in dirtyRectangles)
                        {
                            e.Graphics.FillRectangle(brush, rect);
                        }
                    }

                    // 그려진 사각형들을 리스트에서 제거합니다.
                    dirtyRectangles.Clear();

                    break;
            }

          

            g.Restore(originalState);
        }

        private void Ib_MouseDown(object sender, MouseEventArgs e)
        {            
            SettingParameter();
            UnSelectAll();
            _StartPt = _Ib.PointToImage(e.Location);
            
            switch (_Mode)
            {
                case RoiMode.Rectangle:
                    _MouseOperation = _TempOb.GetNodeSelectable(_StartPt, TempROI, _Ib);
                    List<CRectangleObject> RoisOb = new List<CRectangleObject>();
                    if (_RoiDic.TryGetValue(_SelectedClass, out RoisOb))
                    {
                        for (int i = 0; i < RoisOb.Count; i++)
                        {
                            _MouseOperation = RoisOb[i].GetNodeSelectable(_StartPt, RoisOb[i].Roi, _Ib);
                            if (_MouseOperation != PosSizableRect.None)
                            {
                                _SelectROiIndex = i;
                                RoisOb[i].Selected = true;
                                break;
                            }
                        }
                    }
              
                    break;
                case RoiMode.Segmentation:
                    isDrawing = true;
                    if (!_Position.IsEmpty) { currentPoints.Add(_StartPt); }
                    break;
            }

            _LastPoint = _Ib.PointToImage(e.Location);
            _MouseDown = _Ib.PointToImage(e.Location);
        }

        private void IbSource_MouseMove(object sender, MouseEventArgs e)
        {
            if (_Ib.Image == null) { return; }
            if (_Ib.Image.Width == 10 || _Ib.Image.Width == 10) { return; }

            _Position = _Ib.PointToImage(e.Location);

            GetPixelData(_Position);
            ChangeCursor();

            if (e.Button == MouseButtons.Left)
            {
                //마우스의 현재 위치에서 마지막 위치를 뺀 값을 저장한다.
                int distanceX = _Position.X - _LastPoint.X;
                int distanceY = _Position.Y - _LastPoint.Y;

                _LastPoint = _Position;

                switch (_Mode)
                {
                    case RoiMode.Rectangle:

                        if (_MouseOperation == PosSizableRect.None)
                        {
                            SetToRectangle(e, ref _TempOb.Roi);
                            _TempOb.MoveHandleTo(_Position, _MouseOperation);
                        }

                        if (_MouseOperation != PosSizableRect.None)
                        {
                            List<CRectangleObject> RoisOb = new List<CRectangleObject>();
                            if (_RoiDic.TryGetValue(_SelectedClass, out RoisOb))
                            {
                                SetToRectangle(e, ref RoisOb[_SelectROiIndex].Roi);
                                RoisOb[_SelectROiIndex].MoveHandleTo(_Position, _MouseOperation);
                                if (_MouseOperation == PosSizableRect.SizeAll)
                                {
                                    RoisOb[_SelectROiIndex].Move(distanceX, distanceY);
                                }
                            }
                        }
                        break;
                    case RoiMode.Segmentation:
                        if (isDrawing)
                        {
                            if (!_Position.IsEmpty) { currentPoints.Add(_Position); }
                        }

                        break;
                }
            }
            _Ib.Invalidate();
        }

        private void IbSource_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:                        
                        switch (_Mode)
                        {
                            case RoiMode.Rectangle:
                                if (!TempROI.IsEmpty && TempROI.Width > 15 && TempROI.Height > 15)
                                {
                                    CRectangleObject rectangleObject = new CRectangleObject();
                                    rectangleObject.Roi = TempROI;
                                    rectangleObject.cClassItem = _TempOb.cClassItem;

                                    List<CRectangleObject> list = new List<CRectangleObject>();

                                    if(_RoiDic.TryGetValue(_TempOb.cClassItem.Text, out list))
                                    {
                                        list.Add(rectangleObject);
                                        _RoiDic[_TempOb.cClassItem.Text] = list;
                                        _SelectROiIndex = list.Count - 1;
                                    }   
                                    else
                                    {
                                        list = new List<CRectangleObject>();
                                        list.Add(rectangleObject);
                                        _RoiDic.Add(_TempOb.cClassItem.Text, list);
                                        _SelectROiIndex = 0;
                                    }

                                    CGlobal.Inst.System.UpdateData();                                    
                                }
                                TempROI = new Rectangle();
                                break;
                            case RoiMode.Segmentation:
                                isDrawing = false;
                                if (!_Position.IsEmpty)
                                {
                                    currentPoints = new List<Point>();
                                    lines.Add(currentPoints);
                                }
                                break;
                        }
                        break;
                    case MouseButtons.Right:
                        _ExcuteCount = 1;
                        if (_MouseOperation == PosSizableRect.SizeAll) { Open_DropdownMenu(ddmDelete, sender, e); }
                        else { Open_DropdownMenu(ddmImageMenu, sender, e); }
                        break;
                }
                _MouseOperation = PosSizableRect.None;
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
        }

        public void LoadImageBox(ImageBox ImageBox, bool ControlROI = true, bool onlyDragmode = false)
        {
            _Ib = ImageBox;
            _Ib.MouseWheel += new MouseEventHandler(MouseWheelEvent);
            _Ib.MouseDoubleClick += Ib_MouseDoubleClick;
            _Ib.MouseDown += Ib_MouseDown;
            _Ib.MouseMove += IbSource_MouseMove;
            _Ib.MouseUp += IbSource_MouseUp;
            _Ib.KeyDown += IbSource_KeyDown;
            _Ib.ImageChanged += Ib_ImageChanged;
            _Ib.Paint += Ib_Paint;
            _Ib.AllowDrop = true;
            _Ib.DragEnter += Ib_DragEnter;
            _Ib.DragDrop += new DragEventHandler(Form1_DragDrop);
            _Ib.AllowClickZoom = false;
            _Ib.AllowDoubleClick = true;
            _Ib.SelectionMode = ImageBoxSelectionMode.None;
            _Ib.GridColor = System.Drawing.Color.FromArgb(20, 20, 20);
            _Ib.GridColorAlternate = System.Drawing.Color.FromArgb(20, 20, 20);
            _Ib.HorizontalScroll.Visible = true;
            _Ib.VerticalScroll.Visible = true;

            ItemROI.Click += ItemROI_Click;
            ItemTrainROI.Click += ItemROI_Click;
            ItemMultiROI.Click += ItemROI_Click;
            ItemDrag.Click += ItemROI_Click;

            iconMenuItem7.Click += ItemCollection_Click;
            iconMenuItem8.Click += ItemCollection_Click;

            _Ib.Font = new System.Drawing.Font("Verdana", 20F);
            _Ib.TextAlign = ContentAlignment.TopLeft;
            _Ib.ForeColor = System.Drawing.Color.White;

            this._OnlyDragMode = onlyDragmode;

            SetModeDrag();
        }

        private void Ib_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        public void LoadImageBoxOnlyViewer(ImageBox ImageBox, bool ControlROI = true)
        {
            _Ib = ImageBox;

            for (int i = 0; i < 10; i++) { _Ib.ZoomOut(); }
            _Ib.MouseWheel += new MouseEventHandler(MouseWheelEvent);
            _Ib.MouseDoubleClick += Ib_MouseDoubleClick;
            _Ib.AllowClickZoom = false;
            _Ib.AllowDoubleClick = true;
            _Ib.SelectionMode = ImageBoxSelectionMode.None;

            System.Drawing.Color color = System.Drawing.Color.FromArgb(20, 20, 20);

            _Ib.GridColor = color;
            _Ib.GridColorAlternate = color;
            _Ib.ShowPixelGrid = true;

            _Ib.Font = new System.Drawing.Font("Verdana", 20F);
            _Ib.TextAlign = ContentAlignment.TopLeft;
            _Ib.ForeColor = System.Drawing.Color.White;
        }

        void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                string extension = Path.GetExtension(file);
                switch (extension.ToLower())
                {
                    case ".bmp":
                    case ".exif":
                    case ".gif":
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".tif":
                    case ".tiff":
                        Bitmap Image = new Bitmap(file);
                        _Ib.Image = Image;
                        //this.Image = Image;
                        CDisplayManager.ImageSrc = Lib.Common.CImageConverter.ToMat(Image);
                        _Ib.ZoomToFit();
                        break;
                    default:
                        throw new NotSupportedException(
                            "Unknown file extension " + extension);
                }
            }
        }

        public void SetModeMultiRoi()
        {
            ItemMultiROI.IconColor = System.Drawing.Color.Red;
            ItemMultiROI.IconChar = FontAwesome.Sharp.IconChar.Check;
            ItemTrainROI.IconChar = FontAwesome.Sharp.IconChar.None;
            ItemROI.IconChar = FontAwesome.Sharp.IconChar.None;
            ItemDrag.IconChar = FontAwesome.Sharp.IconChar.None;
            _Mode = RoiMode.Rectangle;

            _Ib.PanMode = ImageBoxPanMode.Middle;
        }

        public void SetModeSegmentation()
        {
            ItemMultiROI.IconColor = System.Drawing.Color.Red;
            ItemMultiROI.IconChar = FontAwesome.Sharp.IconChar.Check;
            ItemTrainROI.IconChar = FontAwesome.Sharp.IconChar.None;
            ItemROI.IconChar = FontAwesome.Sharp.IconChar.None;
            ItemDrag.IconChar = FontAwesome.Sharp.IconChar.None;
            _Mode = RoiMode.Segmentation;

            _Ib.PanMode = ImageBoxPanMode.Middle;
        }

        public void SetModeDrag()
        {
            ItemDrag.IconColor = System.Drawing.Color.Red;
            ItemDrag.IconChar = FontAwesome.Sharp.IconChar.Check;
            ItemTrainROI.IconChar = FontAwesome.Sharp.IconChar.None;
            ItemROI.IconChar = FontAwesome.Sharp.IconChar.None;
            ItemMultiROI.IconChar = FontAwesome.Sharp.IconChar.None;
            _Mode = RoiMode.Drag;

            _Ib.PanMode = ImageBoxPanMode.Both;
        }

        private void SetToRectangle(MouseEventArgs e, ref Rectangle ROI)
        {
            if (e.Button == MouseButtons.Left)
            {
                _EndPt = _Ib.PointToImage(e.Location);
                if (_MouseOperation != PosSizableRect.None) { return; }
                if (_MouseOperation == PosSizableRect.SizeAll) { return; }

                OpenCvSharp.Point ptStart = new OpenCvSharp.Point(_StartPt.X, _StartPt.Y);
                OpenCvSharp.Point ptEnd = new OpenCvSharp.Point(_EndPt.X, _EndPt.Y);

                if (ptStart.X > ptEnd.X)
                {
                    if (ptStart.Y < ptEnd.Y) { ROI = new Rectangle(ptEnd.X, ptStart.Y, ptStart.X - ptEnd.X, ptEnd.Y - ptStart.Y); }
                    else { ROI = new Rectangle(ptEnd.X, ptEnd.Y, ptStart.X - ptEnd.X, ptStart.Y - ptEnd.Y); }
                }
                else
                {
                    if (ptStart.Y < ptEnd.Y)
                    {
                        if (ptStart.X < ptEnd.X) { ROI = new Rectangle(ptStart.X, ptStart.Y, ptEnd.X - ptStart.X, ptEnd.Y - ptStart.Y); }
                        else { ROI = new Rectangle(ptStart.X, ptStart.Y, ptEnd.X - ptStart.X, ptEnd.Y - ptStart.Y); }
                    }
                    else
                    {
                        if (ptStart.X < ptEnd.X) { ROI = new Rectangle(ptStart.X, ptEnd.Y, ptEnd.X - ptStart.X, ptStart.Y - ptEnd.Y); }
                        else { ROI = new Rectangle(ptEnd.X, ptEnd.Y, ptStart.X - ptEnd.X, ptStart.Y - ptEnd.Y); }
                    }
                }

                if (ROI.Y < _MinY) ROI = new Rectangle(ROI.X, _MinY, ROI.Width, ROI.Height);
                if (ROI.X < _MinX) ROI = new Rectangle(_MinX, ROI.Y, ROI.Width, ROI.Height);
                if (ROI.Bottom > _MaxY)
                {
                    int Max_H = _MaxY - ROI.Y;
                    ROI = new Rectangle(ROI.X, ROI.Y, ROI.Width, Max_H);
                }
                if (ROI.Right > _MaxX)
                {
                    int Max_W = _MaxX - ROI.X;
                    ROI = new Rectangle(ROI.X, ROI.Y, Max_W, ROI.Height);
                }
            }
        }

        private void IbSource_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.D1:
                    SetModeDrag();
                    break;
                case Keys.D2:
                    SetModeMultiRoi();
                    break;
                case Keys.D3:
                    SetModeSegmentation();
                    break;
                case Keys.D4:
                    break;

                case Keys.ShiftKey:
                case Keys.ControlKey:
                    // ib.PanMode = ImageBoxPanMode.Middle;
                    break;
                case Keys.Enter:
                    break;
                case Keys.Delete:
                    ClearROI();
                    break;
            }
        }

        private void Ib_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < 50; i++) { _Ib.ZoomOut(); }
            _Ib.ZoomToFit();
            _Ib.Update();
        }

        private void ItemCollection_Click(object sender, EventArgs e)
        {
            try
            {
                string strIndex = (sender as ToolStripMenuItem).Text;
                if (_ExcuteCount != 1) { return; }
                ddmImageMenu.Hide();
                switch (strIndex)
                {
                    case "3 Point Measure":
                        if (_Ib.Image.Width != 10 && _Ib.Image.Height != 10)
                        {
                            FormMeasure formMeasure = new FormMeasure((Bitmap)_Ib.Image);
                            formMeasure.TopLevel = true;
                            formMeasure.TopMost = true;
                            formMeasure.StartPosition = FormStartPosition.CenterParent;
                            if (!CUtil.OpenCheckForm(formMeasure)) return;
                            formMeasure.Show();
                        }
                        break;
                    case "Image Compare":
                        break;
                    default:
                        break;
                }
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
            _ExcuteCount++;
        }

        private void ItemROI_Click(object sender, EventArgs e)
        {
            try
            {
                string strIndex = (sender as FontAwesome.Sharp.IconMenuItem).Text;
                if (_ExcuteCount != 1) { return; }
                ddmImageMenu.Hide();
                ddmDelete.Hide();
                switch (strIndex)
                {
                    case "Drag":
                        SetModeDrag();
                        break;
                    case "Multi ROI":
                        SetModeMultiRoi();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
            _ExcuteCount++;
        }

        private void ClearROI()
        {
            switch (_Mode)
            {
                case RoiMode.Rectangle:
                    List<CRectangleObject> rois = new List<CRectangleObject>();
                    if (_RoiDic.TryGetValue(_SelectedClass, out rois))
                    {
                        int index = rois.FindIndex(x => x.Selected == true);
                        if (index < 0) { return; }

                        rois.RemoveAt(index);
                        CGlobal.Inst.System.UpdateData();
                    }

                    break;
            }
            _MouseDown = Point.Empty;
            _StartPt = new System.Drawing.Point();
            _EndPt = new System.Drawing.Point();

            _Ib.Invalidate();
        }

        private void Ib_ImageChanged(object sender, EventArgs e) => _ImageChanged = true;

        private void ChangeCursor()
        {
            switch (_Mode)
            {
                case RoiMode.Rectangle:
                    List<CRectangleObject> RoisOb = new List<CRectangleObject>();
                    if (_RoiDic.TryGetValue(_SelectedClass, out RoisOb))
                    {
                        if (_SelectROiIndex > RoisOb.Count) { return; }
                        if (RoisOb.Count == 0) { return; }

                        int index = RoisOb.FindIndex(x => x.Selected == true);

                        if (index < 0) { return; }

                        _Ib.Cursor = RoisOb[index].ChangeCursor(_Position, RoisOb[index].Roi, _Ib);
                    }
                    break;
            }
        }

        /// <summary>
        /// 그려진 모든 DrawObject의 선택을 해제한다.
        /// </summary>
        private void UnSelectAll()
        {
            foreach (var item in _RoiDic)
            {
                for (int i = 0; i < item.Value.Count; i++)
                {
                    item.Value[i].Selected = false;
                }
            }
        }

        private void SettingParameter()
        {
            if (_Ib.Image == null) { return; }
            _MaxX = _Ib.Image.Width;
            _MaxY = _Ib.Image.Height;
            _TempOb._MaxX = _Ib.Image.Width;
            _TempOb._MaxY = _Ib.Image.Height;
            _TempOb.OriginalSize = new System.Drawing.Size(this._Ib.Image.Width, this._Ib.Image.Height);

            foreach (var item in _RoiDic)
            {
                for (int i = 0; i < item.Value.Count; i++)
                {
                    item.Value[i]._MaxX = _Ib.Image.Width;
                    item.Value[i]._MaxY = _Ib.Image.Height;
                    item.Value[i].OriginalSize = new System.Drawing.Size(this._Ib.Image.Width, this._Ib.Image.Height);
                }
            }
        }

        private void GetPixelData(System.Drawing.Point _POSITION)
        {
            if (_POSITION.X > 0 && _POSITION.Y > 0 && _POSITION.X < _Ib.Image.Width && _POSITION.Y < _Ib.Image.Height)
            {
                Bitmap Image = (Bitmap)_Ib.Image;
                _Rgb = Image.GetPixel(_POSITION.X, _POSITION.Y);
                _GrayValue = (_Rgb.R + _Rgb.G + _Rgb.B) / 3;
            }
        }

        private void ImageMenuClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            try
            {
                if (_ExcuteCount != 1) { return; }
                ddmImageMenu.Hide();
                ddmDelete.Hide();
                switch (e.ClickedItem.Text)
                {
                    case "Image Load":
                        string ImagePath = CUtil.LoadImageFilePath();

                        if (ImagePath != "")
                        {
                            Bitmap Image = new Bitmap(ImagePath);
                            _Ib.Image = Image;
                            //this.Image = Image;
                            CDisplayManager.ImageSrc = Lib.Common.CImageConverter.ToMat(Image);
                            _Ib.ZoomToFit();
                        }
                        break;
                    case "Image Save":
                        if (_Ib.Image.Width != 10 && _Ib.Image.Height != 10)
                        {
                            ImagePath = CUtil.SaveImageFilePath();

                            if (ImagePath != "") { _Ib.Image.Save(ImagePath); }
                        }
                        break;
                    case "Show Folder":
                        Process.Start(Application.StartupPath + "\\");
                        break;
                    case "CROSS":
                        _ViewCross = !_ViewCross;
                        break;
                    case "Delete":
                        ClearROI();
                        break;
                    case "Roi List":
                        break;
                    default:
                        break;
                }
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
            _ExcuteCount++;
        }


        private void Open_DropdownMenu(RJCodeUI_M1.RJControls.RJDropdownMenu dropdownMenu, object sender, MouseEventArgs e)
        {
            Control control = (Control)sender;

            dropdownMenu.VisibleChanged += new EventHandler((sender2, ev)
             => DropdownMenu_VisibleChanged(sender2, ev, control));
            dropdownMenu.ItemClicked += new ToolStripItemClickedEventHandler(ImageMenuClicked);
            dropdownMenu.Show(control, e.X, e.Y);
        }

        private void DropdownMenu_VisibleChanged(object sender, EventArgs e, Control ctrl)
        {
            RJCodeUI_M1.RJControls.RJDropdownMenu dropdownMenu = (RJCodeUI_M1.RJControls.RJDropdownMenu)sender;
            if (!DesignMode)
            {
                if (dropdownMenu.Visible)
                    ctrl.BackColor = DEFINE.MOUSEHOVER_COLOR;
                else ctrl.BackColor = System.Drawing.Color.FromArgb(49, 42, 81);
            }
        }

        #region ImageBox
        private void MouseWheelEvent(object sender, MouseEventArgs e)
        {
            if ((e.Delta / 120) > 0) { ZoomInImage(); }
            else { ZoomOutImage(); }
        }

        #region Display
        private void ZoomInImage() => _Ib.ZoomIn();
        private void ZoomOutImage() => _Ib.ZoomOut();
        #endregion

        #endregion
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (this._OnlyDragMode)
            {
                _Mode = RoiMode.Drag;
                _Ib.Text = "";
                return;
            }

            switch (_Mode)
            {
                case RoiMode.Drag:
                    _Ib.Text = "이동 모드";
                    break;
                case RoiMode.Rectangle:
                    _Ib.Text = "사각형 모드";
                    break;
                case RoiMode.Segmentation:
                    _Ib.Text = "브러쉬 모드";
                    break;
            }
        }
    }
}
