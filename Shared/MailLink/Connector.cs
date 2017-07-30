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
        private Queues queue;
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
        private bool mailbusy;
        private bool queuebusy;

        /// <summary>
        /// TODO: Comment
        /// </summary>
        public Connector()
        {
            active = false;

            Message = new EventMessage();
            config = Config.Deserialize();
            queue = Queues.Deserialize();
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
            HandleQueue();
        }

        private void onTockElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!active) return;

            // Update the configuration,  queue, and mailbox files.
            config.Serialize();
            queue.Serialize();
            mailboxes.Serialize();

            if (LogLevel == LogLevel.Verbose && queue.Count() > 0)
            {
                WriteLog(String.Format("\n{0:u} Message queue contains {1} {2}.",
                    DateTime.Now, queue.Count(), queue.Count() == 1 ? "message" : "messages"),LogLevel.Verbose);
                foreach(var q in queue)
                {
                    if (q.Progress > 0)
                    {
                        WriteLog(String.Format("        {0} : {1} : {2} ({3}%)", q.Alias, q.UID, q.Status, q.Progress), LogLevel.Verbose);
                    }
                    else
                    {
                        WriteLog(String.Format("        {0} : {1} : {2}", q.Alias, q.UID, q.Status), LogLevel.Verbose);
                    }
                }
                WriteLog("\n", LogLevel.Verbose);
            }

            // TODO: Email Notifications
        }

        // Disable await task warning.
#pragma warning disable CS4014

        private async void HandleNewMail()
        {
            List<Mailbox> active = mailboxes.Where(m => (!m.Busy) && (m.NextPoll == null || m.NextPoll < DateTime.Now)).ToList();
            if (active.Count == 0 || mailbusy) return;
            mailbusy = true;

            //WriteLog(String.Format("{0:u} Processing {1} Mailboxes.", DateTime.Now, active.Count), LogLevel.Informational);

            List<string> aliases = active.GroupBy(m => m.Outbound.Alias).Select(g => g.First()).Select(m => m.Outbound.Alias).ToList();
            foreach (string alias in aliases)
            {
                List<Mailbox> batch = active.Where(m => m.Outbound.Alias == alias).ToList();

                WriteLog(string.Format("{0:u} Processing SMTP: {1} ({2} {3})",
                    DateTime.Now, alias, batch.Count(),
                    batch.Count() == 1 ? "mailbox" : "mailboxes"), LogLevel.Verbose);

                // Process each mailbox associated with the current outbound server.
                foreach (Mailbox mailbox in batch)
                {
                    while (ticktasks.Count >= config.Settings.MaxMailboxThreads)
                    {
                        await Task.Delay(1000);
                        ticktasks.RemoveAll(t => t.Status == TaskStatus.RanToCompletion);
                    }

                    ticktasks.Add(Task.Run(() =>
                    {
                        mailbox.Busy = true;

                        ProcessMailbox(mailbox);

                        if (mailbox.PollInterval.HasValue)
                        {
                            mailbox.NextPoll = DateTime.Now.AddSeconds((int)mailbox.PollInterval);
                        }
                        else
                        {
                            mailbox.NextPoll = DateTime.Now.AddSeconds(config.Defaults.PollInterval);
                        }

                        mailbox.Busy = false;
                    }));
                }
            }
            mailbusy = false;
        }

        private void onMessageSent(object sender, MessageSentEventArgs e)
        {
            string result = e.Response.Split(']')[1];
            WriteLog(string.Format("{0} {1}\n", e.Message.To.First(), result), LogLevel.Verbose);

            //TODO: Update message relay log.

        }

        private void ProcessMailbox(Mailbox mailbox)
        {
            Server server = config.Servers.Find(s => s.Alias == mailbox.Outbound.Alias && s.Type == ServerType.SMTP);

            using (SmtpClient outbound = new SmtpClient(server))
            {
                outbound.MessageSent += onMessageSent;

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
                        inbound.Connect();
                    }
                    catch (Exception e)
                    {
                        WriteLog(String.Format("POP3: Error connecting to {0} : {1}", mailbox.Inbound.Alias, e.Message), LogLevel.Error);
                        return;
                    }

                    try
                    {

                        if (String.IsNullOrEmpty(mailbox.LastUID))
                        {
                            WriteLog(string.Format("Skipping New Mailbox: {0} : {1} : {2} Mesages", mailbox.Alias, mailbox.Name, inbound.Count), LogLevel.Warning);

                            if (inbound.Count > 0)
                            {
                                // We are going to skip ALL messages, because this is the first run on this mailbox!
                                mailbox.LastUID = inbound.GetMessageUids().Last();
                                return;
                            }
                            else
                            {
                                // Return zero as last UID, because mailbox is empty.
                                mailbox.LastUID = "0";
                                return;
                            }
                        }

                        // Get a list of all new message UIDs
                        IList<string> uids = inbound.GetNewMessageUids(mailbox.LastUID);
                        WriteLog(string.Format("Processing Mailbox: {0} : {1} : {2} Mesages ({3} new)", mailbox.Alias, mailbox.Name, inbound.Count, uids.Count), LogLevel.Verbose);

                        if (uids.Count() == 0)
                        {
                            return;
                        }

                        // Set LastUID to the last message in the mailbox.
                        mailbox.LastUID = uids.Last();

                        foreach (string uid in uids)
                        {
                            int size = inbound.GetMessageSize(uid);

                            Queue q = new Queue()
                            {
                                Alias = mailbox.Alias,
                                UID = uid,
                                Owner = mailbox,
                                Size = size
                            };

                            if (size > 0)
                            {
                                //TODO: Shorten size.
                                // Queue the larger messages, so we can get the smaller ones faster.
                                WriteLog(String.Format("Queued Message {0}:{1} (Size {2:N})", mailbox.Alias, uid, size), LogLevel.Verbose);
                                queue.Add(q);
                            }
                            else
                            {
                                TransferMessage(inbound, outbound, q);
                            }

                        }


                        if (inbound.IsConnected) inbound.Disconnect(true);
                        if (inbound != null) inbound.Dispose();
                    }
                    catch (Exception e)
                    {
                        WriteLog(String.Format("Error downloading from {0} : {1}", mailbox.Inbound.Alias, e.Message), LogLevel.Error);
                        return;
                    }
                }
                if (outbound.IsConnected) outbound.Disconnect(true);
                if (outbound != null) outbound.Dispose();
            }
        }

        private void TransferMessage(Pop3Client inbound, SmtpClient outbound, Queue q)
        {
            IList<string> alluids = inbound.GetMessageUids();

            q.Status = Status.Downloading;
            int index = alluids.IndexOf(q.UID);
            MimeMessage message = inbound.GetMessage(index);

            //Task<MimeMessage> task = inbound.GetMessageAsync(index);
            //while (!task.IsCompleted)
            //{
            //    await Task.Delay(1000);
            //    //TODO: get progress
            //}

            //MimeMessage message = task.Result;

            message.To.Clear();
            message.Cc.Clear();
            message.Bcc.Clear();

            foreach (Recipient r in q.Owner.Recipients)
            {
                string name;
                if (string.IsNullOrEmpty(r.Name))
                {
                    name = "";
                }
                else
                {
                    name = r.Name;
                }

                switch (r.Type)
                {
                    case ReceipantType.To:
                        message.To.Add(new MailboxAddress(name, r.Email));
                        break;

                    case ReceipantType.Cc:
                        message.Cc.Add(new MailboxAddress(name, r.Email));
                        break;

                    case ReceipantType.Bcc:
                        message.Bcc.Add(new MailboxAddress(name, r.Email));
                        break;
                }


                ////TODO: Add message to relay log.
            }

            try
            {
                outbound.Send(message);
                q.Status = Status.Complete;
            }
            catch (Exception e)
            {
                WriteLog(String.Format("Error uoloading to {0} : {1}", q.Owner.Outbound.Alias, e.Message), LogLevel.Error);
                q.Status = Status.Failed;
            }
        }
        
        private async void HandleQueue()
        {
            List<Queue> active = queue.Where(q => !q.Owner.Busy && q.Status == Status.Waiting)
                                      .OrderBy(q => q.Size).ToList();

            if (active.Count == 0 || queuebusy) return;
            queuebusy = true;

            List<string> aliases = queue.GroupBy(q => q.Alias)
                                        .Select(g => g.First())
                                        .Select(q => q.Alias).ToList();

            foreach (string alias in aliases)
            {
                //List<Queue> batch = active.Where(q => q.Alias == alias)
                //                          .GroupBy(q => q.Owner)
                //                          .Select(g => g.First()).ToList();

                // TODO: combine duplicte messages, so they only download once.

                //while (tocktasks.Count >= config.Settings.MaxQueueThreads)
                while (tocktasks.Count >= 1)
                {
                    await Task.Delay(1000);
                    tocktasks.RemoveAll(t => t.Status == TaskStatus.RanToCompletion);
                }

                Queue q = queue.Where(a => a.Alias == alias).First();

                tocktasks.Add(Task.Run(() =>
                {
                    ProcessQueuedMessages(q.Owner);
                }));
            }
            queuebusy = false;
        }

        private void ProcessQueuedMessages(Mailbox mailbox)
        {
            Server serverout = config.Servers.Find(s => s.Alias == mailbox.Outbound.Alias && s.Type == ServerType.SMTP);

            WriteLog(string.Format("Precessing Queued Messages: {0}:  {1}:{2}",
                serverout.Alias, mailbox.Alias, serverout.Domain, serverout.Port), LogLevel.Verbose);

            using (SmtpClient outbound = new SmtpClient(serverout))
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

                Server serverin = config.Servers.Find(s => s.Alias == mailbox.Inbound.Alias && s.Type == ServerType.POP3);
                using (var inbound = new Pop3Client(serverin, mailbox))
                {
                    try
                    {
                        inbound.Connect();
                    }
                    catch (Exception e)
                    {
                        WriteLog(String.Format("POP3: Error connecting to {0} : {1}", mailbox.Inbound.Alias, e.Message), LogLevel.Error);
                        return;
                    }

                    try
                    {
                        List<Queue> batch = queue.Where(q => q.Owner.Alias == mailbox.Alias).OrderBy(q => q.Size).ToList();
                        foreach (Queue q in batch)
                        {
                            TransferMessage(inbound, outbound, q);

                        }
                    }
                    catch (Exception e)
                    {
                        WriteLog(String.Format("Error downloading from {0} : {1}", mailbox.Inbound.Alias, e.Message), LogLevel.Error);
                        return;
                    }
                    if (inbound.IsConnected) inbound.Disconnect(true);
                    if (inbound != null) inbound.Dispose();
                }
                if (outbound.IsConnected) outbound.Disconnect(true);
                if (outbound != null) outbound.Dispose();
            }
        }


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
