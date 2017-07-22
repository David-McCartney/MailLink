using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MimeKit;
using MailKit;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;

namespace MailLink
{
    public class Connector
    {
        System.Timers.Timer tick;
        System.Timers.Timer tock;

        const int DEFAULT_SMTP_PORT = 25;
        const int DEFAULT_POP3_PORT = 110;

        private Config config;

        public LogLevel LogLevel { get; set; }

        public EventMessage Message { get; protected set; }

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

            Message = new EventMessage();
            config = Config.Deserialize();

            // Load the log level from teh config.
            LogLevel = config.Default.LogLevel;

            // Inilize polling clock, used for retrieving email from POP and IMAP servers.
            tick = new System.Timers.Timer();
            tick.Interval = config.TickInterval * 1000;
            //tick.Interval = 500;
            tick.Elapsed += onTickElapsed;

            // Initialize system clock, used for sending ActivityLogs, ErrorLogs, and Notifications to mail administrators.
            tock = new System.Timers.Timer();
            tock.Interval = config.TockInterval * 1000;
            tock.Elapsed += onTockElapsed;
        }

        private void onTickElapsed(object sender, System.Timers.ElapsedEventArgs ea)
        {
            if (!active) return;

            HandleNewMail();
        }

        private void onTockElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!active) return;

            // Update the configuration file.
            config.Serialize();

            // TODO: Email Notifications
        }

        private void HandleNewMail()
        {
            List<Mailbox> mailboxes = config.Mailboxes.Where(m => (!m.Busy) && (m.NextPoll == null || m.NextPoll < DateTime.Now)).ToList();

            if (mailboxes.Count == 0) return;

            WriteLog(String.Format("{0:u} Processing {1} Mailboxes.", DateTime.Now, config.Mailboxes.Where(u => !u.Busy).Count()), LogLevel.Informational);

            foreach(string servername in mailboxes.Select(m => m.Outbound).ToList().Distinct())
            {
                using (SmtpClient outbound = new SmtpClient())
                {
                    Server server = config.Servers.Where(s => s.Name == servername).First();

                    WriteLog(String.Format("Processing SMTP: {0}:  {1}:{2}", server.Name, server.Domain, server.Port), LogLevel.Verbose);

                    outbound.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    outbound.Connect(server.Domain, server.Port, server.RequireSSL);

                    if (server.RequireAuth)
                    {
                        // Only needed if the SMTP server requires authentication
                        outbound.AuthenticationMechanisms.Remove("XOAUTH2");
                        outbound.Authenticate("", ""); //TODO: Move the outbound credentials from the Reciepient to the mailbox container.
                    }

                    // Process each mailbox associated with the current outbound server.
                    foreach(Mailbox mailbox in mailboxes.Where(m => m.Outbound == servername).ToList())
                    {
                        using (var inbound = new Pop3Client())
                        {
                            ServerConnect(inbound, mailbox);

                            IList<string> uids = GetMessageUIDS(inbound, mailbox);

                        WriteLog(String.Format("Processing Mailbox: {0} : {1}", mailbox.Alias, mailbox.Name), LogLevel.Verbose);

                    }
                    //message.To.Clear();
                    ////message.To.Add(new MailboxAddress(recipient.Name, recipient.ToEmail));
                    //message.To.Add(new MailboxAddress("David McCartney", "dmccartney@coreslab.com"));

                    //client.MessageSent += onClientMessageSent;

                    ////TODO: Add message to relay log.

                    //client.Send(message);
                    //}


                    outbound.Disconnect(true);
                }

            }

        }

        void none()
        { 
            foreach (Mailbox mailbox in config.Mailboxes)
            {
                if(!mailbox.Busy)
                {
                    mailbox.Busy = true;

                    new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;

                        using (var client = new Pop3Client())
                        {
                            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                            Server server = config.Servers.Where(s => s.Name == mailbox.Inbound).First();


                            try
                            {
                                client.Connect(server.Domain, server.Port, server.RequireSSL);

                                if (server.RequireAuth)
                                {
                                    client.AuthenticationMechanisms.Remove("XOAUTH2");
                                    client.Authenticate(mailbox.Username, mailbox.Password);
                                }
                            }
                            catch (Exception e)
                            {
                                WriteLog(String.Format("Error connectiong to {0} : {1}", mailbox.Inbound, e.Message), LogLevel.Error);

                                if (client != null) client.Dispose();
                                return;
                            }

                            try
                            {
                                IList<string> uids = client.GetMessageUids();
                                List<string> queue = new List<string>();

                                int FirstNewID;
                                int count = uids.Count();
                                if (String.IsNullOrEmpty(mailbox.LastUID))
                                {
                                    // We are going to skip ALL messages, because this is the first run on this mailbox!
                                    FirstNewID = count;
                                }
                                else
                                {
                                    // Set FirstNewID to the message following the last message we read before.
                                    FirstNewID = uids.IndexOf(mailbox.LastUID) + 1;
                                }
                                // Set LastUID to the last message in the mailbox.
                                mailbox.LastUID = uids[count - 1];


                                WriteLog(String.Format("User: {0} has {1} messges on {2} ({3} New).", mailbox.Name, uids.Count(), mailbox.Inbound, (uids.Count - FirstNewID)), LogLevel.Informational);

                                for (int i = FirstNewID; i < count; i++)
                                {
                                    int size = client.GetMessageSize(i);
                                    if (size > 500000)
                                    {
                                        // Queue the larger messages, so we can get the smaller ones faster.
                                        WriteLog(String.Format("{0}:Size:{1:N}\n", uids[i], size), LogLevel.Verbose);
                                        queue.Add(uids[i]);
                                    }
                                    else
                                    {
                                        var message = client.GetMessage(i);
                                        WriteLog(String.Format("{0}:{1}:{2}\n", uids[i], message.Attachments.Count(), message.Subject), LogLevel.Verbose);
                                        //SendMessage(message, mailbox.Recipients);
                                    }

                                }

                                // Now download larger messages.
                                foreach(string uid in queue)
                                {
                                    var message = client.GetMessage(uids.IndexOf(uid));
                                    WriteLog(String.Format("{0}:{1}:{2}\n", uid, message.Attachments.Count(), message.Subject), LogLevel.Verbose);
                                    //SendMessage(message, mailbox.Recipients);
                                }

                                client.Disconnect(true);
                            }
                            catch (Exception e)
                            {
                                WriteLog(String.Format("Error parsing messages {0} : {1}", mailbox.Inbound, e.Message), LogLevel.Error);

                            }
                            if (client != null)
                            {
                                client.Disconnect(true);
                                client.Dispose();
                            }
                        }

                        //user.Busy = false;

                    }).Start();
                }
            }
        }


        private void onClientMessageSent(object sender, MessageSentEventArgs e)
        {
            string result = e.Response.Split(']')[1];
            WriteLog(string.Format("{0} {1}\n", e.Message.To.First(), result), LogLevel.Verbose);

            //TODO: Update message relay log.

        }


        private void WriteLog(string message, LogLevel level)
        {
            Message.Text = message;
            Message.Level = level;
            LogMessage(this, new EventArgs());
        }

        public void Start()
        {
            if (active == true) return;
            else active = true;

            HandleNewMail();

            tick.Start();
            tock.Start();
        }


        public void Pause()
        {
            active = false;

            tick.Stop();
            tock.Stop();

            config.Serialize();
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

            config.Serialize();
            //TODO: Disconnect and dispose all active connections.
        }
    }

        private IList<string> GetMessageUIDS(Pop3Client client, Mailbox mailbox)
        {
        }

        private void Pop3Connect(Pop3Client client, Server server)
        {
            try
            {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                client.Connect(server.Domain, server.Port, server.RequireSSL);

                if (server.RequireAuth)
                {
                    client.AuthenticationMechanisms.Remove("XOAUTH2");
                    //client.Authenticate(mailbox.Username, mailbox.Password);
                }
            }
            catch (Exception e)
            {
                WriteLog(String.Format("Error connectiong to {0} : {1}", server.Name, e.Message), LogLevel.Error, LogLevel.Error);

                if (client != null) client.Dispose();
            }
        }

        public class EventMessage
    {
        public string Text { get; set; }
        public LogLevel Level { get; set; }
    }

}
