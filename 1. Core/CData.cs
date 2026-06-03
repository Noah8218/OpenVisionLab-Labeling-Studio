using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using System.Collections.Concurrent;
using System.Xml.Linq;
using OpenCvSharp;
using System.Collections.Generic;
using System.Xml.Serialization;
using static MvcVisionSystem.DEFINE;
using MvcVisionSystem._2._Common;
using System.ComponentModel;
using System.Drawing;
using OpenCvSharp.Aruco;
using Lib.Common;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{   
    public class CData
    {
        // 이미지 큐
        // 해당 큐에 검사 이미지를 넣어서 검사를 진행        
        [XmlIgnore] public ConcurrentQueue<CGrabBuffer> GrabQueue = new ConcurrentQueue<CGrabBuffer>();

        // 스팩관련 프로퍼티
        [XmlIgnore] public CPropertySpec SPEC = new CPropertySpec("SPEC");
        [XmlIgnore] public CPropertySetting SETTING = new CPropertySetting("SETTING");

        // 그래프 관리 리스트
        [XmlIgnore] public List<CMvcGraph> GraphList { get; set; } = new List<CMvcGraph>();

        public List<CClassItem> ClassNamedList { get; set; } = new List<CClassItem>();

        public string OutputDataYamlPath { get; set; } = "";

        public string OutputDataImageAndTxtPath { get; set; } = "";

        public CYolov5TranningParam TranningParam { get; set; } = new CYolov5TranningParam();

        [XmlIgnore] public string LastSelectImageName { get; set; } = "";

        public CData() { CUtil.InitDirectory("DATA"); }
   
        /// <summary>
        /// 레시피가 변경될 때마다 프로퍼티값들을 다시 load
        /// </summary>
        /// <param name="RecipeName"></param>
        public void LoadProperty(string RecipeName)
        {
            // 직렬화는 아래와 같이 객체를 Load후 넘겨줘야 한다.
            SPEC = SPEC.LoadConfig(RecipeName);
            SETTING = SETTING.LoadConfig(RecipeName);
        }

        public CData LoadConfig(string RecipeName)
        {
            string strPath = Application.StartupPath + "\\RECIPE\\" + RecipeName + "\\" + "VISION" + ".xml";
            CData newData = null;

            if (File.Exists(strPath))
            {
                newData = SerializeHelper.FromXmlFile<CData>(strPath);
                if (newData != null)
                {
                    newData.LoadProperty(RecipeName);
                    return newData;
                }
                    
            }
            this.SaveConfig(RecipeName);
            return newData = this.LoadConfig(RecipeName);
        }

        public void SaveConfig(string RecipeName)
        {
            string strPath = Application.StartupPath + "\\RECIPE\\" + RecipeName + "\\" + "VISION" + ".xml";
            SerializeHelper.ToXmlFile(strPath, this);
        }
    }

    public class CGrabBuffer
    {
        public int Index = 0;                
        public Mat ImageGrab = new Mat();
        public double TotalEncoder = 0;
        public bool TestImage = false;
        public CGrabBuffer(Mat image, int nIndex, double totalEncoder, bool testImage)
        {
            Index = nIndex;
            // 나중에 메모리 증가 사유일수 있음(Deep/Slow 확인 필요)
            image.CopyTo(ImageGrab);
            TotalEncoder = totalEncoder;
            TestImage = testImage;
        }
    }
}
