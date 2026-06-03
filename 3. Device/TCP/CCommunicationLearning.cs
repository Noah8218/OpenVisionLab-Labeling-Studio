using Lib.Common;
using MvcVisionSystem._1._Core;
using Newtonsoft.Json;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Windows.Media;

namespace MvcVisionSystem._3._Device.TCP
{
    public class DefectInfo
    {
        public string ClassName { get; set; } = "";
        public float Confidence { get; set; } = 0;
        public float X { get; set; } = 0;
        public float Y { get; set; } = 0;
        public float Width { get; set; } = 0;
        public float Height { get; set; } = 0;
    }

    public class CCommunicationLearning
    {
        public enum CommandLearning
        {
            StartTraining,
            StopTraining,
            StartDefect,
            StopDefect,
        }

        private CTCPAsync Yolov5Comm { get; set; } = new CTCPAsync();
        
        public CCommunicationLearning()
        {
            Yolov5Comm.IsStringData = true;                    // 문자열로 데이터를 처리한다.
            Yolov5Comm.IsStringUnicode = false;                 // ASCII 
            Yolov5Comm.IsAutoConnectTry = true;                 // 연결이 끊어질 경우 자동으로 Retry를 수행한다.

            Yolov5Comm.nID = 2;                                 // 통신 ID는 1
            Yolov5Comm.sName = "Vision_TCP";                     // Vision 과의 연결 TCP/IP 통신
            
            Yolov5Comm.SetCallbackReceive(OnServerReceiveFunction);
            Yolov5Comm.SetCallbackConnect(OnServerConnectFunction);
            Yolov5Comm.SetCallbackDisconnect(OnServerDisconnectFunction);

            Yolov5Comm.SetListen();
        }

        public void Send(String data )
        {
            try
            {
                Yolov5Comm.Send(data );
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
        }


        public void SendTrainingData(string Command, string imgSize, string batch, string epoch, string cfg, string weight)
        {
            try
            {              
                // Write the data to the server
                Yolov5Comm.SendTrainingData(Command, imgSize, batch, epoch, cfg, weight);
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
        }

        public void SendData(String data, Bitmap bitmap)
        {
            try
            {
                Yolov5Comm.SendData(data, (Image)bitmap);
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
        }

        public Bitmap DrawDefects(Bitmap bitmap, List<DefectInfo> defects)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Calculate font size based on image width
                float fontSize = bitmap.Width / 50.0f;  // Adjust the denominator as needed
                if (fontSize < 10) fontSize = 10;  // Set a minimum font size
                if (fontSize > 20) fontSize = 20;  // Set a maximum font size

                // Define a font
                using (Font font = new Font("Arial", fontSize))
                {
                    // Define a brush
                    using (System.Drawing.Brush brush = new SolidBrush(System.Drawing.Color.Green))
                    using (System.Drawing.Brush brushNg = new SolidBrush(System.Drawing.Color.Red))
                    {
                        foreach (var defect in defects)
                        {
                            // Convert coordinates and size from float to int
                            int x = (int)defect.X;
                            int y = (int)defect.Y;
                            int width = (int)defect.Width;
                            int height = (int)defect.Height;

                            // Create a rectangle
                            Rectangle rect = new Rectangle(x, y, width, height);

                            // Calculate pen width based on image width
                            float penWidth = bitmap.Width / 800.0f;  // Adjust the denominator as needed
                            if (penWidth < 1) penWidth = 1;  // Set a minimum pen width
                            if (penWidth > 4) penWidth = 4;  // Set a maximum pen width

                            // Create a pen
                            using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.Green, penWidth))
                            using (System.Drawing.Pen penNg = new System.Drawing.Pen(System.Drawing.Color.Red, penWidth))
                            {
                                if (defect.ClassName == "OK") { g.DrawRectangle(pen, defect.X, defect.Y, defect.Width, defect.Height); }
                                else { g.DrawRectangle(penNg, defect.X, defect.Y, defect.Width, defect.Height); }
                                // Draw the rectangle
                                
                            }

                            // Define the text to display
                            string text = $"{defect.ClassName}: {defect.Confidence}";

                            // Define the location to draw the text
                            float textX = x;
                            float textY = y - font.Height > 0 ? y - font.Height : 0;

                            // Ensure that the text does not go beyond the image boundaries
                            if (textX + text.Length * fontSize > bitmap.Width)
                                textX = bitmap.Width - text.Length * fontSize;
                            if (textX < 0) textX = 0;

                            PointF point = new PointF(textX, textY);

                            if (defect.ClassName == "OK") { g.DrawString(text, font, brush, point); }
                            else { g.DrawString(text, font, brushNg, point); }
                            // Draw the text
                            
                        }
                    }
                }
            }

            return bitmap;
        }


        private void OnServerReceiveFunction(IAsyncResult ar)
        {
            byte[] byData;
            string sMsg;

            while (Yolov5Comm.GetByteData(out byData))
            {
                sMsg = Encoding.ASCII.GetString(byData, 0, byData.Length);
                CLOG.COMM($"[Receive] {sMsg}");

                if (sMsg.StartsWith("ResultDefect"))
                {
                    string jsonResult = sMsg.Substring("ResultDefect".Length).Trim();

                    // Parse the JSON string to a list of DefectInfo objects
                    List<DefectInfo> defects = JsonConvert.DeserializeObject<List<DefectInfo>>(jsonResult);
                    List<DefectInfo> truncatedDefects = defects.Select(defect => new DefectInfo
                    {
                        ClassName = defect.ClassName,
                        Confidence = (float)Math.Truncate(100 * defect.Confidence) / 100,
                        X = (float)Math.Truncate(100 * defect.X) / 100,
                        Y = (float)Math.Truncate(100 * defect.Y) / 100,
                        Width = (float)Math.Truncate(100 * defect.Width) / 100,
                        Height = (float)Math.Truncate(100 * defect.Height) / 100
                    }).ToList();
                    var image24 =  CDrawBitmap.GetBitmapFormat24bppRgb(Lib.Common.CImageConverter.ToBitmap(CDisplayManager.ImageSrc));
                    var image =  DrawDefects(image24, truncatedDefects);                    
                    CDisplayManager.CreateLayerDisplay(image, "Detect", true);
                    // Add the newly detected defects to the global defect list
                    //defectList.AddRange(defects);
                }
                else
                {
                    switch (sMsg)
                    {
                        case "StartTraining":
                            //StartTraining();                        
                            break;
                        case "StopTraining":
                            //StopTraining();
                            break;
                        case "StartDefect":
                            //StartDefect();
                            break;
                        case "StopDefect":
                            //StopDefect();
                            break;
                        default:
                            Console.WriteLine($"Unknown command: {sMsg}");
                            break;
                    }
                }
            }
        }

        // Clinet가 연결에 성공했을 때
        private void OnServerConnectFunction(IAsyncResult ar)
        {
            CTCPAsync socket = (ar.AsyncState as CTCPAsync);
            CLOG.COMM($"---- Server connect success ID:{socket.nID}, Name:{socket.sName}");
        }

        // Clinet가 연결이 끊어졌을 때
        private void OnServerDisconnectFunction(IAsyncResult ar)
        {
            CTCPAsync socket = (ar.AsyncState as CTCPAsync);
            CLOG.COMM($"**** Server Disconnect ID:{socket.nID}, Name:{socket.sName}");

            // m_log2.Write($"**** Server Disconnect ID:{socket.nID}, Name:{socket.sName}");
        }
    }
}
