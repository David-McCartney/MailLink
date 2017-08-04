using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using MailKit;
using System.Xml.Serialization;
using MimeKit;
using System.Threading;
using System.Threading.Tasks;

namespace MailLink
{
    public class Pop3Client : MailKit.Net.Pop3.Pop3Client
    {
        public Server Server { get; set; }
        public Mailbox Mailbox { get; set; }
        public string Uid { get; set; }
        public long BytesTransfered { get; set; }

        /// <summary>
        /// Raised for each reported BytesTransfered changed.
        /// </summary>
        public event EventHandler<EventArgs> ProgressChanged = delegate { };

        public Pop3Client(Server server, Mailbox mailbox)
        {
            Server = server;
            Mailbox = mailbox;
        }

        public void Connect()
        {
            // Disable server certification callbacks.
            base.ServerCertificateValidationCallback = (s, c, h, e) => true;


            //TODO: MX server lookup
            //TODO: use default port
            base.Connect(Server.Domain, Server.Port, Server.RequireSSL);

            if (Server.RequireAuth)
            {
                // Disable XOAUTH2 because we won't be using it.
                base.AuthenticationMechanisms.Remove("XOAUTH2");

                string username, password;

                if (string.IsNullOrEmpty(Mailbox.Inbound.Username))
                {
                    username = Server.UserName;
                }
                else
                {
                    username = Mailbox.Inbound.Username;
                }

                if (string.IsNullOrEmpty(Mailbox.Inbound.Username))
                {
                    password = Server.Password;
                }
                else
                {
                    password = Mailbox.Inbound.Password;
                }

                // Only needed if the SMTP server requires authentication
                base.Authenticate(username, password); //TODO: Move the outbound credentials from the Reciepient to the mailbox container.
            }
        }

        // Overiding base.Count because it is too slow on mailboxes with a lot of messsages.
        public new int Count
        {
            get
            {
                return base.GetMessageUids().Count;
            }
        }

        /// <summary>
        /// Get the size of the specified message, in bytes.
        /// </summary>
        /// <param name="uid">Specify UID of desired message</param>
        /// <returns></returns>
        public int GetMessageSize(string uid)
        {
            IList<string> alluids = base.GetMessageUids();
            return base.GetMessageSize(alluids.IndexOf(uid));
        }

        public List<string> GetNewMessageUids(string lastuid)
        {
            List<string> newuids = new List<string>();
            IList<string> uids = base.GetMessageUids();
            Int64 last = 0;

            // Set the index to the first new UID, by searching for the last UID + 1.
            int index = uids.IndexOf(lastuid) + 1;

            // If the last UID was not found, find the next larger ID.
            if (index == 0)
            {
                index = uids.Count();

                try
                {
                    last = Convert.ToInt64(lastuid, 16);
                }
                catch { }

                for (int i = 0; i < uids.Count(); i++)
                {
                    try
                    {
                        if (Convert.ToInt64(uids[i], 16) > last)
                        {
                            index = i;
                            break;
                        }
                    }
                    catch { }
                }
            }

            try
            {
                newuids.AddRange(uids.Skip(index));
                return newuids;
            }
            catch
            {
                return null;
            }
        }

        public MimeMessage GetMessage(string uid)
        {
            CancellationToken token = new CancellationToken();
            ProgressReport progress = new ProgressReport();

            Uid = uid;

            progress.ProgressChanged += (obj, e) =>
            {
                // Send the event up to the parrent.
                //ProgressChanged(((ProgressReport)o).BytesTransfered, e);
                //TODO: use custom eventargs to pass UID and BytesTransfered
                BytesTransfered = ((ProgressReport)obj).BytesTransfered;
                ProgressChanged(this, e);
            };

            int index = base.GetMessageUids().IndexOf(uid);
            MimeMessage message = base.GetMessage(index, token, progress);

            return message;
        }
    }

    public class ProgressReport : ITransferProgress
    {
        public long BytesTransfered { get; set; }

        /// <summary>
        /// Raised for each reported BytesTransfered changed.
        /// </summary>
        public event EventHandler<EventArgs> ProgressChanged = delegate { };

        public ProgressReport()
        {

        }

        public void Report(long bytes)
        {
            BytesTransfered = bytes;
            ProgressChanged(this, new EventArgs());
        }

        public void Report(long bytes, long total)
        {
            BytesTransfered = bytes;
            ProgressChanged(this, new EventArgs());
        }
    }
}
