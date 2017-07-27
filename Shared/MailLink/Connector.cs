using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MimeKit;
using MailKit;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using System.Threading.Tasks;

namespace MailLink
{
    public class Connector
    {
        System.Timers.Timer tick;
        System.Timers.Timer tock;

        List<Task> ticktasks = new List<Task>();
        List<Task> tocktasks = new List<Task>();

        const int DEFAULT_SMTP_PORT = 25;
        const int DEFAULT_POP3_PORT = 110;

        private Config config;
        private Mailboxes mailboxes;

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
            mailboxes = Mailboxes.Deserialize();

            // Load the log level from teh config.
            LogLevel = config.Defaults.LogLevel;

            // Inilize polling clock, used for retrieving email from POP and IMAP servers.
            tick = new System.Timers.Timer();
            tick.Interval = config.Settings.TickInterval * 1000;
            //tick.Interval = 500;
            tick.Elapsed += onTickElapsed;

            // Initialize system clock, used for sending ActivityLogs, ErrorLogs, and Notifications to mail administrators.
            tock = new System.Timers.Timer();
            tock.Interval = config.Settings.TockInterval * 1000;
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

            // Update the mailbox file.
            mailboxes.Serialize();

            // TODO: Email Notifications
        }

        // Disable await task warning.
#pragma warning disable CS4014

        private async void HandleNewMail()
        {

            List<Mailbox> active = mailboxes.Where(m => (!m.Busy) && (m.NextPoll == null || m.NextPoll < DateTime.Now)).ToList();

            if (active.Count == 0) return;

            WriteLog(String.Format("{0:u} Processing {1} Mailboxes.", DateTime.Now, active.Count), LogLevel.Informational);

            List<string> aliases = active.GroupBy(m => m.Outbound.Alias).Select(g => g.First()).Select(m => m.Outbound.Alias).ToList();
            foreach (string alias in aliases)
            {
                Server server = config.Servers.Find(s => s.Alias == alias && s.Type == ServerType.SMTP);
                WriteLog(string.Format("Processing SMTP: {0}:  {1}:{2}", server.Alias, server.Domain, server.Port), LogLevel.Verbose);

                // Process each mailbox associated with the current outbound server.
                foreach (Mailbox mailbox in mailboxes.Where(m => m.Outbound.Alias == server.Alias).ToList())
                {
                    while (ticktasks.Count >= config.Settings.MaxMailboxThreads)
                    {
                        await Task.Delay(1000);
                        ticktasks.RemoveAll(t => t.Status == TaskStatus.RanToCompletion);
                    }

                    ticktasks.Add(Task.Run(() => ProcessMailbox(mailbox, server)));
                }
            }
        }

        private void ProcessMailbox(Mailbox mailbox, Server server)
        {
            using (SmtpClient outbound = new SmtpClient(server))
            {
                try
                {
                    outbound.Connect();
                }
                catch (Exception e)
                {
                    WriteLog(String.Format("SMTP: Error connecting to {0} : {1}", mailbox.Outbound.Alias, e.Message), LogLevel.Error);
                    return;
                }

                using (var inbound = new Pop3Client(config.Servers.Find(s => s.Alias == mailbox.Inbound.Alias && s.Type == ServerType.POP3), mailbox))
                {
                    try
                    {
                        try
                        {
                            inbound.Connect();
                        }
                        catch (Exception e)
                        {
                            WriteLog(String.Format("POP3: Error connecting to {0} : {1}", mailbox.Inbound, e.Message), LogLevel.Error);
                            return;
                        }

                        if (String.IsNullOrEmpty(mailbox.LastUID))
                        {
                            WriteLog(string.Format("Skipping New Mailbox: {0} : {1} : {2} Mesages", mailbox.Alias, mailbox.Name, inbound.Count), LogLevel.Warning);

                            // We are going to skip ALL messages, because this is the first run on this mailbox!
                            mailbox.LastUID = inbound.GetMessageUids().Last();
                            return;
                        }

                        // Get a list of all new message UIDs
                        IList<string> uids = inbound.GetNewMessageUids(mailbox.LastUID);
                        WriteLog(string.Format("Processing Mailbox: {0} : {1} : {2} Mesages ({3} new)", mailbox.Alias, mailbox.Name, inbound.Count, uids.Count), LogLevel.Verbose);

                        if(uids.Count() == 0)
                        {
                            return;
                        }

                        // Set LastUID to the last message in the mailbox.
                        mailbox.LastUID = uids.Last();


                        if(inbound.IsConnected)inbound.Disconnect(true);
                        if(inbound != null)inbound.Dispose();
                    }
                    catch (Exception e)
                    {
                        WriteLog(String.Format("Error downloading from {0} : {1}", mailbox.Inbound.Alias, e.Message), LogLevel.Error);
                        return;
                    }
                }
                //message.To.Clear();
                ////message.To.Add(new MailboxAddress(recipient.Name, recipient.ToEmail));
                //message.To.Add(new MailboxAddress("David McCartney", "dmccartney@coreslab.com"));

                //client.MessageSent += onClientMessageSent;

                ////TODO: Add message to relay log.

                //client.Send(message);
                //}
                if (outbound.IsConnected) outbound.Disconnect(true);
                if (outbound != null) outbound.Dispose();
            }
        }

        //                            List<string> queue = new List<string>();




        //                            WriteLog(String.Format("User: {0} has {1} messges on {2} ({3} New).", mailbox.Name, uids.Count(), mailbox.Inbound, (uids.Count - FirstNewID)), LogLevel.Informational);

        //                            for (int i = FirstNewID; i < count; i++)
        //                            {
        //                                int size = client.GetMessageSize(i);
        //                                if (size > 500000)
        //                                {
        //                                    // Queue the larger messages, so we can get the smaller ones faster.
        //                                    WriteLog(String.Format("{0}:Size:{1:N}\n", uids[i], size), LogLevel.Verbose);
        //                                    queue.Add(uids[i]);
        //                                }
        //                                else
        //                                {
        //                                    var message = client.GetMessage(i);
        //                                    WriteLog(String.Format("{0}:{1}:{2}\n", uids[i], message.Attachments.Count(), message.Subject), LogLevel.Verbose);
        //                                    //SendMessage(message, mailbox.Recipients);
        //                                }

        //                            }

        //                            // Now download larger messages.
        //                            foreach (string uid in queue)
        //                            {
        //                                var message = client.GetMessage(uids.IndexOf(uid));
        //                                WriteLog(String.Format("{0}:{1}:{2}\n", uid, message.Attachments.Count(), message.Subject), LogLevel.Verbose);
        //                                //SendMessage(message, mailbox.Recipients);
        //                            }

        //                            client.Disconnect(true);
        //                        }
        //                        catch (Exception e)
        //                        {
        //                            WriteLog(String.Format("Error parsing messages {0} : {1}", mailbox.Inbound, e.Message), LogLevel.Error);

        //                        }
        //                        if (client != null)
        //                        {
        //                            client.Disconnect(true);
        //                            client.Dispose();
        //                        }
        //                    }

        //                    //user.Busy = false;

        //                }).Start();
        //            }
        //        }
        //    }
        //}


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

        public class EventMessage
        {
            public string Text { get; set; }
            public LogLevel Level { get; set; }
        }
    }
}
