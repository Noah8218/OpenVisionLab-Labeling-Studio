using MvcVisionSystem._1._Core;
using Lib.Common;
using System;
using System.Reflection;
using MvcVisionSystem._3._Device.TCP;
using System.Xml.Serialization;

namespace MvcVisionSystem
{
    public static class CVersion
    {        
        public static string VERSION { get; set; } = "1.5.0";
        public static string DATETIME_UPDATED { get; set; } = "2026/06/03 /*20:00*/";
        public static string MANAGER { get; set; } = "NOAH";
    }
    
    public class CGlobal
    {               
        // 싱글톤(객체 접근시에만 객체를 생성)->지연 생성
        private static readonly Lazy<CGlobal> instance = new Lazy<CGlobal>(() => new CGlobal());

        public static CGlobal Inst
        {
            get { return instance.Value; }
        }
        
        // 레시피 관리 클래스(실행폴더//Recipe)
        // 레시피가 변경되면, 장치,스팩,파라미터 등 관련 값들을 Load
        public CRecipe Recipe { get; set; } = new CRecipe();
        // 모드, 권한, 창 변경 등 System 관련 클래스
        public CSystem System { get; set; } = new CSystem();
        // 장치(카메라,io,조명,모션) 등 관리 클래스
        public CDevice Device { get; set; } = new CDevice();
        // 상시로 스레드가 돌면서 큐에 이미지가 들어오면 검사
        // 유동적으로 알아서 변경 사용
        //public CSeqVision SeqVision { get; set; } = new CSeqVision();        
        // 스팩,파라미터, 판정값등 각종 검사에 사용되는 값들을 관리
        public CData Data { get; set; } = new CData();
        public CSeqThread Thread { get; set; } = new CSeqThread();

        public CCommunicationLearning DeepLearning { get; set; } = new CCommunicationLearning();

        public CGlobal() { }        

        public bool Close()
        {
            try
            {
                Thread.Stop();
                System.Close();
                Device.Close();                                
                return true;
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL( $"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
                return false;
            }
        }
    }
}
