using MvcVisionSystem._1._Core;
using Lib.Common;
using Lib.OpenCV;
using MetroFramework.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using static MvcVisionSystem.DEFINE;
using Cursors = System.Windows.Forms.Cursors;
using MvcVisionSystem._3._Device.TCP;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    public partial class FormTeachingVision : MetroForm
    {
        private CGlobal Global = CGlobal.Inst;
        private WeifenLuo.WinFormsUI.Docking.DockPanel dockPanel;
        private int PanelCount = 0;

        #region Event Register                        
        public EventHandler<ClassItemEventArgs> EventUpdateImageItem;
        public EventHandler<EventArgs> EventUpdateClassItem;
        #endregion

        public Dictionary<VISION_DOCK_FORM, object> Forms = new Dictionary<VISION_DOCK_FORM, object>();

        public FormTeachingVision() => InitializeComponent();

        private void FormTeachingVision_Load(object sender, EventArgs e)
        {            
            InitEvent();
            InitUi();                        
        }

        private void btnNewPanel_Click(object sender, EventArgs e) => CDisplayManager.CreatePanel();

        // 최상위 keys 명령어 이기 때문에 
        // Datagridview 같은곳에 editmode f2번같은게 먹지 않는다.        
        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            Keys key = keyData & ~(Keys.Shift | Keys.Control);

            switch (key)
            {
                case Keys.Escape:
                    //if (CCommon.ShowMessageBox("Notice", "창을 닫으시겠습니까?"))
                    //{
                    //    this.DialogResult = DialogResult.Cancel;
                    //    this.Close();
                    //}
                    return true;
                case Keys.F5:
                    return true;
                case Keys.F7:
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }


        private void OnGrabEnd(object sender, GrabEventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
                if (Global.System.Menu == CSystem.MENU.VISION)
                {
                    if (!COpenCVHelper.IsImageEmpty(e.ImageGrab))
                    {
                        //CDisplayManager.Displays[DEFINE.Main].Activate();
                        CDisplayManager.Displays[DEFINE.Main].ibSource.Image = Lib.Common.CImageConverter.ToBitmap(e.ImageGrab.Clone());

                        e.ImageGrab.Dispose();
                        e.ImageGrab = null;
                    }

                    GC.Collect();
                }
            });           
        }

        private bool InitEvent()
        {
            try
            {
                CDisplayManager.EventUpdateResult += OnUpdateResult;
                Global.Thread.CSeqVision.EventSeqComplete += OnInspResult;                
                EventUpdateImageItem+= OnUpdateImageItem;
                EventUpdateClassItem += OnUpdateClassItem;
                Global.Recipe.EventChagedRecipe += OnChangedRecipe;                

                for (int i = 0; i < Global.Device.CAMERAS.Count; i++)
                {
                    Global.Device.CAMERAS[i].EventGrabEnd += OnGrabEnd;
                }
                
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name} Execption ==> {Desc.Message}");
                return false;
            }
            return true;
        }

        private void OnInspResult(object sender, EventArgs e)
        {
            if (!(e is InspResultArgs args)) { return; }
            this.UIThreadInvoke(() =>
            {
                CDisplayManager.CreateLayerDisplay(args.imageResult, "Result");
                CDisplayManager.Displays[CDisplayManager.FindIndex("Result")].Activate();
                CDisplayManager.Displays[CDisplayManager.FindIndex("Result")].ibSource.ZoomToFit();                
            });
        }

        private void InitUi()
        {
            Font font = new Font("Verdana", 12, FontStyle.Regular);

            this.dockPanel = new WeifenLuo.WinFormsUI.Docking.DockPanel();
            this.dockPanel.Theme = new VS2015DarkTheme();
            this.dockPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dockPanel.DocumentStyle = DocumentStyle.DockingWindow;
            TeachingPanel.Controls.Add(this.dockPanel);
            CDisplayManager.SetForm(this);
            CDisplayManager.SetDockPanel(dockPanel);
            CDisplayManager.Displays.Add(new FormLayerDisplay(new Bitmap(10, 10), 0, CDisplayManager.Displays, false, "Main"));
            CDisplayManager.Displays[DEFINE.Main].Show(this.dockPanel, DockState.Document);

            dockPanel.Theme.Skin.DockPaneStripSkin.TextFont = font;
            dockPanel.Theme.Skin.AutoHideStripSkin.TextFont = font;

            Forms.Add(VISION_DOCK_FORM.IMAGELIST, new FormImageList());
            Forms.Add(VISION_DOCK_FORM.CLASSLIST, new FormClassList());
            Forms.Add(VISION_DOCK_FORM.TOOLS, new FormTools());
            Forms.Add(VISION_DOCK_FORM.LOG, new FormLog());            
            ShowVisionForms();
        }

        private void ShowVisionForms()
        {
            dockPanel.DockLeftPortion = 67;
            dockPanel.DockRightPortion = 570;
            WeifenLuo.WinFormsUI.Docking.DockContent fr;
            foreach (var form in Forms)
            {
                fr = (form.Value as WeifenLuo.WinFormsUI.Docking.DockContent);
                DockContent system = (Forms[VISION_DOCK_FORM.IMAGELIST] as DockContent);
                DockContent CLASSLIST = (Forms[VISION_DOCK_FORM.CLASSLIST] as DockContent);
                switch (form.Key)
                {
                    case VISION_DOCK_FORM.IMAGELIST:
                        fr.Show(this.dockPanel, DockState.DockRight);
                        fr.AutoHidePortion = 570;
                        break;
                    case VISION_DOCK_FORM.CLASSLIST:
                        fr.Show(system.PanelPane, DockAlignment.Bottom, 0.47);
                        fr.AutoHidePortion = 570;
                        break;
                    case VISION_DOCK_FORM.LOG:
                        fr.Show(CLASSLIST.PanelPane, DockAlignment.Bottom, 0.5);
                        fr.AutoHidePortion = 570;
                        break;
                    //case VISION_DOCK_FORM.BLOB:
                    //    fr.Show(system.PanelPane, null);
                    //    break;
                    //case VISION_DOCK_FORM.LINE:
                    //    fr.Show(system.PanelPane, null);
                    //    break;
                    //case VISION_DOCK_FORM.TEACHING:
                    //    fr.Show(system.PanelPane, null);
                    //    break;
                    case VISION_DOCK_FORM.CONTOUR:
                        break;
                    case VISION_DOCK_FORM.TOOLS:
                        fr.Show(this.dockPanel, DockState.DockLeftAutoHide);
                        fr.AutoHidePortion = 100;                        
                        break;
                    //case VISION_DOCK_FORM.PROPERTY:
                    //    fr.Show(system.PanelPane, DockAlignment.Bottom, 0.47);
                    //    fr.AutoHidePortion = 550;
                    //    break;
                    //case VISION_DOCK_FORM.THRESHOLD:
                    //    fr.Show(this.dockPanel, DockState.DockLeftAutoHide);
                    //    fr.AutoHidePortion = 500;
                    //    break;
                }
            }
            fr = (Forms[VISION_DOCK_FORM.IMAGELIST] as WeifenLuo.WinFormsUI.Docking.DockContent);
            fr.Activate();
            fr = (Forms[VISION_DOCK_FORM.CLASSLIST] as WeifenLuo.WinFormsUI.Docking.DockContent);
            fr.Activate();
        }

        private void OnChangedRecipe(object sender, EventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
            });
        }

        private void OnUpdateResult(object sender, EventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
                //lbTackTime.Text = CDisplayManager.TackTime;
            });          
        }

        private void OnUpdateClassItem(object sender, EventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
                //foreach (var item in CDisplayManager.Displays[DEFINE.Main].viewer._RoisOb)
                //{
                //    FormClassList fr = (FormClassList)Forms[VISION_DOCK_FORM.CLASSLIST];
                //    fr.ShowClassItems();
                //}
            });
        }

        private void OnUpdateImageItem(object sender, ClassItemEventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
                CDisplayManager.Displays[DEFINE.Main].viewer._SelectedClass = e.cClassItem.Text;
                CDisplayManager.Displays[DEFINE.Main].viewer._TempOb.cClassItem = e.cClassItem;
                CDisplayManager.Displays[DEFINE.Main].viewer._TempOb.Color = e.cClassItem.DrawColor;
                CDisplayManager.Displays[DEFINE.Main].viewer._TempOb.Title = e.cClassItem.Text;
            });
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                if (PanelCount != CDisplayManager.Displays.Count) { PanelCount = CDisplayManager.Displays.Count; }
                
                if (CDisplayManager.Displays[CDisplayManager.FindIndex()].viewer._ImageChanged)
                {
                    CDisplayManager.ImageSrc = Lib.Common.CImageConverter.ToMat((Bitmap)CDisplayManager.Displays[CDisplayManager.FindIndex()].viewer._Ib.Image);
                    CDisplayManager.Displays[CDisplayManager.FindIndex()].viewer._ImageChanged = false;
                }                
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
        }
    }   
}
