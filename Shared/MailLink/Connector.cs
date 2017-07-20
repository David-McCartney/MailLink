using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MailLink
{
    public class Connector
    {
        const int DEFAULT_SMTP_PORT = 25;
        const int DEFAULT_POP3_PORT = 110;

        public bool VerboseLogging { get; set; }

        public string Message { get; protected set; }

        /// <summary>
        /// Occurs when a new client has connected to the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public event EventHandler<EventArgs> LogMessage = delegate { };

        /// <summary>
        /// Specifies the IP Address to bind for the TCP listener.
        /// </summary>
        public IPAddress ServerIP { private get; set; }

        /// <summary>
        /// Specifies the Port to bind for the TCP listener.
        /// </summary>
        public int ServerPort { private get; set; }

        private bool active;
        private static TcpListener tcpListener;

        /// <summary>
        /// TODO: Comment
        /// </summary>
        public Connector()
        {
            active = false;
            ServerIP = IPAddress.Any;
            ServerPort = DEFAULT_SMTP_PORT;

            VerboseLogging = false;

        }

        private void WriteLog(string message)
        {
            Message = message;
            LogMessage(this, new EventArgs());
        }

        public async void Start()
        {
            if (active == true) return;
            else active = true;

            WriteLog("MailLink Connector - Version 1734 - Copyright (C) David McCartney.\r\n\r\n");

            WriteLog("Initializing...");

            WriteLog("\rReady to receive connections. Type 'HELP' for a list of commands.\n");


        }


        public void Pause()
        {
            tcpListener.Stop();
        }

        public void Resume()
        {
            tcpListener.Start();
        }

        public void Stop()
        {
            active = false;
        }
    }
}
