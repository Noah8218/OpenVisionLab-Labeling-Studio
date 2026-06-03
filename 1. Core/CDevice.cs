using Lib.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MvcVisionSystem
{
    public class CDevice
    {
        public int CAMERA_COUNT { get; set; } = 0;
        public int LIGHT_COUNT { get; set; } = 0;

        [XmlIgnore] public List<CGrabberMatrox> CAMERAS = new List<CGrabberMatrox>();

        public CDevice() { }

        public bool Init() => true;
        public bool LoadDevices(string RecipeName) => true;
        public bool Close() => true;

        public CDevice LoadConfig()
        {
            string strPath = Application.StartupPath + "\\CONFIG\\DEVICE\\DEVICE.xml";
            if (System.IO.File.Exists(strPath))
            {
                CDevice newData = SerializeHelper.FromXmlFile<CDevice>(strPath);
                if (newData != null)
                {
                    return newData;
                }
            }

            SaveConfig();
            return this;
        }

        public void SaveConfig()
        {
            try
            {
                CUtil.InitDirectory("CONFIG");
                CUtil.InitDirectory("CONFIG\\DEVICE");
                string strPath = Application.StartupPath + "\\CONFIG\\DEVICE\\DEVICE.xml";
                SerializeHelper.ToXmlFile(strPath, this);
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name} Execption ==> {Desc.Message}");
            }
        }
    }
}
