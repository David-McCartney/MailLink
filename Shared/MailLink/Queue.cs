using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace MailLink
{
    public enum Status { Waiting, Downloading, Uploading, Failed, Complete }

    [Serializable] [XmlRoot]
    public class Queues : List<Queue>
    {

        public Queues()
        {
        }

        public void Serialize()
        {
            if (!Directory.Exists(@"c:\ProgramData\MailLink"))
            {
                Directory.CreateDirectory(@"c:\ProgramData\MailLink");
            }

            string queueFile = @"c:\ProgramData\MailLink\queue.xml";
            using (FileStream fs = new FileStream(queueFile, FileMode.OpenOrCreate))
            {
                XmlSerializerNamespaces xmlns = new XmlSerializerNamespaces(); xmlns.Add("", "");
                XmlSerializer xml = new XmlSerializer(typeof(Queues));

                xml.Serialize(fs, this, xmlns);

                fs.Close();
            }
        }

        public static Queues Deserialize()
        {
            string queueFile = @"c:\ProgramData\MailLink\queue.xml";

            if (!File.Exists(queueFile))
            {
                Queues q = new Queues();
                q.Serialize();
                return q;
            }
            else
            {
                using (FileStream fs = new FileStream(queueFile, FileMode.Open))
                {
                    XmlSerializer xml = new XmlSerializer(typeof(Queues));

                    Queues m = (Queues)xml.Deserialize(fs);
                    fs.Close();
                    return m;
                }
            }
        }
    }

    public class Queue
    {
        [XmlAttribute]
        //public string Alias { get; set; }
        public string UID { get; set; }

        [XmlElement]
        public string MessageID { get; set; }
        public String Owner { get; set; }
        public int Size { get; set; }
        public Status Status { get; set; }

        [XmlIgnore]
        public int Progress { get; set; }

        public Queue()
        {
        }
     }
}

