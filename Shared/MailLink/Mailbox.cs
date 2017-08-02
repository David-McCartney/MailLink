using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;

namespace MailLink
{
    public enum RecipientType { To, Cc, Bcc, From }

    [Serializable] [XmlRoot]
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
                Recipients = new List<Recipient> { new Recipient { Name = "MicroBlaster", Address = "dmc@it1.biz" } }
            });
            Add(new Mailbox()
            {
                Alias = "dmc",
                Name = "David McCartney",
                CatchAll = "dmc@it1.biz",
                PollInterval = 120, //Two Minutes
                Inbound = new Credentials { Alias = "IT1", Username = "dmc@it1.biz", Password = "Mz35bvj!Dfu@" },
                Outbound = new Credentials { Alias = "CSSOK" },
                Recipients = new List<Recipient> {
                    new Recipient { Type=RecipientType.To,  Address = "dmccartney@coreslab.com" },
                    new Recipient { Type=RecipientType.Bcc, Address = "dmc@it1.biz" }
                }
            });
            Add(new Mailbox()
            {
                Alias = "dmccartney",
                Name = "David McCartney",
                Inbound = new Credentials { Alias = "Coreslab", Username = "dmccartney", Password = "rPjx3Q!SgwT7" },
                Outbound = new Credentials { Alias = "CSSOK" },
                Recipients = new List<Recipient> { new Recipient { Address = "dmccartney@coreslab.com" } },
                DaysToKeep = 1
            });
            //Add(new Mailbox()
            //{
            //    Alias = "kseat",
            //    Name = "Kamden Seat",
            //    Inbound = new Credentials { Alias = "Coreslab", Username = "kseat", Password = "Se@t74" },
            //    Outbound = new Credentials { Alias = "CSSOK" },
            //    DaysToKeep = 1,
            //});
            //Add(new Mailbox()
            //{
            //    Alias = "ahernandez",
            //    Name = "Anna Hernandez",
            //    Inbound = new Credentials { Alias = "Coreslab", Username = "ahernandez", Password = "An@h2012!" },
            //    Outbound = new Credentials { Alias = "CSSOK" },
            //    DaysToKeep = 1,
            //});
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
                //XmlSerializer xml = new XmlSerializer(typeof(Mailboxes), new Type[] { typeof(MailTo), typeof(MailCc), typeof(MailBcc) });
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
        public bool ShouldSerializeRecipients() { return Recipients.Count > 0; }

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

    public class Recipient : MimeKit.MailboxAddress
    {
        [XmlAttribute]
        public RecipientType Type { get; set; }
        public bool ShouldSerializeType() { return Type > 0; }

        [XmlElement]
        public new string Name { get => base.Name; set => base.Name = value; }
        public new string Address { get => base.Address; set => base.Address = value; }

        public new string IsInternational { get; set; }
        public new Encoding Encoding { get; set; }
        public new MimeKit.DomainList Route { get; set; }


        public Recipient() : base("", "")
        {

        }

        public Recipient(string name, string address) : base(name, address)
        {
            Address = address;
            Name = name;
        }
    }
}

