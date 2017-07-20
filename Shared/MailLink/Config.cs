using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace MailLink
{
    public class Config
    {

        public Configuration Root { get; protected set; }

        public Config()
        {
            if (!File.Exists(@"c:\projects\config.xml")) 
            {
                Create();
                Save();
            }
            else
            {
                Create(); //Temporarly skip loading
                Save();
                //Load();
            }
        }

        private void Create()
        {
            Root = new Configuration();

            Root.PollInterval   =  5 * 60; // Five Minutes
            Root.SystemInterval = 15 * 60; // Fifteen Minutes

            Root.Settings.Add(new Setting("Testing", "Test"));

            Root.Servers.Add(new Server("SMTP", "CSSOK", "cssokc-ms1.coreslab.local", 25));
            Root.Servers.Add(new Server("POP", "Coreslab", "psmtp.coreslab.com", 110));
            Root.Servers.Add(new Server("POP", "IT1", "pop.it1.biz", 110));

            Root.Users.Add(new User("David McCartney","dmc@it1.biz","IT1","CSSOK","dmccartney","Maui2010!"));
            Root.Users.Add(new User { Name = "David McCartney", Email = "dmccartney@coreslab.com",
                                               Inbound = { Server = "Coreslab", Username = "dmccartney", Password = "Maui2010!" },
                                               Outbound = { Server = "CSSOK" }  });
        }

        public void Save()
        {
            FileStream fs = new FileStream(@"c:\projects\config.xml", FileMode.Create);

            XmlSerializerNamespaces xmlns = new XmlSerializerNamespaces();
            //xmlns.Add("ML", "https://github.com/David-McCartney/MailLink");
            xmlns.Add("", "");
            XmlSerializer xml = new XmlSerializer(typeof(Configuration));

            xml.Serialize(fs, Root, xmlns);
            //xml.Serialize(fs, configuration);

            fs.Close();
        }

        public void Load()
        {
            FileStream fs = new FileStream(@"c:\projects\config.xml", FileMode.Open);

            XmlSerializer xml = new XmlSerializer(typeof(Configuration));
            Root = (Configuration)xml.Deserialize(fs);

            fs.Close();
        }

    }

    [XmlRoot]
    public class Configuration
    {
        [XmlElement]
        public int PollInterval { get; set; }

        [XmlElement]
        public int SystemInterval { get; set; }

        [XmlArrayItem(ElementName = "Setting", Type = typeof(Setting))]
        public List<Setting> Settings { get; set; }

        [XmlArrayItem(ElementName = "Server", Type = typeof(Server))]
        public List<Server> Servers { get; set; }

        [XmlArrayItem(ElementName = "User", Type = typeof(User))]
        public List<User> Users { get; set; }

        public Configuration()
        {
            Settings = new List<Setting>();
            Servers = new List<Server>();
            Users = new List<User>();
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

        [XmlElement]
        public int Port { get; set; }

        [XmlElement]
        public bool RequireSSL { get; set; }

        public Server()
        {

        }

        public Server(string type, string name, string domain, int port)
        {
            Type = type;
            Name = name;
            Domain = domain;
            Port = port;

            RequireSSL = false;
        }
    }

    public class User
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlElement]
        public string Email { get; set; }

        [XmlElement]
        public Credentials Inbound { get; set; }

        [XmlElement]
        public Credentials Outbound { get; set; }

        [XmlIgnore]
        public bool Busy { get; set; }

        public User()
        {
            Inbound = new Credentials();
            Outbound = new Credentials();
        }

        public User(string name, string email, string inbound, string outbound, string username, string pasword)
        {
            Name = name;
            Email = email;
            Inbound = new Credentials(inbound, username, pasword);
            Outbound = new Credentials(outbound, null, null);

            Busy = false;
        }
    }

    public class Credentials
    {
        [XmlAttribute]
        public string Server { get; set; }

        [XmlElement]
        public string Username { get; set; }

        //TODO: Encript Password
        [XmlElement]
        public string Password { get; set; }

        public Credentials()
        {

        }

        public Credentials(string server, string username, string password)
        {
            Server = server;
            Username = username;
            Password = password;
        }
    }
}