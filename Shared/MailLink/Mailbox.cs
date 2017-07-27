using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace MailLink
{
    public enum Mail { To, Cc, Bcc, From }

    [Serializable] [XmlRoot(ElementName = "Mailboxes")]
    public class Mailboxes : List<Mailbox>
    {
        public Mailboxes()
        {
        }

        /// <summary>
        /// Creates a new config object populated with sample data.
        /// </summary>
        private void Create()
        {
            // Create sample mailboxes
            Add(new Mailbox()
            {
                Alias = "micro",
                Name = "MicroBlaster",
                PollInterval = 15,
                Inbound = new Credentials { Alias = "TWFM", Username = "dmc@twfm.net", Password = "m+5?VYJNtk4@" },
                Outbound = new Credentials { Alias = "TWFM", Username = "dmc@twfm.net", Password = "m+5?VYJNtk4@" },
                Recipients = new List<Recipient>
                {
                    new Recipient {Email = "dmc@it1.biz"},
                }
            });
            Add(new Mailbox()
            {
                Alias = "dmc",
                Name = "David McCartney",
                CatchAll = "dmc@it1.biz",
                Inbound = new Credentials { Alias = "IT1", Username = "dmc@it1.biz", Password = "Mz35bvj!Dfu@" },
                Outbound = new Credentials { Alias = "CSSOK" },
                Recipients = new List<Recipient>
                {
                    new Recipient {Email = "dmc@it1.biz"},
                    new Recipient{Type = Mail.Cc, Email = "dmccartney@coreslab.com"}
                }
            });
        }

        public void Serialize()
        {
            if (!Directory.Exists(@"c:\ProgramData\MailLink"))
            {
                Directory.CreateDirectory(@"c:\ProgramData\MailLink");
            }

            string mailboxFile = @"c:\ProgramData\MailLink\mailbox.xml";
            FileStream fs = new FileStream(mailboxFile, FileMode.OpenOrCreate);


            XmlSerializerNamespaces xmlns = new XmlSerializerNamespaces(); xmlns.Add("", "");
            //xmlns.Add("ML", "https://github.com/David-McCartney/MailLink");
            XmlSerializer xml = new XmlSerializer(typeof(Mailboxes));

            xml.Serialize(fs, this, xmlns);
            //xml.Serialize(fs, configuration);

            fs.Close();
        }

        public static Mailboxes Deserialize()
        {
            string mailboxFile = @"c:\ProgramData\MailLink\mailbox.xml";

            if (!File.Exists(mailboxFile))
            {
                Mailboxes m = new Mailboxes();
                m.Create();
                m.Serialize();
                return m;
            }
            else
            {
                FileStream fs = new FileStream(mailboxFile, FileMode.Open);

                XmlSerializer xml = new XmlSerializer(typeof(Mailboxes));

                Mailboxes m = (Mailboxes)xml.Deserialize(fs);
                fs.Close();

                return m;
            }
        }
    }

    public class Mailbox
    {
        [XmlAttribute]
        public string Alias { get; set; }

        [XmlElement]
        public string Name { get; set; }
        public string Description { get; set; }
        public string CatchAll { get; set; }
        public string LastUID { get; set; }
        public int PollInterval { get; set; }
        public Credentials Inbound { get; set; }
        public Credentials Outbound { get; set; }

        [XmlArrayItem(ElementName = "Recipient", Type = typeof(Recipient))]
        public List<Recipient> Recipients { get; set; }

        [XmlIgnore]
        public bool Busy { get; set; }

        [XmlIgnore]
        public DateTime NextPoll { get; set; }

        public Mailbox()
        {
            Recipients = new List<Recipient>();
        }
    }

    public class Credentials
    {
        [XmlAttribute]
        public ServerType Type { get; set; }

        [XmlElement]
        public string Alias { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        //TODO: Encript passwords

        public Credentials()
        {
        }
    }

    public class Recipient
    {
        [XmlAttribute]
        public MailLink.Mail Type { get; set; }

        [XmlElement]
        public string Email { get; set; }

        public Recipient()
        {
        }
    }
}

