using System;

namespace MvcVisionSystem
{
    public class MessageEventArgs : EventArgs
    {
        public enum MESSAGEBOX_TYPE { OKCANCEL, OK };

        private string m_strHead = "";
        public string Head
        {
            get => m_strHead;
            set => m_strHead = value;
        }

        private string m_strMessage = "";
        public string Message
        {
            get { return m_strMessage; }
            set { m_strMessage = value; }
        }

        public MessageEventArgs(string strMessage, string strHead)
        {
            m_strMessage = strMessage;
            m_strHead = strHead;
        }

        public MessageEventArgs()
        {

        }
    }

    public class StringEventArgs : EventArgs
    {
        private string m_strMessage = "";
        public string Message
        {
            get { return m_strMessage; }
            set { m_strMessage = value; }
        }

        public StringEventArgs(string strMessage)
        {
            m_strMessage = strMessage;
        }

        public StringEventArgs()
        {

        }
    }

    public class ClassItemEventArgs : EventArgs
    {
        public Yolo.CClassItem cClassItem = new Yolo.CClassItem();

        public ClassItemEventArgs(Yolo.CClassItem cClassItem)
        {
            this.cClassItem = cClassItem;   
        }
    }

    //public class LogEventArgs : EventArgs
    //{
    //    private ILog m_iLog;
    //    public ILog Log
    //    {
    //        get { return m_iLog; }
    //        set { m_iLog = value; }
    //    }

    //    public LogEventArgs(ILog iLog)
    //    {
    //        Log = iLog;
    //    }
    //}
}
