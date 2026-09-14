using System;
using System.Threading;

namespace MvcVisionSystem._3._Communication.TCP
{
    internal sealed class PythonModelCommunicationLifecycle
    {
        private readonly TcpAsyncConnection connection;
        private readonly object transitionLock = new object();
        private bool isListening;
        private bool isClosing;

        public PythonModelCommunicationLifecycle(TcpAsyncConnection connection)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public bool IsListening
        {
            get
            {
                lock (transitionLock)
                {
                    return isListening;
                }
            }
        }

        public bool IsClosing => Volatile.Read(ref isClosing);

        public bool Start(out bool startedNewListener)
        {
            lock (transitionLock)
            {
                if (isListening)
                {
                    startedNewListener = false;
                    return true;
                }

                startedNewListener = true;
                Volatile.Write(ref isClosing, false);
                isListening = connection.SetListen();
                return isListening;
            }
        }

        public void Stop()
        {
            lock (transitionLock)
            {
                Volatile.Write(ref isClosing, true);
                isListening = false;
                connection.StopListen();
            }
        }
    }
}
