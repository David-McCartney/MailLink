﻿using System;
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

            //int count = uids.Count;
            int first = uids.IndexOf(lastuid) + 1;

            if (first == 0)
            {
                try
                {
                    last = Convert.ToInt64(lastuid, 16);
                }
                catch { }

                // If last ID was not found, find the next larger ID.
                for (int i = 0; i < uids.Count(); i++)
                {
                    try
                    {
                        if (Convert.ToInt64(uids[i], 16) > last)
                        {
                            first = i;
                            break;
                        }
                    }
                    catch { }
                }

                if (first == 0)
                {
                    // No new messages were found. Return empty list.
                    return newuids;
                }
            }

            newuids.AddRange(uids.Skip(first));

            //for (int i = first; i < count; i++)
            //{
            //    newuids.Add(uids[i]);
            //}
            return newuids;
        }

        public Task<MimeMessage> GetMessageAsync(int index)
        {
            CancellationToken token = new CancellationToken();
            TransferProgress progress = null;

            //Task<MimeMessage> task = inbound.GetMessageAsync(index, cancel, progress);
            Task<MimeMessage> task = base.GetMessageAsync(index, token, progress);

            return task;
        }

    }


    public class TransferProgress : ITransferProgress
    {
        public long BytesTransferred { get; set; }

        public void Report(long bytesTransferred, long totalSize)
        {
            BytesTransferred = bytesTransferred;
        }

        public void Report(long bytesTransferred)
        {
            BytesTransferred = bytesTransferred;
        }
    }
}
