using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace MailLink
{
    public enum ReceipantType { To, Cc, Bcc, From }

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
                PollInterval = 300, // Five Minutes
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
                PollInterval = 120, //Two Minutes
                Inbound = new Credentials { Alias = "IT1", Username = "dmc@it1.biz", Password = "Mz35bvj!Dfu@" },
                Outbound = new Credentials { Alias = "CSSOK" },
                Recipients = new List<Recipient>
                {
                    new Recipient {Email = "dmc@it1.biz"},
                    new Recipient{Type = ReceipantType.Cc, Email = "dmccartney@coreslab.com"}
                }
            });
            Add(new Mailbox()
            {
                Alias = "dmccartney",
                Name = "David McCartney",
                Inbound = new Credentials { Alias = "Coreslab", Username = "dmccartney", Password = "rPjx3Q!SgwT7" },
                Outbound = new Credentials { Alias = "CSSOK" },
                DaysToKeep = 1,
                Recipients = new List<Recipient>
                {
                    new Recipient{Email = "dmccartney@coreslab.com"}
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
            using (FileStream fs = new FileStream(mailboxFile, FileMode.OpenOrCreate))
            {
                XmlSerializerNamespaces xmlns = new XmlSerializerNamespaces(); xmlns.Add("", "");
                XmlSerializer xml = new XmlSerializer(typeof(Mailboxes));

                xml.Serialize(fs, this, xmlns);

                fs.Close();
            }
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
                using (FileStream fs = new FileStream(mailboxFile, FileMode.Open))
                {

                    XmlSerializer xml = new XmlSerializer(typeof(Mailboxes));

                    Mailboxes m = (Mailboxes)xml.Deserialize(fs);
                    fs.Close();

                    return m;
                }
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
        public Credentials Inbound { get; set; }
        public Credentials Outbound { get; set; }

        [XmlElement]
        public int? PollInterval { get; set; }
        public bool ShouldSerializePollInterval() { return PollInterval.HasValue; }

        [XmlElement]
        public int? DaysToKeep { get; set; }
        public bool ShouldSerializeDaysToKeep() { return DaysToKeep.HasValue; }

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
        public string Alias { get; set; }

        [XmlElement]
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
        public ReceipantType Type { get; set; }
        public bool ShouldSerializeType() { return Type != 0; }

        [XmlElement]
        public string Name { get; set; }
        public bool ShouldSerializeName() { return String.IsNullOrEmpty(Name);}

        [XmlText]
        public string Email { get; set; }

        public Recipient()
        {
        }
    }
}

