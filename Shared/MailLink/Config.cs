using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace MailLink
{
    public enum LogLevel { Fatal, Error, Warning, Informational, Verbose };
    public enum ServerType { SMTP, POP3, IMAP }

    [Serializable] [XmlRoot(Namespace = "")]
    public class Config
    {
        [XmlElement]
        public Setting Settings { get; set; }

        [XmlElement]
        public Default Defaults { get; set; }

        [XmlArrayItem(ElementName = "Server", Type = typeof(Server))]
        public List<Server> Servers { get; set; }

        //public Config Config { get; protected set; }

        public Config()
        {
            Defaults = new Default();
            Settings = new Setting();
            Servers = new List<Server>();

            //Initialize();
        }

        /// <summary>
        /// Creates a new config object populated with sample data.
        /// </summary>
        private void Create()
        {
            // Initialize Settings.
            Settings.TickInterval = 30; // Fifteen Seconds
            Settings.TockInterval = 2 * 60; // One Minute
            Settings.MaxMailboxThreads = 1;
            Settings.MaxAttachmentThreads = 2;

            // Initialize Defaults.
            Defaults.LogLevel = LogLevel.Verbose;
            Defaults.PollInterval = 1;
            Defaults.Pop3Port = 110;
            Defaults.Pop3EncriptedPort = 995;
            Defaults.ImapPort = 143;
            Defaults.ImapEncriptedPort = 993;
            Defaults.SmtpPort = 25;
            Defaults.SmtpEncriptedPort = 587;
            Defaults.KeepMessages = true;
            Defaults.DaysToKeep = 14;

            Servers = new List<Server>()
            {
                new Server()
                {
                    Type = ServerType.SMTP,
                    Alias = "CSSOK",
                    Domain = "cssokc-ms1.coreslab.local",
                    Port = 25,
                    RequireSSL = false,
                    RequireAuth = false
                },
                new Server()
                {
                    Type = ServerType.POP3,
                    Alias = "Coreslab",
                    Domain = "mail.coreslab.com",
                    Port = 110,
                    RequireSSL = false,
                    RequireAuth = false
                },
                new Server()
                {
                    Type = ServerType.SMTP,
                    Alias = "TWFM",
                    Domain = "twfm.net.mail.eo.outlook.com",
                    Port = 25,
                    RequireSSL = false,
                    RequireAuth = false
                },
                new Server()
                {
                    Type = ServerType.POP3,
                    Alias = "TWFM",
                    Domain = "outlook.office365.com",
                    Port = 995,
                    RequireSSL = true,
                    RequireAuth = true
                },
                new Server()
                {
                    Type = ServerType.POP3,
                    Alias = "IT1",
                    Domain = "outlook.office365.com",
                    Port = 995,
                    RequireSSL = true,
                    RequireAuth = true
                }
            };

        }

        public void Serialize()
        {
            if (!Directory.Exists(@"c:\ProgramData\MailLink"))
            {
                Directory.CreateDirectory(@"c:\ProgramData\MailLink");
            }

            string configFile = @"c:\ProgramData\MailLink\config.xml";
            FileStream fs = new FileStream(configFile, FileMode.OpenOrCreate);

            XmlSerializerNamespaces xmlns = new XmlSerializerNamespaces(); xmlns.Add("", "");
            //xmlns.Add("ML", "https://github.com/David-McCartney/MailLink");
            XmlSerializer xml = new XmlSerializer(typeof(Config));

            xml.Serialize(fs, this, xmlns);
            //xml.Serialize(fs, configuration);

            fs.Close();
        }

        public static Config Deserialize()
        {
            string configFile = @"c:\ProgramData\MailLink\config.xml";

            if (!File.Exists(configFile))
            {
                Config c = new Config();
                c.Create();
                c.Serialize();
                return c;
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

    public class Default
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

        public Default()
        {

        }
    }

    public class Setting
    {
        [XmlElement]
        public int TickInterval { get; set; }
        public int TockInterval { get; set; }
        public int MaxMailboxThreads { get; set; }
        public int MaxAttachmentThreads { get; set; }

        public Setting()
        {

        }
    }


    public class Server
    {
        [XmlAttribute]
        public ServerType Type { get; set; }
        public string Alias { get; set; }

        [XmlElement]
        public string Domain { get; set; }
        public int Port { get; set; }
        public bool RequireSSL { get; set; }
        public bool RequireAuth { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        //TODO: Encript Password

        public Server()
        {
        }
    }


}