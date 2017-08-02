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

            // Cleanup the queue
            queue.RemoveAll(q => q.Status == Status.Complete);

            // Update the configuration,  queue, and mailbox files.
            config.Serialize();
            queue.Serialize();
            mailboxes.Serialize();

            if (LogLevel == LogLevel.Verbose && queue.Count() > 0)
            {
                WriteLog(String.Format("\n Message queue contains {0} {1}.",
                    queue.Count(), queue.Count() == 1 ? "message" : "messages"),LogLevel.Informational);
                foreach(var q in queue)
                {
                    if (q.Progress > 0)
                    {
                        WriteLog(String.Format("        {0} : {1} : {2} ({3}%)", q.Owner, q.UID, q.Status, q.Progress), LogLevel.Verbose);
                    }
                    else
                    {
                        WriteLog(String.Format("        {0} : {1} : {2}", q.Owner, q.UID, q.Status), LogLevel.Verbose);
                    }
                }
                WriteLog("\n", LogLevel.Verbose);
            }

            // TODO: Email Notifications
        }

        private void onMessageSent(object sender, MessageSentEventArgs e)
        {
            //SmtpClient outbound = (SmtpClient)sender;

            string result = e.Response.Split(']')[1];
            WriteLog(string.Format("Message Result {0} {1}", e.Message.To.First(), result), LogLevel.Informational);

            try
            {
                Queue q = queue.Where(queue => queue.MessageID == e.Message.MessageId).First();
                if(result.Contains("Queued mail for delivery"))
                {
                    q.Status = Status.Complete;
                }
                else
                {
                    q.Status = Status.Failed;
                }
            }
            catch { }

            //TODO: Update message relay log.

        }

// Disable await task warning.
//#pragma warning disable CS4014

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

                WriteLog(string.Format("Processing SMTP: {0} ({1} {2})",
                    alias, batch.Count(),
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

        private void ProcessMailbox(Mailbox mailbox)
        {
            Server server = config.Servers.Find(s => s.Alias == mailbox.Outbound.Alias && s.Type == ServerType.SMTP);

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
                        if (uids.Count > 0)
                        {
                            WriteLog(string.Format("Processing Mailbox: {0} : {1} : {2} Mesages ({3} new)", mailbox.Alias, mailbox.Name, inbound.Count, uids.Count), LogLevel.Informational);
                        }
                        else
                        {
                            WriteLog(string.Format("Processing Mailbox: {0} : {1} : {2} Mesages ({3} new)", mailbox.Alias, mailbox.Name, inbound.Count, uids.Count), LogLevel.Verbose);
                        }
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
                                UID = uid,
                                Owner = mailbox.Alias,
                                Size = size
                            };

                            if (size > 50000)
                            {
                                //TODO: Shorten size.
                                // Queue the larger messages, so we can get the smaller ones faster.
                                WriteLog(String.Format("Queued Message {0}:{1} (Size {2:N})", mailbox.Alias, uid, size), LogLevel.Verbose);
                                queue.Add(q);
                            }
                            else
                            {
                                WriteLog(String.Format("Downloading Message {0}:{1} (Size {2:N})", mailbox.Alias, uid, size), LogLevel.Verbose);
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

        private async void HandleQueue()
        {
            var active = queue.Join(mailboxes, queue => queue.Owner, mailboxes => mailboxes.Alias,
                                    (q, m) => new { queue = q, mailboxes = m })
                                    .Where(q => q.queue.Status == Status.Waiting && !q.mailboxes.Busy)
                                    .OrderBy(q => q.queue.Size).ToList();

            //List<Queue> active = queue.Where(q => !q.Owner.Busy && q.Status == Status.Waiting)
            //                          .OrderBy(q => q.Size).ToList();

            if (active.Count == 0 || queuebusy) return;
            queuebusy = true;

            List<string> aliases = active.GroupBy(q => q.queue.Owner)
                                         .Select(g => g.First())
                                         .Select(q => q.queue.Owner).ToList();

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

                Queue q = queue.Where(a => a.Owner == alias).First();
                Mailbox owner = mailboxes.Where(m => m.Alias == q.Owner).First();

                tocktasks.Add(Task.Run(() =>
                {
                    ProcessQueuedMessages(owner);
                }));
            }
            queuebusy = false;
        }

        private void ProcessQueuedMessages(Mailbox mailbox)
        {
            Server serverout = config.Servers.Find(s => s.Alias == mailbox.Outbound.Alias && s.Type == ServerType.SMTP);
            mailbox.Busy = true;

            WriteLog(string.Format("Precessing Queued Messages: {0}:  {1}:{2}",
                serverout.Alias, mailbox.Alias, serverout.Domain, serverout.Port), LogLevel.Verbose);

            using (SmtpClient outbound = new SmtpClient(serverout))
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
                        List<Queue> batch = queue.Where(q => q.Owner == mailbox.Alias).OrderBy(q => q.Size).ToList();
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
            mailbox.Busy = false;
        }

        private void TransferMessage(Pop3Client inbound, SmtpClient outbound, Queue q)
        {
            IList<string> alluids = inbound.GetMessageUids();

            q.Status = Status.Downloading;
            //int index = alluids.IndexOf(q.UID);
            MimeMessage message = inbound.GetMessage(q.UID);

            Mailbox mailbox = mailboxes.Where(m => m.Alias == q.Owner).First();


            //Task<MimeMessage> task = inbound.GetMessage(index);
            //while (!task.IsCompleted)
            //{
            //    await Task.Delay(1000);
            //    //TODO: get progress
            //}

            //MimeMessage message = task.Result;

            //message.To.Clear();
            //message.Cc.Clear();
            //message.Bcc.Clear();
            //System.Net.Mail.SmtpClient test = new System.Net.Mail.SmtpClient();
            //System.Net.Mail.MailMessage mail;

            foreach (Recipient recipient in mailbox.Recipients.Where(r => r.Type == RecipientType.To))
            {
                if(!ContainsRecipient(message, recipient))
                {
                    message.To.Add(recipient);
                }
            }

            foreach (Recipient recipient in mailbox.Recipients.Where(r => r.Type == RecipientType.Cc))
            {
                if (!ContainsRecipient(message, recipient))
                {
                    message.Cc.Add(recipient);
                }
            }

            foreach (Recipient recipient in mailbox.Recipients)
            {
                // This only matches if name and email address match, so I had to write one that only matches email address.
                //if (message.To.Contains(recipient))
                //{
                //    WriteLog(String.Format("Match Found :{0}:{1}",recipient.Name,recipient.Address), LogLevel.Verbose);
                //}

                try
                {
                    q.Status = Status.Uploading;
                    q.MessageID = message.MessageId;
                    outbound.Send(message, (MailboxAddress)message.From.First(), mailbox.Recipients);
                }
                catch (Exception e)
                {
                    WriteLog(String.Format("Error uoloading to {0} : {1}", mailbox.Outbound.Alias, e.Message), LogLevel.Error);
                    q.Status = Status.Failed;
                }
            }
        }

        private bool ContainsRecipient(MimeMessage message, Recipient recipient)
        {
            InternetAddressList mailto = null;

            switch (recipient.Type)
            {
                case RecipientType.To:
                    mailto = message.To;
                    break;

                case RecipientType.Cc:
                    mailto = message.Cc;
                    break;

                case RecipientType.Bcc:
                    mailto = message.Bcc;
                    break;
            }

            foreach (MailboxAddress r in mailto)
            {
                if (r.Address == recipient.Address)
                {
                    return true;
                }
            }
            return false;
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

            //            MailTest();
            HandleNewMail();

            tick.Start();
            tock.Start();
        }

        private void MailTest()
        {

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
