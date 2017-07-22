using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace MailLink
{
    public enum LogLevel { Error, Warning, Informational, Verbose };

    [Serializable]
    [XmlRoot]
    public class Config
    {
        [XmlElement]
        public int TickInterval { get; set; }

        [XmlElement]
        public int TockInterval { get; set; }

        [XmlElement]
        public int MaxMailboxThreads { get; set; }

        [XmlElement]
        public int MaxAttachmentThreads { get; set; }

        [XmlElement]
        public Defaults Default { get; set; }

        [XmlArrayItem(ElementName = "Setting", Type = typeof(Setting))]
        public List<Setting> Settings { get; set; }

        [XmlArrayItem(ElementName = "Server", Type = typeof(Server))]
        public List<Server> Servers { get; set; }

        [XmlArrayItem(ElementName = "Mailbox", Type = typeof(Mailbox))]
        public List<Mailbox> Mailboxes { get; set; }

        //public Config Config { get; protected set; }
        static string configFile = @"c:\ProgramData\MailLink\config.xml";

        public Config()
        {
            Settings = new List<Setting>();
            Servers = new List<Server>();
            Mailboxes = new List<Mailbox>();
            Default = new Defaults();

            //Initialize();
        }

        /// <summary>
        /// Creates a new config object populated with sample data.
        /// </summary>
        private void Create()
        {
            TickInterval = 5 * 60; // Five Minutes
            TockInterval = 15 * 60; // Fifteen Minutes
            MaxMailboxThreads = 5;
            MaxAttachmentThreads = 2;

            // Initialize Defaults.
            Default.LogLevel = LogLevel.Verbose;
            Default.Pop3Port = 110;
            Default.Pop3EncriptedPort = 995;
            Default.ImapPort = 143;
            Default.ImapEncriptedPort = 993;
            Default.SmtpPort = 25;
            Default.SmtpEncriptedPort = 587;
            Default.KeepMessages = true;
            Default.DaysToKeep = 14;

            Settings.Add(new Setting("Testing", "Test"));

            Servers.Add(new Server("SMTP", "CSSOK", "cssokc-ms1.coreslab.local", 25, false, false));
            Servers.Add(new Server("POP", "Coreslab", "mail.coreslab.com", 110, false, true));
            Servers.Add(new Server("POP", "IT1", "outlook.office365.com", 995, true, true));

            Mailboxes.Add(new Mailbox
            {
                Alias = "DMC",
                Name = "David McCartney",
                Description = "Catchall Mailbox for it1.biz",
                Inbound = "IT1",
                Outbound = "CSSOK",
                Username = "dmc@it1.biz",
                Password = "Maui2645!",
                CatchAll = "dmccartney@coreslab.com",
                DefaultDomain = "it1.biz",
                PollInterval = 900,
                Recipients = { new Recipient { Alias = "dmc", Name = "David McCartney", 
                                               MailFrom = "dmc@it1.biz", MailTo = "dmccartney@coreslab.com" }
                               //new Recipient { Alias = "dmc", Name = "David McCartney", Server = "CSSOK",
                               //                FromEmail = "dmc@it1.biz", ToEmail = "dmc@it1.biz" }
                }
            });

            Mailboxes.Add(new Mailbox
            {
                Alias = "dmccartney",
                Name = "David McCartney",
                Description = "Catchall Mailbox for it1.biz",
                Inbound = "Coreslab",
                Outbound = "CSSOK",
                Username = "dmccartney@coreslab.com",
                Password = "Maui2645!",
                CatchAll = "dmccartney@coreslab.com",
                PollInterval = 900,
                Recipients = { new Recipient { Alias = "dmccartney", Name = "David McCartney", MailFrom = "dmccartney@coreslab.com", MailTo = "dmccartney@coreslab.com" }
                }
            });
        }

        public void Serialize()
        {
            FileStream fs = new FileStream(configFile, FileMode.Create);

            XmlSerializerNamespaces xmlns = new XmlSerializerNamespaces(); xmlns.Add("", "");
            //xmlns.Add("ML", "https://github.com/David-McCartney/MailLink");
            XmlSerializer xml = new XmlSerializer(typeof(Config));

            xml.Serialize(fs, this, xmlns);
            //xml.Serialize(fs, configuration);

            fs.Close();
        }

        public static Config Deserialize()
        {
            //Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!Directory.Exists(@"c:\ProgramData\MailLink"))
            {
                Directory.CreateDirectory(@"c:\ProgramData\MailLink");
            }

            if (!File.Exists(configFile))
            {
                Config config = new Config();
                config.Create();
                config.Serialize();
                return config;
            }
            else
            {
                FileStream fs = new FileStream(configFile, FileMode.Open);

                XmlSerializer xml = new XmlSerializer(typeof(Config));

                Config config = (Config)xml.Deserialize(fs);
                fs.Close();

                return config;
            }

        }

    }

    public class Defaults
    {
        [XmlElement]
        public LogLevel LogLevel { get; set; }
        public int PollInterval { get; set; }
        public int Pop3Port { get; set; }
        public int Pop3EncriptedPort { get; set; }
        public int ImapPort { get; set; }
        public int ImapEncriptedPort { get; set; }
        public int SmtpPort { get; set; }
        public int SmtpEncriptedPort { get; set; }
        public bool KeepMessages { get; set; }
        public int DaysToKeep { get; set; }

        public Defaults()
        {

        }
    }

    public class Setting
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlText]
        public string Value { get; set; }

        public Setting()
        {

        }

        public Setting(String name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    public class Server
    {
        [XmlAttribute]
        public string Type { get; set; }

        [XmlAttribute]
        public string Name { get; set; }

        [XmlElement]
        public string Domain { get; set; }
        public int Port { get; set; }
        public bool RequireSSL { get; set; }
        public bool RequireAuth { get; set; }

        public Server()
        {

        }

        public Server(string type, string name, string domain, int port, bool requireSSL, bool requireAuth)
        {
            Type = type;
            Name = name;
            Domain = domain;
            Port = port;
            RequireSSL = requireSSL;
            RequireAuth = requireAuth;
        }
    }

    public class Mailbox
    {
        [XmlAttribute]
        public string Alias { get; set; }

        [XmlElement]
        public string Name { get; set; }
        public string Description { get; set; }
        public string Inbound { get; set; }
        public string Outbound { get; set; }
        public string Username { get; set; }
        public string Password { get; set; } //TODO: Encript Password
        public string CatchAll { get; set; }
        //TODO: Impliment default email addresses from Recipient Name and Default Domain.
        public string DefaultDomain { get; set; }
        public string LastUID { get; set; }
        public int PollInterval { get; set; }

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

    public class Recipient
    {
        [XmlAttribute]
        public string Alias { get; set; }

        [XmlElement]
        public string Name { get; set; }
        public string Description { get; set; }
        public string MailFrom { get; set; }
        public string MailTo { get; set; }  //TODO: Srtich to emaladdress class, and add CC/BCC
        public string Username { get; set; }
        public string Password { get; set; } //TODO: Encript Password

        public Recipient()
        {

        }
    }
}