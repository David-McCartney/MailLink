using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace MailLink
{
    public class Config
    {

        private Configuration configuration;

        public Config()
        {
            if (!File.Exists(@"config.xml")) 
            {
                Create();
                Save();
            }
            else
            {
                Load();
            }
        }

        private void Create()
        {
            configuration = new Configuration();

        }

        public void Save()
        {
            FileStream fs = new FileStream(@"config.xml", FileMode.Create);


        }

        public void Load()
        {
            FileStream fs = new FileStream(@"config.xml", FileMode.Open);

            XmlSerializer xml = new XmlSerializer(typeof(List<Setting>), new XmlRootAttribute("Configuration"));
            try
            {
                configuration = (Configuration)xml.Deserialize(fs);
            }
            catch { }
        }

    }


    [XmlRootAttribute("Configuration", Namespace = "http://www.cpandl.com", IsNullable = false)]]
    public class Configuration
    {

        public List<Setting> Settings { get; set; }


    }


    public class Setting
    {
        public string Name { get; set; }
        public string Value { get; set; }

        Setting()
        {

        }

        Setting(String name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}