using Lib.Common;
using System;
using System.Threading;

namespace MvcVisionSystem
{
    public class CSystem
    {
        public delegate void UpdateControl();
        public delegate void UpdateLabelName(string text);

        // Delegate를 선언합니다.
        public delegate void NotifyUpdate();

        // 해당 delegate의 이벤트를 추가합니다.
        public event NotifyUpdate OnDataUpdated;

        public void UpdateData()
        {
            // 데이터를 처리하는 로직을 여기에 넣습니다.

            // 데이터가 변경되면 OnDataUpdated 이벤트를 발생시킵니다.
            OnDataUpdated?.Invoke();
        }

        public ManualResetEvent PauseWait = new ManualResetEvent(false);
        public ManualResetEvent AlarmWait = new ManualResetEvent(true);
               
        public EventHandler EventChangedMode = null;
        public EventHandler EventChangedAuthorization = null;
        public EventHandler EventChangedMenu = null;
        public EventHandler EventChangedUi = null;
        public EventHandler EventChangedNotice = null;
        public EventHandler<EventArgs> EventUpdateStyle;

        #region MODE       
        public enum MODE { READY, AUTO, ALARM, SIMULATION };
        private MODE m_eModePrev = MODE.READY;
        private MODE m_eMode = MODE.READY;
        public MODE Mode
        {
            get { return m_eMode; }
            set
            {
                m_eModePrev = m_eMode;
                m_eMode = value;

                if (m_eMode != m_eModePrev)
                {
                    if (EventChangedMode != null) { EventChangedMode(null, null); }                   
                }
            }
        }
        #endregion

        #region MENU     
        public enum MENU { MAIN, VISION };

        public MENU m_SelectedMenu = MENU.MAIN;
        public MENU Menu
        {
            get { return m_SelectedMenu; }
            set
            {
                m_SelectedMenu = value;
                if (EventChangedMenu != null) { EventChangedMenu(null, null); }               
            }
        }        
        #endregion

        #region NOTICE
        private string m_strNotice = "-";
        public string Notice
        {
            get { return m_strNotice; }
            set
            {
                m_strNotice = value;

                if (m_strNotice != "")
                {
                    if (EventChangedNotice != null) { EventChangedNotice(null, null); }                   
                }
                AppLog.NORMAL(Notice);
            }
        }        
        #endregion

        #region RESULT
        public enum RESULT { IDLE, OK, NG };
        private RESULT m_eResult = RESULT.IDLE;
        public RESULT Result
        {
            get { return m_eResult; }
            set { m_eResult = value; }
        }
        #endregion

        #region UI
        private string m_strProcdure = "READY";
        public string PROCDURE
        {
            get => m_strProcdure;
            set
            {
                m_strProcdure = value;
                AppLog.SEQ( "PROCDURE : {0}", m_strProcdure);
            }
        }

        private int m_nStyleIndex = 6;
        public int StyleIndex
        {
            get => m_nStyleIndex;
            set
            {
                m_nStyleIndex = value;

                if (EventUpdateStyle != null)
                {
                    EventUpdateStyle(null, null);
                }
            }
        }

        #endregion

        #region IPC
        public IntPtr IF_Handle { get; set; } = IntPtr.Zero;
        #endregion

        #region PROPERTIES

        private DEFINE.AUTHORIZATION m_Authorization = DEFINE.AUTHORIZATION.OPERATOR;
        public DEFINE.AUTHORIZATION Authorization
        {
            get => m_Authorization;
            set
            {
                m_Authorization = value;

                if (EventChangedAuthorization != null)
                {
                    EventChangedAuthorization(this, new EventArgs());
                }
            }
        }

        #endregion

        public CSystem()
        {
            CUtil.InitDirectory("IMAGE");
            CUtil.InitDirectory("SAVE_IMAGE");
            CUtil.InitDirectory("RECIPE");            
            CUtil.InitDirectory("CAPTURE");
            CUtil.InitDirectory("CONFIG");
        }

        public void Close() { }        
    }
}
