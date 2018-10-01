using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace CloudIP
{
    public partial class Form1 : Form
    {
        private string currentIP = string.Empty;
        private int count = 0;
        private string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/cloudip";
        private Fields fields = new Fields();

        public Form1()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);
            else
                LoadS();

            if(File.Exists(appFolder + "/logs.txt"))
            {
                string allLogs = File.ReadAllText(appFolder + "/logs.txt");
                richTextBox1.Text = allLogs;
            }

            Thread thread = new Thread(Time);
            thread.Start();
        }

        private void Time()
        {
            UpdateIP();

            if (textBox_Api.Text != "" && textBox_Domain.Text != "" && textBox_Email.Text != "" && textBox_ID.Text != "" && textBox_ZoneID.Text != "")
            {
                UpdateDomains();
                UpdateDNSRecord(textBox_ZoneID.Text, textBox_ID.Text, GetDNSType(), textBox_Domain.Text, currentIP);

                while (true)
                {
                    if (count >= 60)
                    {
                        count = 0;

                        UpdateIP();
                        UpdateDNSRecord(textBox_ZoneID.Text, textBox_ID.Text, "A", textBox_Domain.Text, currentIP);
                    }
                    else
                    {
                        label8.BeginInvoke((MethodInvoker)delegate () { this.label8.Text = 60 - count + " seconds"; ; });
                    }

                    count++;
                    Thread.Sleep(1000);
                }
            }
            else
            {
                label10.BeginInvoke((MethodInvoker)delegate () { this.label10.Text = "Waiting..."; ; });
            }
        }

        private void UpdateDomains()
        {
            MakeLog("Getting domain ID...");

            try
            {
                string url = String.Format("https://api.cloudflare.com/client/v4/zones/{0}/dns_records", textBox_ZoneID.Text);

                var client = new RestClient(url);
                var request = new RestRequest(Method.GET);

                request.AddHeader("X-Auth-Email", textBox_Email.Text);
                request.AddHeader("X-Auth-Key", textBox_Api.Text);
                request.AddHeader("Content-Type", "application/json");

                IRestResponse response = client.Execute(request);

                string content = response.Content; // raw content as string

                RootObject RO = JsonConvert.DeserializeObject<RootObject>(content);

                bool found = false;

                foreach (var res in RO.result)
                {
                    if (res.name == textBox_Domain.Text)
                    {
                        textBox_ID.Text = res.id;
                        textBox_ZoneID.Text = res.zone_id;

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    string domains = string.Empty;

                    foreach (var res in RO.result)
                    {
                        domains += "\n" + res.name;
                    }

                    MessageBox.Show("We didn't find any domain called " + textBox_Domain.Text + ". All domains found:\n" + domains, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                else
                {
                    MakeLog("Domain ID found: " + textBox_ID.Text);
                }
            }
            catch
            {
                MakeLog("Error getting domain ID");
            }
        }

        private void UpdateDNSRecord(string zoneIdentifier, string identifier, string type = null, string domain = "domain.com", string host = "127.0.0.1")
        {
            MakeLog("Updating DNS records...");

            try
            {
                string url = String.Format("https://api.cloudflare.com/client/v4/zones/{0}/dns_records/{1}", zoneIdentifier, identifier);

                var client = new RestClient(url);
                var request = new RestRequest(Method.PUT);

                request.AddHeader("X-Auth-Email", textBox_Email.Text);
                request.AddHeader("X-Auth-Key", textBox_Api.Text);
                request.AddHeader("Content-Type", "application/json");

                dynamic data = new ExpandoObject();
                data.type = type;
                data.name = domain;
                data.content = host;
                data.proxied = checkBox1.Checked;

                Debug.WriteLine(checkBox1.Checked);

                request.AddJsonBody(data);

                IRestResponse response = client.Execute(request);
                var content = response.Content; // raw content as string

                Debug.WriteLine(content);

                string time = DateTime.Now.ToString("hh:mm:ss"); // includes leading zeros
                string date = DateTime.Now.ToString("dd/MM/yy"); // includes leading zeros

                label10.BeginInvoke((MethodInvoker)delegate () { this.label10.Text = date + " " + time; ; });
                label10.ForeColor = Color.Black;

                MakeLog("DNS Records updated " + type + "|" + domain + "|" + host + "|" + GetDNSType() + "|" + (checkBox1.Checked ? "Proxied" : "DNS Only"));
            }
            catch
            {
                MakeLog("Error updating DNS records");
            }
        }

        private void UpdateIP()
        {
            using (WebClient client = new WebClient())
            {
                string ip = client.DownloadString("https://api.ipify.org").Trim();
                currentIP = ip;
                label_currIP.BeginInvoke((MethodInvoker)delegate () { this.label_currIP.Text = ip; ; });
            }
        }

        private void MakeLog(string text)
        {
            Thread thread = new Thread(() => CreateLog(text));
            thread.Start();
        }

        private void CreateLog(string text)
        {
            try
            {
                string time = DateTime.Now.ToString("hh:mm:ss"); // includes leading zeros
                string date = DateTime.Now.ToString("dd/MM/yy"); // includes leading zeros

                string formated = String.Format("[{0} - {1}] {2}\n", date, time, text);

                File.AppendAllText(appFolder + "/logs.txt", formated);

                string allLogs = File.ReadAllText(appFolder + "/logs.txt");

                richTextBox1.BeginInvoke((MethodInvoker)delegate () { this.richTextBox1.Text = allLogs; ; });
            }
            catch { }
        }

        private string GetDNSType()
        {
            if (radioButton1.Checked)
                return "A";
            else if (radioButton2.Checked)
                return "AAAA";
            else
                return "CNAME";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox_Api.Text != "" && textBox_Domain.Text != "" && textBox_Email.Text != "" && textBox_ZoneID.Text != "")
            {
                UpdateDomains();
            }
            else
            {
                MessageBox.Show("Please fill all the fields.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(textBox_Api.Text != "" && textBox_Domain.Text != "" && textBox_Email.Text != "" && textBox_ID.Text != "" && textBox_ZoneID.Text != "")
            {
                if (label10.Text == "Waiting...")
                {
                    Thread thread = new Thread(Time);
                    thread.Start();
                }
                else
                {
                    count = 60;
                }
            }
            else
            {
                MessageBox.Show("Please get the domain identifier first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void Save()
        {
            fields.email = textBox_Email.Text;
            fields.apiKey = textBox_Api.Text;
            fields.domainID = textBox_ID.Text;
            fields.domainName = textBox_Domain.Text;
            fields.zoneID = textBox_ZoneID.Text;

            if (radioButton1.Checked)
                fields.dnsType = "A";
            else if (radioButton2.Checked)
                fields.dnsType = "AAAA";
            else
                fields.dnsType = "CNAME";

            fields.httpProxy = checkBox1.Checked;

            string json = JsonConvert.SerializeObject(fields);

            File.WriteAllText(appFolder + "/save.json", json);
        }

        private void LoadS()
        {
            string save = File.ReadAllText(appFolder + "/save.json");

            fields = JsonConvert.DeserializeObject<Fields>(save);

            textBox_Api.Text = fields.apiKey;
            textBox_Domain.Text = fields.domainName;
            textBox_Email.Text = fields.email;
            textBox_ID.Text = fields.domainID;
            textBox_ZoneID.Text = fields.zoneID;

            if (fields.dnsType == "A")
                radioButton1.Checked = true;
            else if (fields.dnsType == "AAAA")
                radioButton2.Checked = true;
            else
                radioButton3.Checked = true;

            checkBox1.Checked = fields.httpProxy;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Save();
            Environment.Exit(-1);
        }

        private void OnProcessExit(object sender, EventArgs e)
        {

        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            File.Delete(appFolder + "/logs.txt");
            richTextBox1.ResetText();
            MakeLog("Logs cleared");
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }

        private void label13_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/bruxo00");
        }

        private void label12_Click(object sender, EventArgs e)
        {
            Process.Start("https://twitter.com/imBruxoPT");
        }
    }

    public class Meta
    {
        public bool auto_added { get; set; }
        public bool managed_by_apps { get; set; }
        public bool managed_by_argo_tunnel { get; set; }
    }

    public class Data
    {
        public string service { get; set; }
        public string proto { get; set; }
        public string name { get; set; }
        public int weight { get; set; }
        public int port { get; set; }
        public string target { get; set; }
        public int priority { get; set; }
    }

    public class Result
    {
        public string id { get; set; }
        public string type { get; set; }
        public string name { get; set; }
        public string content { get; set; }
        public bool proxiable { get; set; }
        public bool proxied { get; set; }
        public int ttl { get; set; }
        public bool locked { get; set; }
        public string zone_id { get; set; }
        public string zone_name { get; set; }
        public DateTime modified_on { get; set; }
        public DateTime created_on { get; set; }
        public Meta meta { get; set; }
        public int? priority { get; set; }
        public Data data { get; set; }
    }

    public class ResultInfo
    {
        public int page { get; set; }
        public int per_page { get; set; }
        public int total_pages { get; set; }
        public int count { get; set; }
        public int total_count { get; set; }
    }

    public class RootObject
    {
        public List<Result> result { get; set; }
        public ResultInfo result_info { get; set; }
        public bool success { get; set; }
        public List<object> errors { get; set; }
        public List<object> messages { get; set; }
    }
}
