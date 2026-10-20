// Not imported wholesale: Epya.Cache.Common.Action would collide with System.Action,
// which Form1 uses for marshalling onto the UI thread.
using CacheConfigDto = Epya.Cache.Common.CacheConfigDto;
using Epya.Cache.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        Engine engine;
        private readonly CacheConfigDto _cacheConfigs;
        private System.Timers.Timer _timer;

        public Form1(CacheConfigDto cacheConfigs)
        {
            ArgumentNullException.ThrowIfNull(cacheConfigs);
            _cacheConfigs = cacheConfigs;

            InitializeComponent();

            _timer = new System.Timers.Timer();
            _timer.Interval = 100; // Check every 100 milliseconds
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();

            listView1.View = View.Details;
            listView1.FullRowSelect = true;

            // Define Columns
            listView1.Columns.Add("Replica", 100);
            listView1.Columns.Add("Cache Size", 100);
            listView1.Columns.Add("Success Reads", 100);
            listView1.Columns.Add("Fails Reads", 100);
            listView1.Columns.Add("Success Writes", 100);
            listView1.Columns.Add("Fails Writes", 100);
            listView1.Columns.Add("Cache Dependancy Size", 150);
            listView1.Columns.Add("Cache inDependancy Size", 150);

            listView2.View = View.Details;
            listView2.FullRowSelect = true;

            listView2.Columns.Add("Id", 240);
            listView2.Columns.Add("Cache Key", 300);
            listView2.Columns.Add("Written At", 130);
            listView2.Columns.Add("Entities", 300);
            listView2.DoubleClick += listView2_DoubleClick;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text))
                return;

            if (!int.TryParse(textBox1.Text, out int size))
                return;

                engine = new Engine(_cacheConfigs, size);
            engine.StartServer();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            UpdateData();
        }

        private void UpdateData()
        {
            if (engine == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action(UpdateData));
                return;
            }
            label1.Text = (engine.isRunning) ? "Running" : "Stopped";


            //lblTotal.Text = engine.total_clients.ToString();
            //lblCurrent.Text = engine.current_clients.ToString();
            //lblSuccessReads.Text = engine.success_reads.ToString();
            //lblFailReads.Text = engine.fail_reads.ToString();
            lblSucessWrites.Text = engine.success_writes.ToString();
            lblFailWrites.Text = engine.fail_writes.ToString();

            //lblCacheSize.Text = Engine.cacheData.Count.ToString();
            //lblCachDep.Text = Engine.directCacheDependances.Count.ToString();


            listView1.Items.Clear(); // Clear existing items

            if (engine.replicas != null && engine.replicas.Count > 0)
            {
                int index = 0;
                foreach (var replica in engine.replicas)
                {
                    ListViewItem item = new ListViewItem($"Replica {index}");
                    item.SubItems.Add(replica.cacheData.Count.ToString());
                    item.SubItems.Add(replica.successReads.ToString());
                    item.SubItems.Add(replica.failReads.ToString());
                    item.SubItems.Add(replica.successWrites.ToString());
                    item.SubItems.Add(replica.failWrites.ToString());
                    item.SubItems.Add(replica.directCacheDependances.Count().ToString());
                    item.SubItems.Add(replica.indirectCacheDependances.Count().ToString());


                    listView1.Items.Add(item);
                    index += 1;
                }

                _UpdateWrittenItems(engine.replicas[0]);
            }

        }

        // Every replica holds a full copy, so the first one is enough to list what was written.
        // Rows are synced by guid rather than rebuilt, so the 100ms poll keeps the scroll position.
        private void _UpdateWrittenItems(cacheReplica replica)
        {
            // Reverse the dependency index (type|id -> "guid,guid,") into guid -> entities.
            Dictionary<string, List<string>> entitiesByGuid = new Dictionary<string, List<string>>();
            foreach (var dependancy in replica.directCacheDependances)
            {
                foreach (string guid in dependancy.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!entitiesByGuid.TryGetValue(guid, out List<string> entities))
                    {
                        entities = new List<string>();
                        entitiesByGuid[guid] = entities;
                    }
                    entities.Add(dependancy.Key);
                }
            }

            listView2.BeginUpdate();
            try
            {
                for (int i = listView2.Items.Count - 1; i >= 0; i--)
                {
                    if (!replica.cacheMeta.ContainsKey(listView2.Items[i].Name))
                        listView2.Items.RemoveAt(i);
                }

                foreach (var entry in replica.cacheMeta.OrderBy(m => m.Value.Timestamp))
                {
                    string entities = entitiesByGuid.TryGetValue(entry.Key, out List<string> list)
                        ? string.Join(", ", list.OrderBy(e => e))
                        : string.Empty;

                    ListViewItem item = listView2.Items[entry.Key];
                    if (item == null)
                    {
                        item = new ListViewItem(entry.Key) { Name = entry.Key };
                        item.SubItems.Add(entry.Value.CacheKey);
                        item.SubItems.Add(entry.Value.Timestamp.ToString("HH:mm:ss.fff"));
                        item.SubItems.Add(entities);
                        listView2.Items.Add(item);
                    }
                    else if (item.SubItems[3].Text != entities)
                    {
                        // Dependencies land just after cacheMeta, and the background sweep can strip them later.
                        item.SubItems[3].Text = entities;
                    }
                }
            }
            finally
            {
                listView2.EndUpdate();
            }
        }

        private const int BodyPreviewLength = 4000;

        private void listView2_DoubleClick(object sender, EventArgs e)
        {
            if (engine?.replicas == null || engine.replicas.Count == 0 || listView2.SelectedItems.Count == 0)
                return;

            ListViewItem item = listView2.SelectedItems[0];
            string guid = item.Name;
            cacheReplica replica = engine.replicas[0];

            if (!replica.cacheMeta.TryGetValue(guid, out var meta))
            {
                MessageBox.Show(this, "This item is no longer in the cache.", "Cached Item", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            replica.cacheData.TryGetValue(guid, out dynamic data);
            replica.cacheHeaders.TryGetValue(guid, out Dictionary<string, string> headers);
            string body = Convert.ToString(data) ?? string.Empty;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Id:          {guid}");
            sb.AppendLine($"Cache Key:   {meta.CacheKey}");
            sb.AppendLine($"Written At:  {meta.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Body Size:   {body.Length:N0} chars");
            sb.AppendLine();

            sb.AppendLine("Entities:");
            string entities = item.SubItems[3].Text;
            if (string.IsNullOrEmpty(entities))
                sb.AppendLine("  (none)");
            else
                foreach (string entity in entities.Split(", "))
                    sb.AppendLine("  " + entity);
            sb.AppendLine();

            sb.AppendLine("Headers:");
            if (headers == null || headers.Count == 0)
                sb.AppendLine("  (none)");
            else
                foreach (var header in headers)
                    sb.AppendLine($"  {header.Key}: {header.Value}");
            sb.AppendLine();

            sb.AppendLine("Body:");
            string preview = _FormatJson(body);
            if (preview.Length > BodyPreviewLength)
                preview = preview.Substring(0, BodyPreviewLength) + Environment.NewLine + $"... ({preview.Length - BodyPreviewLength:N0} more chars)";
            sb.Append(preview);

            _ShowTextDialog("Cached Item - " + guid, sb.ToString());
        }

        private static string _FormatJson(string json)
        {
            try
            {
                return Newtonsoft.Json.Linq.JToken.Parse(json).ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }

        private void _ShowTextDialog(string title, string text)
        {
            using Form dialog = new Form
            {
                Text = title,
                Size = new Size(700, 550),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                ShowInTaskbar = false
            };

            TextBox textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                Text = text
            };

            Button closeButton = new Button { Text = "Close", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };

            dialog.Controls.Add(textBox);
            dialog.Controls.Add(closeButton);
            dialog.AcceptButton = closeButton;
            dialog.CancelButton = closeButton;

            textBox.Select(0, 0);
            dialog.ShowDialog(this);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            engine.StopServer();

        }

    }
}
