//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// C# Gaus Library
// 
// Gaus.Comm : TCP/IP Socket 통신 및 RS232/485 Serial 통신 class
//
// 2020-02-25, jhLee
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using Lib.Common;

namespace MvcVisionSystem._3._Communication.TCP
{
    // Gaus TCP/IP Socket class
    // 비동기방식의 socket class로 Server와 Client를 동시에 지원한다.
    // 문자열 및 바이트 배열을 전송하고, 문자열은 Unicode와 ASCII를 지원한다.
    // OnConnect, OnDisconnect, OnReceive Callback 함수를 지원한다.
    // CGxLog를 통해 화면과 file로 전송데이터 및 이벤트/예외 내용을 저장 가능하다.

    public class CTCPAsync
    // Server socket class
    {
        public bool bConnected = false;

        public List<byte[]> listRcvData = new List<byte[]>();        // Byte 배열을 가지는 list queue
        private readonly object receiveQueueLock = new object();
        private readonly object socketLock = new object();
        private volatile bool isListenActive;
        private volatile bool isClosing;

        public Socket socketServer = null;
        public Socket socketClient = null;                  // client Socket class

        public const int BufferSize = 4096;                    // Size of receive buffer.  
        public byte[] byBuffer = new byte[BufferSize];      // Receive buffer. 
        // public byte[] byPopData = null;                     // 수신 list에서 사용을 위해 빼어낸 데이터의 임시 보관용
        // public StringBuilder sb = new StringBuilder();      // Received data string. 

        // 송수신 데이터 길이
        public int nSendLength = 0;                         // 송신 완료 길이
        public int nRecvLength = 0;                         // 수신 완료 길이

        public string sMyName;
        public string sIPAddress = "";                      // 접속 할 IP 주소
        public int nPortNo = 5000;                          // 접속 할 Port 주소

        public IAsyncResult arConnect;                      // BeginConnect의 결과를 담아둔다.
        public int nAbnormalCount = 0;                      // 비정상 상태 반복횟수
        private bool bAutoConnectFlag = false;              // 자동 접속 동작을 활성화할 것인가 ?

        System.Timers.Timer tmrTryConnect = new System.Timers.Timer();  // 접속용 Timer

        // 비동기 처리시 각종 수행 완료 event
        public ManualResetEvent evtConnectDone;             // Connect 완료 event
        public ManualResetEvent evtSendDone;                // Send 완료 event
        public ManualResetEvent evtReceiveDone;             // Receive 완료 event
        public ManualResetEvent evtReceiveFlag;             // Receive 완료 event, 수신 데이터 처리를 위한 Event


        // 각종 Event callback 함수
        private AsyncCallback fnAcceptHandle = null;        // Accept event
        private AsyncCallback fnSendHandle = null;          // Send event
        private AsyncCallback fnReceiveHandle = null;       // Receive event
        private AsyncCallback fnConnectHandle = null;       // Connect event  


        // 최종 사용단에서 각 이벤트 발생시 처리 할 내용 지정
        private AsyncCallback fnCallbackConnect = null;     // 연결이 이루어질 경우 발생되는 callback 함수
        private AsyncCallback fnCallbackDisconnect = null;  // 연결이 끊어졌을 경우 발생되는 callback 함수
        private AsyncCallback fnCallbackReceive = null;     // 데이터가 수신되었을때 수행되는 callback 함수


        // Property

        private string m_strIp = "127.0.0.1";
        public string IP
        {
            get => m_strIp;
            set => m_strIp = value;
        }

        private int m_nPort = 5000;
        public int Port
        {
            get => m_nPort;
            set => m_nPort = value;
        }

        public string sName { get; set; } = "NONE";         // 객체 이름 지정
        public int nID { get; set; } = 0;                   // 구분 ID 지정

        public bool IsUnicode { get; set; } = false;        // 전송시 Unicode 문자열을 이용하는가 ? true:Unicode, false:ASCII
        public Encoding TextEncoding { get; set; } = Encoding.ASCII;

        // Log 기록관련
        public bool IsStringData { get; set; } = true;      // 전송데이터가 문자열인가 바이너리인가 ?
        public bool IsStringUnicode { get; set; } = false;  // 문자열을 Unicode로 송/수신 할것인가 ?
        public bool IsLogData { get; set; } = true;         // Send / Receive 동작시 문자열로 데이터를 log에 남길것인가 ?
        public bool IsLogLength { get; set; } = true;       // 송/수신 데이터 길이를 log 에 남길것인가 ?

        public bool IsLogException { get; set; } = true;    // 예외 발생을 log에 남길것인가 ?
        public bool IsLogEvent { get; set; } = true;        // Event 발생을 log에 남길것인가 ?
        public bool IsLogEventReceive { get; set; } = true;    // Receive event발생시 log에 남길것인가 ? (자주 발생되는 이벤트 로그기록)

        // 재접속 관련 설정
        public bool IsAutoConnectTry { get; set; } = false;     // 자동으로 재접속을 시도한다.
        public int ConnectTimeout { get; set; } = 1500;     // Client connect timeout
        public int ConnectDelay { get; set; } = 3000;       // 얼마의 시간 뒤에 다시 재접속을 시도할 것인가 ?


        public CTCPAsync() // 생성시 초기화
        {
            fnAcceptHandle = new AsyncCallback(OnAcceptCallback);
            fnSendHandle = new AsyncCallback(OnSendCallback);
            fnReceiveHandle = new AsyncCallback(OnReceiveCallback);
            fnConnectHandle = new AsyncCallback(OnConnectCallback);

            // 비동기 처리시 각종 수행 완료 event
            evtConnectDone = new ManualResetEvent(false);             // Connect 완료 event
            evtSendDone = new ManualResetEvent(false);                // Send 완료 event
            evtReceiveDone = new ManualResetEvent(false);             // Receive 완료 event
            evtReceiveFlag = new ManualResetEvent(false);             // Receive 완료 event, 수신 데이터 처리를 위한 Event

            // 접속 시도 Timer 기본 설정
            tmrTryConnect.Interval = 2000;                      // 이벤트 발생 주기
            tmrTryConnect.AutoReset = false;                    // 이벤트를 1번만 발생시킨다.
            tmrTryConnect.Elapsed += OnTryConnectEvent;         // 재접속 시도 이벤트 지정
        }

        private sealed class SendState
        {
            public SendState(Socket socket, byte[] buffer)
            {
                Socket = socket;
                Buffer = buffer;
            }

            public Socket Socket { get; }
            public byte[] Buffer { get; }
            public int Offset { get; set; }
        }

        public void Send(String data)
        {
            try
            {                
                byte[] byteData = GetTextEncoding().GetBytes(data);

                if (SendBytesAsync(byteData))
                {
                    AppLog.COMM($"SEND ==> {data}");
                }
            }
            catch (Exception Desc)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                }
            }
        }

        public bool SendBytesAsync(byte[] sendData)
        {
            if (sendData == null || sendData.Length <= 0) return false;
            if (socketClient == null || !socketClient.Connected) return false;

            try
            {
                evtSendDone.Reset();
                nSendLength = 0;

                var sendState = new SendState(socketClient, sendData);
                socketClient.BeginSend(sendData, 0, sendData.Length, SocketFlags.None, new AsyncCallback(SendCallback), sendState);
                return true;
            }
            catch (Exception Desc)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                }
                return false;
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                SendState sendState = ar.AsyncState as SendState;
                if (sendState == null) return;

                int bytesSent = sendState.Socket.EndSend(ar);
                if (bytesSent <= 0) return;

                sendState.Offset += bytesSent;
                if (sendState.Offset < sendState.Buffer.Length)
                {
                    sendState.Socket.BeginSend(
                        sendState.Buffer,
                        sendState.Offset,
                        sendState.Buffer.Length - sendState.Offset,
                        SocketFlags.None,
                        new AsyncCallback(SendCallback),
                        sendState);
                    return;
                }

                nSendLength = sendState.Offset;
                evtSendDone.Set();
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }
        }


        // 선두의 데이터를 바이트 배열로 받아온다. (원본 삭제)
        // return 데이터를 정상적으로 받아왔는지 여부, true:정상적으로 데이터를 취득하였다. false:데이터가 존재하지 않는다.
        public bool GetByteData(out byte[] OutData)
        {
            lock (receiveQueueLock)
            {
                if (listRcvData.Count > 0)              // 수신 데이터가 존재하는가 ?
                {
                    OutData = new byte[listRcvData[0].Length];
                    Array.Copy(listRcvData[0], 0, OutData, 0, listRcvData[0].Length);       // 배열을 복사한다.
                    listRcvData.RemoveAt(0);                                                // 선두 데이터를 삭제한다.

                    return true;
                }
            }

            OutData = null;
            return false;                           // 데이터가 존재하지 않는다.
        }


        // 선두의 데이터를 문자열로 받아온다. (원본 삭제)
        // return 데이터를 정상적으로 받아왔는지 여부, true:정상적으로 데이터를 취득하였다. false:데이터가 존재하지 않는다.
        public bool GetStringData(out string OutMsg)
        {
            byte[] data = null;
            lock (receiveQueueLock)
            {
                if (listRcvData.Count > 0)              // 수신 데이터가 존재하는가 ?
                {
                    data = new byte[listRcvData[0].Length];
                    Array.Copy(listRcvData[0], 0, data, 0, listRcvData[0].Length);
                    listRcvData.RemoveAt(0);                                                // 선두 데이터를 삭제한다.
                }
            }

            if (data != null)
            {
                try
                {
                    if (IsStringUnicode)
                    {
                        OutMsg = Encoding.Unicode.GetString(data, 0, data.Length);  // Unicode
                    }
                    else
                        OutMsg = GetTextEncoding().GetString(data, 0, data.Length);

                    return true;
                }
                catch (Exception Desc)
                {
                    AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                }
            }

            OutMsg = "";
            return false;                           // 데이터가 존재하지 않는다.
        }


        // Event callback 함수 대입
        public void SetCallbackFunction(AsyncCallback fnConnect, AsyncCallback fnDisconnect, AsyncCallback fnReceive)
        {
            fnCallbackConnect = fnConnect;          // 연결이 이루어질 경우 발생되는 callback 함수
            fnCallbackDisconnect = fnDisconnect;    // 연결이 끊어졌을 경우 발생되는 callback 함수
            fnCallbackReceive = fnReceive;          // 데이터가 수신되었을때 수행되는 callback 함수
        }

        // 개별적인 callback 함수 대입
        // 연결이 된 경우 호출
        public void SetCallbackConnect(AsyncCallback fn)
        {
            fnCallbackConnect = fn;         // 연결이 이루어질 경우 발생되는 callback 함수
        }

        // 연결이 끊어진 경우 호출
        public void SetCallbackDisconnect(AsyncCallback fn)
        {
            fnCallbackDisconnect = fn;     // 연결이 끊어졌을 경우 발생되는 callback 함수
        }

        // 데이터가 수신된 경우 호출
        public void SetCallbackReceive(AsyncCallback fn)
        {
            fnCallbackReceive = fn;      // 데이터가 수신되었을때 수행되는 callback 함수
        }


        // Client socket이 재접속을 하도록 만들어주는 Timer event 생성
        private void OnTryConnectEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            if (isClosing || isListenActive || bAutoConnectFlag == false)
            {
                return;
            }

            AppLog.COMM($"The Elapsed event was raised at {0:HH:mm:ss.fff}", e.SignalTime);
            
            // Client socket이 초기화 되었다면 새로이 생성하고 연결을 시도한다.
            if (socketClient == null)
            {
                // 자동 연결에 대한 동작이 중지되었다면 아무런 수행도 하지 않는다.
                // (사용자가 명시적으로 ClientClose를 수행한 경우)
                if (bAutoConnectFlag == false) return;

                // 연결을 시도하고 연결 지령을 성공적으로 수행하지 못했다면
                if (Connect() == false)
                {
                    try
                    {
                        socketClient.Close();           // socket  객체를 닫고 초기화 해준다.
                    }
                    catch (Exception Desc)
                    {
                        AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                    }

                    socketClient = null;

                    tmrTryConnect.Interval = ConnectDelay;      // 다시 연결을 지령하기위한 지연 시간
                }
                else // 연결 요청 성공
                    tmrTryConnect.Interval = ConnectTimeout;    // 연결대기 시간초과

                tmrTryConnect.Start();          // 다시 타이머를 동작시킨다.
            }
            else      // socket 객체가 생성되어있다면, 이전 단계에서 connect를 시도하였다.
            {
                // 아직까지 연결이 이루어지지 않았다면 Timeout으로 간주한다.
                if (IsConnected() == false)
                {
                    try
                    {
                        socketClient.Close();                       // 연결을 중지시킨다.
                    }
                    catch (Exception Desc)
                    {
                        AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                    }
                }

                // Close 이후로는 Receive event가 발생하면서 Read size가 0이 되어 disconnect 처리하게 되어있다.
                // 그리고 이 Timer는 1회성 timer이므로 명시적으로 start 시켜주지 않으면 반복되지 않게된다.
            }
        }


        // 수신대기를 시작한다.
        //public bool SetListen()                              // Server 수신대기를 시작한다.
        //{
        //    return SetListen();                      // 내정된 Port로 listen을 수행한디ㅏ.
        //}

        // 지정 Port로 수신대기를 시작한다.
        public bool SetListen()                    // Server 수신대기를 시작한다.
        {
            isClosing = false;
            IPAddress ipAddress = IPAddress.Parse(m_strIp);            
            IPEndPoint localEPoint = new IPEndPoint(ipAddress, Port);         // 포트 지정

            try
            {
                Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);    // server Socket class
                server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                server.Bind(localEPoint);                           // 지정 포트로 바인딩 한다.
                server.Listen(100);

                lock (socketLock)
                {
                    socketServer = server;
                    isListenActive = true;
                    bConnected = true;
                }

                server.BeginAccept(fnAcceptHandle, this); //  socketServer);   // Accept call back 함수를 지정한다.


                AppLog.COMM($"!!SetListen({Port}) ok");                
                return true;
            }
            catch (SocketException ex)
            {
                AppLog.ABNORMAL($"TCP listen failed. ID:{nID}, Name:{sName}, Endpoint:{m_strIp}:{Port}, SocketError:{ex.SocketErrorCode}, Message:{ex.Message}");
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }

            return false;
        }

        // Server의 연결 대기를 종료한다.
        public void StopListen()
        {
            Socket client = null;
            Socket server = null;

            try
            {
                isClosing = true;
                bAutoConnectFlag = false;
                tmrTryConnect.Stop();
                lock (socketLock)
                {
                    isListenActive = false;
                    client = socketClient;
                    server = socketServer;
                    socketClient = null;
                    socketServer = null;
                    bConnected = false;
                }

                evtConnectDone.Reset();
                evtReceiveDone.Reset();
                evtReceiveFlag.Reset();
                lock (receiveQueueLock)
                {
                    listRcvData.Clear();
                }

                CloseSocketQuietly(client);
                CloseSocketQuietly(server);
            }
            catch (Exception Desc)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                }
            }

        }

        // Client 연결을 닫는다.
        public void CloseClient()
        {
            try
            {
                isClosing = true;
                bAutoConnectFlag = false;                       // 명시적으로 연결을 끊는경우에는 자동 재접속을 수행하지 않도록 한다.
                Socket client = socketClient;
                if (client == null)
                {
                    return;
                }

                client.Disconnect(false);                       // 전송중인 데이터를 모두 종료하고 연결을 끊는다.

                // 이후 Receive event가 발생하며 Reading 길이가 0인 경우가 발생하므로 이를 최종 Close 단계로 인식하면 된다.

                //d socketClient.Shutdown(SocketShutdown.Both);  // 즉시 연결을 끊는다. 
                //d socketClient.Close();
                //d socketClient = null;                
                AppLog.COMM("Close server connected Client ...");
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }
        }

        private static void CloseSocketQuietly(Socket socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }

            try
            {
                socket.Close();
            }
            catch
            {
            }
        }

        // 연결 요청이 발생하면 호출될 콜백 함수
        public void OnAcceptCallback(IAsyncResult ar)
        {
            Socket server = socketServer;
            if (isClosing || !isListenActive || server == null)
            {
                evtConnectDone.Reset();
                return;
            }

            Socket acceptedClient = null;
            bool clientActivated = false;
            // 접속을 허용시킨다.
            try
            {
                // Get the socket that handles the client request.  
                acceptedClient = server.EndAccept(ar);                // Accept를 끝낸다.
                if (isClosing || !isListenActive)
                {
                    CloseSocketQuietly(acceptedClient);
                    evtConnectDone.Reset();
                    return;
                }

                lock (socketLock)
                {
                    socketClient = acceptedClient;
                }

                acceptedClient.BeginReceive(byBuffer, 0, byBuffer.Length, 0, fnReceiveHandle, this);   // 수신 Callback 함수를 대입한다.
                clientActivated = true;

                evtConnectDone.Set();               // 연결이 설정되었다.

                if (fnCallbackConnect != null)
                {
                    try
                    {
                        fnCallbackConnect(ar);          // 연결이 이루어질 경우 발생되는 callback 함수 호출
                    }
                    catch (Exception ex)
                    {
                        if (!isClosing)
                        {
                            AppLog.ABNORMAL($"TCP connect callback failed. ID:{nID}, Name:{sName}, Error:{ex.Message}");
                        }
                    }
                }

                // 연결이 끊어 진 뒤 다시 재연결이 가능하도록 Accept를 재가동 시킨다.
                Socket currentServer = socketServer;
                if (!isClosing && isListenActive && currentServer != null && ReferenceEquals(currentServer, server))
                {
                    try
                    {
                        currentServer.BeginAccept(fnAcceptHandle, this); //  socketServer);   // Accept call back 함수를 지정한다.
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (Exception ex)
                    {
                        if (!isClosing)
                        {
                            AppLog.ABNORMAL($"TCP accept restart failed. ID:{nID}, Name:{sName}, Error:{ex.Message}");
                        }
                    }
                }

                AppLog.COMM($"!! OnAcceptCallback() EndAccept : {acceptedClient.Connected}");                
                return;
            }
            catch (ObjectDisposedException)
            {
                if (!isClosing && isListenActive)
                {
                    AppLog.ABNORMAL("Socket 개체가 닫혔습니다.");
                }
            }
            catch (ArgumentNullException)
            {
                AppLog.ABNORMAL("asyncResult가 비어 있습니다.");                
            }
            catch (ArgumentException)
            {
                AppLog.ABNORMAL($"BeginAccept(AsyncCallback, Object)를 호출했지만 asyncResult가 만들어지지 않았습니다.");                                
            }
            catch (InvalidOperationException)
            {
                AppLog.ABNORMAL("EndAccept(IAsyncResult) 메서드가 이미 호출되었습니다.");                
            }
            catch (SocketException)
            {
                if (!isClosing && isListenActive)
                {
                    AppLog.ABNORMAL( "Socket에 액세스하려고 시도하는 동안 오류가 발생했습니다.");
                }
            }
            catch (Exception Desc)
            {
                if (isListenActive)
                {
                    AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                }
            }

            evtConnectDone.Reset();         // 연결이 해제되었다.

            if (!clientActivated)
            {
                CloseSocketQuietly(acceptedClient);
            }

            if (!isClosing && isListenActive)
            {
                AppLog.COMM("---- OnAcceptCallback() EndAccept ----");
            }
        }


        // Client가 접속 할 주소를 지정한다.
        public void SetAddress(string sAddr, int nPort)
        {
            sIPAddress = sAddr;                      // 접속 할 IP 주소
            nPortNo = nPort;                         // 접속 할 Port 주소
        }


        // 기존에 설정된 주소로 접속을 시도한다.
        public bool Connect()
        {
            return Connect(sIPAddress, nPortNo);
        }


        // Client socket이 begin connect를 수행한다.
        public bool Connect(string sAddr, int nPort)
        {
            isClosing = false;
            bAutoConnectFlag = true;

            if (socketClient == null)
            {
                socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);    // server Socket class
                AppLog.COMM($"Create new stocket instance {sAddr}:{nPort}");                
            }

            try
            {
                AppLog.COMM($"!! BeginConnect to {sAddr}:{nPort}");
                                
                arConnect = socketClient.BeginConnect(sAddr, nPort, fnConnectHandle, this); //  socketClient);
                
                return true;
            }
            catch (ArgumentNullException)
            {
                AppLog.ABNORMAL("host이(가) null입니다.");            
            }
            catch (ArgumentOutOfRangeException)
            {
                AppLog.ABNORMAL("포트 번호가 잘못되었습니다.");                
            }
            catch (SocketException ex)
            {
                AppLog.ABNORMAL($"TCP connect failed. ID:{nID}, Name:{sName}, Endpoint:{sAddr}:{nPort}, SocketError:{ex.SocketErrorCode}, Message:{ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                AppLog.ABNORMAL("Socket이 닫혔습니다.");                
            }
            catch (NotSupportedException)
            {
                AppLog.ABNORMAL("이 메서드는 InterNetwork 또는 InterNetworkV6 제품군의 소켓에 유효합니다.");                
            }
            catch (InvalidOperationException)
            {
                AppLog.ABNORMAL("Socket이 Listen(Int32)을 호출하여 수신 상태에 배치되었습니다.");                
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }

            return false;       // 예외 발생시 수행 실패
        }


        public bool IsConnected()
        {
            if (socketClient == null) return false;

            try
            {
                return !((socketClient.Poll(1000, SelectMode.SelectRead) && (socketClient.Available == 0)) || !socketClient.Connected);
            }
            catch (ArgumentNullException)
            {
                AppLog.ABNORMAL("host이(가) null입니다.");
            }
            catch (ArgumentOutOfRangeException)
            {
                AppLog.ABNORMAL("포트 번호가 잘못되었습니다.");
            }
            catch (SocketException)
            {
                AppLog.ABNORMAL("소켓에 액세스하는 동안 오류가 발생했습니다.");
            }
            catch (ObjectDisposedException)
            {
                AppLog.ABNORMAL("Socket이 닫혔습니다.");
            }
            catch (NotSupportedException)
            {
                AppLog.ABNORMAL("이 메서드는 InterNetwork 또는 InterNetworkV6 제품군의 소켓에 유효합니다.");
            }
            catch (InvalidOperationException)
            {
                AppLog.ABNORMAL("Socket이 Listen(Int32)을 호출하여 수신 상태에 배치되었습니다.");
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }

            return false;
        }



        // 접속 시도에 대한 Callback event 발생
        public void OnConnectCallback(IAsyncResult ar)
        {
            Socket client = socketClient;
            if (isClosing || client == null)
            {
                evtConnectDone.Reset();
                return;
            }

            try
            {
                client.EndConnect(ar);            // 연결시도 종료
                if (isClosing)
                {
                    CloseSocketQuietly(client);
                    evtConnectDone.Reset();
                    return;
                }

                client.BeginReceive(byBuffer, 0, byBuffer.Length, 0, fnReceiveHandle, this); // socketClient);   // 수신 Callback 함수를 대입한다.

                tmrTryConnect.Stop();                   // Timeout 처리 및 재접속 timer 중지
                nAbnormalCount = 0;                     // 비정상 상태 반복횟수
                evtConnectDone.Set();                   // 연결이 설정되었다.

                if (fnCallbackConnect != null)
                {
                    try
                    {
                        fnCallbackConnect(ar);              // 연결이 이루어질 경우 발생되는 callback 함수
                    }
                    catch (Exception ex)
                    {
                        if (!isClosing)
                        {
                            AppLog.ABNORMAL($"TCP connect callback failed. ID:{nID}, Name:{sName}, Error:{ex.Message}");
                        }
                    }
                }

                AppLog.COMM($"!! OnConnectCallback(), connected : {client.Connected}");                
                return;
            }
            catch (ArgumentNullException)
            {
                AppLog.ABNORMAL("host이(가) null입니다.");
            }
            catch (ArgumentOutOfRangeException)
            {
                AppLog.ABNORMAL("포트 번호가 잘못되었습니다.");
            }
            catch (SocketException)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL("소켓에 액세스하는 동안 오류가 발생했습니다.");
                }
            }
            catch (ObjectDisposedException)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL("Socket이 닫혔습니다.");
                }
            }
            catch (NotSupportedException)
            {
                AppLog.ABNORMAL("이 메서드는 InterNetwork 또는 InterNetworkV6 제품군의 소켓에 유효합니다.");
            }
            catch (InvalidOperationException)
            {
                AppLog.ABNORMAL("Socket이 Listen(Int32)을 호출하여 수신 상태에 배치되었습니다.");
            }
            catch (Exception Desc)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                }
            }

            // 정상적이지 않다.
            if (!isClosing)
            {
                AppLog.ABNORMAL($"OnConnectCallback(), connected fail. ID:{nID}, Name:{sName}, Endpoint:{sIPAddress}:{nPortNo}, AutoRetry:{IsAutoConnectTry}");
            }

            CloseSocketQuietly(client);
            if (ReferenceEquals(socketClient, client))
            {
                socketClient = null;
            }
            evtConnectDone.Reset();         // 연결이 해제되었다.

            // 자동으로 재접속을 시도하기로 지정되었다면
            if (!isClosing && IsAutoConnectTry)
            {
                tmrTryConnect.Interval = ConnectDelay;      // 다시 연결을 지령하기위한 지연 시간
                tmrTryConnect.Start();          // 다시 타이머를 동작시킨다.
            }

        }


        // 데이터 수신이 이루어졌을때 발생하는 Callback 함수
        public void OnReceiveCallback(IAsyncResult ar)
        {
            nRecvLength = 0;  
            Socket client = socketClient;
            if (isClosing || client == null)
            {
                evtConnectDone.Reset();
                return;
            }

            try
            {
                // 자료를 수신하고, 수신받은 바이트를 가져옵니다.
                nRecvLength = client.EndReceive(ar);
            }
            catch (ArgumentNullException)
            {
                AppLog.ABNORMAL("host이(가) null입니다.");
            }
            catch (ArgumentOutOfRangeException)
            {
                AppLog.ABNORMAL("포트 번호가 잘못되었습니다.");
            }
            catch (SocketException)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL("소켓에 액세스하는 동안 오류가 발생했습니다.");
                }
            }
            catch (ObjectDisposedException)
            {
                if (!isClosing && bConnected)
                {
                    AppLog.ABNORMAL("Socket이 닫혔습니다.");
                }
                evtConnectDone.Reset();
                return;
            }
            catch (NotSupportedException)
            {
                AppLog.ABNORMAL("이 메서드는 InterNetwork 또는 InterNetworkV6 제품군의 소켓에 유효합니다.");
            }
            catch (InvalidOperationException)
            {
                AppLog.ABNORMAL("Socket이 Listen(Int32)을 호출하여 수신 상태에 배치되었습니다.");
            }
            catch (Exception Desc)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                }
            }

            if (isClosing)
            {
                evtConnectDone.Reset();
                return;
            }


            // 수신받은 자료의 크기가 1 이상일 때에만 자료 처리
            if (nRecvLength > 0)
            {
                byte[] data = new byte[nRecvLength];            // 수신된 데이터 수 만큼 배열을 잡는다.

                Array.Copy(byBuffer, 0, data, 0, nRecvLength);  // 수신된 길이만큼 내용을 복사한다

                lock (receiveQueueLock)
                {
                    listRcvData.Add(data);                          // 복사된 수신 데이터를 list에 보관한다.
                }

                
                if (IsLogData) // 송수신 메세지를 로그로 기록하라고 되어있다면
                {

                    if (IsStringData)      // 전송데이터가 문자열인가 바이너리인가 ?
                    {
                        string sMsg;

                        if (IsStringUnicode) // 문자열을 Unicode로 송/수신 할것인가 ?
                        {
                            sMsg = Encoding.Unicode.GetString(byBuffer, 0, nRecvLength);
                        }
                        else
                            sMsg = GetTextEncoding().GetString(byBuffer, 0, nRecvLength);
                    }
                    else
                    {       // Binary data
                        StringBuilder sbMsg = new StringBuilder();

                        for (int i = 0; i < data.Length; i++)
                        {
                            sbMsg.Append($"{data[i]:X2} ");     // 2자리 16진수로 표시
                        }
                    }

                }

                // 수신데이터 처리를 위한 event set 최종 사용자가 사용한다.
                evtReceiveFlag.Set();

                // 수신완료 함수 수행
                if (fnCallbackReceive != null)
                {
                    try
                    {
                        fnCallbackReceive(ar);              // 데이터 수신이 이루어질 경우 발생되는 callback 함수
                    }
                    catch (Exception ex)
                    {
                        if (!isClosing)
                        {
                            AppLog.ABNORMAL($"TCP receive callback failed. ID:{nID}, Name:{sName}, Error:{ex.Message}");
                        }
                    }
                }

                // byBuffer.Initialize(); -> 정상 동작하지 않음
                Array.Clear(byBuffer, 0x0, byBuffer.Length);            // Buffer Clear

                // 다음 수신을 대기한다.
                if (!isClosing && socketClient != null && ReferenceEquals(socketClient, client) && client.Connected)
                {
                    try
                    {
                        client.BeginReceive(byBuffer, 0, byBuffer.Length, 0, fnReceiveHandle, this); // socketClient);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (Exception ex)
                    {
                        if (!isClosing)
                        {
                            AppLog.ABNORMAL($"TCP receive restart failed. ID:{nID}, Name:{sName}, Error:{ex.Message}");
                        }
                    }
                }
            }
            else 
            {
                // 수신된 Size가 0일 경우 연결이 끊어진 것이다.
                evtConnectDone.Reset();         // 연결이 해제되었다.

                if (!isClosing && fnCallbackDisconnect != null)
                {
                    try
                    {
                        fnCallbackDisconnect(ar);              // 연결이 끊어질 경우 발생되는 callback 함수
                    }
                    catch (Exception ex)
                    {
                        if (!isClosing)
                        {
                            AppLog.ABNORMAL($"TCP disconnect callback failed. ID:{nID}, Name:{sName}, Error:{ex.Message}");
                        }
                    }
                }

                lock (socketLock)
                {
                    if (ReferenceEquals(socketClient, client))
                    {
                        socketClient = null;
                    }
                }

                CloseSocketQuietly(client);

                // 자동으로 재접속을 시도하기로 지정되었다면
                if (!isClosing && !isListenActive && IsAutoConnectTry)
                {
                    tmrTryConnect.Interval = ConnectDelay;      // 다시 연결을 지령하기위한 지연 시간
                    tmrTryConnect.Start();          // 다시 타이머를 동작시킨다.
                }

            }
        }

        // 데이터 송신이 이루어졌을때 발생하는 Callback 함수
        public void OnSendCallback(IAsyncResult ar)
        {
            Socket client = socketClient;
            if (isClosing || client == null)
            {
                evtSendDone.Set();
                return;
            }

            try
            {
                nSendLength = client.EndSend(ar);                     // 송신 처리
                evtSendDone.Set();

               // if (IsLogEventReceive) log.Write($"!! OnSendCallback : {nSendLength}");
            }
            catch (Exception Desc)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                }

                nSendLength = 0;                                            // 송신 실패
                                                                            // if (IsLogException) log.Write($"** OnSendCallback() SocketException : {exp.Message}");

                evtConnectDone.Reset();         // 연결이 해제되었다.

            }
        }


        // 문자열을 전송한다. (비동기 방식)
        public bool SendStringASync(string sMsg)
        {
            if (sMsg.Length <= 0) return false;                     // 전송 할 내용이 없다면 전송 실패


            byte[] data = GetTextEncoding().GetBytes(sMsg);

            try
            {
                // 지정 데이터를 전송 시도한다.
                nSendLength = 0;
                evtSendDone.Reset();

                socketClient.BeginSend(data, 0, data.Length, 0, fnSendHandle, this);    // socketClient);

                nAbnormalCount = 0;                      // 비정상 상태 반복횟수 clear

                return true;
            }
            catch (ArgumentNullException)
            {
                AppLog.ABNORMAL("host이(가) null입니다.");
            }
            catch (ArgumentOutOfRangeException)
            {
                AppLog.ABNORMAL("포트 번호가 잘못되었습니다.");
            }
            catch (SocketException)
            {
                AppLog.ABNORMAL("소켓에 액세스하는 동안 오류가 발생했습니다.");
            }
            catch (ObjectDisposedException)
            {
                AppLog.ABNORMAL("Socket이 닫혔습니다.");
            }
            catch (NotSupportedException)
            {
                AppLog.ABNORMAL("이 메서드는 InterNetwork 또는 InterNetworkV6 제품군의 소켓에 유효합니다.");
            }
            catch (InvalidOperationException)
            {
                AppLog.ABNORMAL("Socket이 Listen(Int32)을 호출하여 수신 상태에 배치되었습니다.");
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }

            ++nAbnormalCount;                      // 비정상 상태 반복횟수
            return false;
        }


        // 문자열을 전송한다. (동기 방식)
        public bool SendString(string sMsg)
        {
            if (sMsg.Length <= 0) return false;                     // 전송 할 내용이 없다면 전송 실패

            byte[] data;

            if (IsStringUnicode) // 문자열을 Unicode로 송/수신 할것인가 ?
            {
                // sMsg = Encoding.Unicode.GetString(byBuffer, 0, nRecvLength);
                data = Encoding.Unicode.GetBytes(sMsg);
            }
            else
                data = GetTextEncoding().GetBytes(sMsg);

            // byte[] data = Encoding.Unicode.GetBytes(sMsg);
            // byte[] data = Encoding.ASCII.GetBytes(sMsg);

            try
            {
                // 지정 데이터를 전송 시도한다.
                evtSendDone.Reset();
                nSendLength = socketClient.Send(data, data.Length, 0);  // , MSG_NOSIGNAL);

                nAbnormalCount = 0;                                   // 비정상 상태 반복횟수 clear
                evtSendDone.Set();

                if (IsLogData) // 송수신 메세지를 로그로 기록하라고 되어있다면
                {

                    if (IsStringData)      // 전송데이터가 문자열인가 바이너리인가 ?
                    {
                    }
                    else
                    {       // Binary data
                        StringBuilder sbMsg = new StringBuilder();

                        for (int i = 0; i < data.Length; i++)
                        {
                            sbMsg.Append($"{data[i]:X2} ");
                        }
                    }
                }

                return (nSendLength > 0);
            }
            catch (ArgumentNullException)
            {
                AppLog.ABNORMAL("buffers은 null입니다.");                
            }
            catch (ArgumentException)
            {
                AppLog.ABNORMAL("buffers가 비어 있는 경우");                
            }
            catch (SocketException)
            {
                AppLog.ABNORMAL("소켓에 액세스하는 동안 오류가 발생했습니다.아래의 설명 부분을 참조하십시오.");                
            }
            catch (ObjectDisposedException)
            {
                AppLog.ABNORMAL("Socket이 닫혔습니다.");                
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }

            ++nAbnormalCount;                      // 비정상 상태 반복횟수
            return false;
        }

        // 지정 Byte 배열의 데이터를 전송한다. (동기 방식)
        public bool Send(byte[] sendData)
        {
            if (sendData == null || sendData.Length <= 0) return false;                     // 전송 할 내용이 없다면 전송 실패
            if (socketClient == null || !socketClient.Connected) return false;

            try
            {
                // 지정 데이터를 전송 시도한다.
                evtSendDone.Reset();
                nSendLength = 0;
                while (nSendLength < sendData.Length)
                {
                    int sent = socketClient.Send(sendData, nSendLength, sendData.Length - nSendLength, SocketFlags.None);
                    if (sent <= 0) return false;
                    nSendLength += sent;
                }

                nAbnormalCount = 0;                                   // 비정상 상태 반복횟수 clear
                evtSendDone.Set();

                if (IsLogData) // 송수신 메세지를 로그로 기록하라고 되어있다면
                {
                    // Binary data
                    StringBuilder sbMsg = new StringBuilder();

                    for (int i = 0; i < sendData.Length; i++)
                    {
                        sbMsg.Append($"{sendData[i]:X2} ");
                    }
                }

                return (nSendLength > 0);
            }
            catch (ArgumentNullException)
            {
                AppLog.ABNORMAL("buffers은 null입니다.");
            }
            catch (ArgumentException)
            {
                AppLog.ABNORMAL("buffers가 비어 있는 경우");
            }
            catch (SocketException)
            {
                AppLog.ABNORMAL("소켓에 액세스하는 동안 오류가 발생했습니다.아래의 설명 부분을 참조하십시오.");
            }
            catch (ObjectDisposedException)
            {
                AppLog.ABNORMAL("Socket이 닫혔습니다.");
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }

            ++nAbnormalCount;                      // 비정상 상태 반복횟수
            return false;
        }

        private Encoding GetTextEncoding()
        {
            if (IsUnicode || IsStringUnicode)
            {
                return Encoding.Unicode;
            }

            return TextEncoding ?? Encoding.ASCII;
        }
        
    } //of public class CGxSocket


}
