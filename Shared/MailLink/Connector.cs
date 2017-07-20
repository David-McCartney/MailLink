using MailKit.Net.Pop3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MailLink
{
    public class Connector
    {
        System.Timers.Timer tick;
        System.Timers.Timer tock;

        const int DEFAULT_SMTP_PORT = 25;
        const int DEFAULT_POP3_PORT = 110;

        private Config config;

        public bool VerboseLogging { get; set; }

        public string Message { get; protected set; }

        /// <summary>
        /// Occurs when a message needs to be written to the event log.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public event EventHandler<EventArgs> LogMessage = delegate { };

        private bool active;

        /// <summary>
        /// TODO: Comment
        /// </summary>
        public Connector()
        {
            active = false;

            VerboseLogging = true;

            config = new Config();

            // Inilize polling clock, used for retrieving email from POP and IMAP servers.
            tick = new System.Timers.Timer();
            //tick.Interval = config.Root.PollInterval * 1000;
            tick.Interval = 5000;
            tick.Elapsed += onTickElapsed;

            // Initialize system clock, used for sending ActivityLogs, ErrorLogs, and Notifications to mail administrators.
            tock = new System.Timers.Timer();
            tock.Interval = config.Root.SystemInterval * 1000;
            //tock.AutoReset = true;
            tock.Elapsed += onTockElapsed;
        }

        private void onTickElapsed(object sender, System.Timers.ElapsedEventArgs ea)
        {
            if (!active) return;

            foreach(User user in config.Root.Users)
            {
                if(!user.Busy)
                {
                    user.Busy = true;

                    new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;

                        using (var client = new Pop3Client())
                        {
                            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                            Server server = config.Root.Servers.Where(s => s.Name == user.Inbound.Server).First();


                            try
                            {
                                client.Connect(server.Domain, server.Port, false);

                                client.AuthenticationMechanisms.Remove("XOAUTH2");
                                client.Authenticate(user.Inbound.Password, user.Inbound.Password);

                                //for (int i = 0; i < client.Count; i++)
                                //{
                                //    var message = client.GetMessage(i);
                                //    Console.WriteLine("Subject: {0}", message.Subject);
                                //}
                                Console.WriteLine("User: {0} has {1} messges.\r\n", user.Name, client.Count());

                                client.Disconnect(true);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Error connectiong to {0} : {1}\r\n", user.Inbound.Server, e.Message);

                                if (client != null) client.Dispose();
                            }
                        }

                        user.Busy = false;

                    }).Start();
                }
            }
        }

        private void onTockElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!active) return;

            // TODO: Email Notifications
        }

        private void WriteLog(string message)
        {
            Message = message;
            LogMessage(this, new EventArgs());
        }

        public void Start()
        {
            if (active == true) return;
            else active = true;

            WriteLog("MailLink Connector - Version 1734 - Copyright (C) David McCartney.\r\n\r\n");

            WriteLog("Initializing...");

            


            WriteLog("\rInitialization Complete. Type 'HELP' for a list of commands.\n\n");

            tick.Start();
            tock.Start();

        }


        public void Pause()
        {
            active = false;

            tick.Stop();
            tock.Stop();

        }

        public void Resume()
        {
            active = true;

            tick.Start();
            tock.Start();
        }

        public void Stop()
        {
            active = false;

            tick.Stop();
            tock.Stop();

            //TODO: Disconnect and dispose all active connections.
        }
    }
}
