using System;
using System.Collections.Generic;
using System.Text;
using MailKit;
using System.Xml.Serialization;

namespace MailLink 
{
    public class SmtpClient : MailKit.Net.Smtp.SmtpClient
    {
        public Server Server { get; set; }

        public SmtpClient(Server server)
        {
            Server = server;
        }

        public void Connect()
        {
            // Disable server certification callbacks.
            base.ServerCertificateValidationCallback = (s, c, h, e) => true;

            base.Connect(Server.Domain, Server.Port, Server.RequireSSL);

            if (Server.RequireAuth)
            {
                // Disable XOAUTH2 because we won't be using it.
                base.AuthenticationMechanisms.Remove("XOAUTH2");

                // Only needed if the SMTP server requires authentication
                base.Authenticate(Server.UserName, Server.Password); //TODO: Move the outbound credentials from the Reciepient to the mailbox container.
            }

        }
    }
}
