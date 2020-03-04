using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Founder2.Searcher;
using static Founder2.MainFormViewModel;

namespace Founder2
{

    public partial class Form1 : Form
    {
        CancellationTokenSource cancelTokenSource;
        private ManualResetEvent _busy = new ManualResetEvent(false);
        private Stopwatch workTimer;
        private int ProcessedDirs;
        private int processed;
        private int count;
        private Searcher searcher { get; set; }
        public string CurrentSearchPath { get; set; }
        CancellationToken CancelToken;
        public Form1()
        {
            InitializeComponent();
            
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (!SearchingInWork)
            {
                timer1.Start();
                ClearForm();
                cancelTokenSource = new CancellationTokenSource();
                CancelToken = cancelTokenSource.Token;

                searcher = new Searcher(SearchPathTextBox.Text, PaternTextBox.Text, SearchStringTextBox.Text, _busy, CancelToken);
                searcher.SearchingNotify += Search_Notify;
                searcher.FoundFile += Search_Found;
                searcher.ProcessedUp += Processed_Up;
                searcher.CountUp += Count_Up;
                searcher.CompleteSearch += Search_CompleteSearch;
                searcher.CancelSearch += Search_CancelSearch;

                this.workTimer = new Stopwatch();

                SearchPath = SearchPathTextBox.Text;

                NodeCollection = new TreeNode { Name = SearchPath, Text = $"Поиск в {SearchPath}" };
                treeView1.Nodes.Add( new TreeNode { Name = SearchPath, Text = $"Поиск в {SearchPath}" });

                button2.Enabled = true;
                button2.Text = "Остановить";

                _busy.Set();
                workTimer.Start();
                SearchingInWork = true;

                await Task.Run(() => fillRootNodes(treeView1, _busy));
                await Task.Run(() => searcher.StartSearch());
                await Task.Run(() => fillRootNodes(treeView1, _busy));
            }
        }

        private void Search_CancelSearch(object sender, CompleteEventArgs e)
        {
            SearchingInWork = false;
            timer1.Stop();
            var elaps = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", e.Elaps.Hours,
                e.Elaps.Minutes, e.Elaps.Seconds,
                e.Elaps.Milliseconds / 10);
            label1.Invoke((Action)(() => label1.Text = $"Прошло: {elaps} Просмотрено папок: {ProcessedDirs} Обработано {e.Processed} Найдено: {e.Count} {e.Message}"));
        }

        private void Search_CompleteSearch(object sender, CompleteEventArgs e)
        {
            Properties.Settings.Default.LastSearchPath = e.SearchPath;
            Properties.Settings.Default.LastSearchPattern = e.SearchPattern;
            Properties.Settings.Default.LastSearchText = e.SearchText;
            Properties.Settings.Default.Save();
            SearchingInWork = false;
            timer1.Stop();
            var elaps = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", e.Elaps.Hours,
                e.Elaps.Minutes, e.Elaps.Seconds,
                e.Elaps.Milliseconds / 10);
            label1.Invoke((Action)(() => label1.Text = $"Прошло: {elaps}Просмотрено папок: {ProcessedDirs} Обработано {e.Processed} Найдено: {e.Count} Ошибок доступа: {e.ErrorsPaths.Length} {e.Message}"));
        }

        private void Search_Notify(object sender, SearchEventArgs e)
        {
            CurrentSearchPath = e.FilePath;
            
        }
        private void Search_Found(object sender, SearchEventArgs e)
        {
            PrepareNodes(e.FilePath);
        }
        private void Processed_Up(object sender, ProcessedUpEventArgs e)
        {
            processed = e.Count;
            ProcessedDirs = e.DirsCount;
        }
        public void Count_Up(object sender, ProcessedUpEventArgs e)
        {
            ProcessedDirs = e.DirsCount;
            count = e.Count;
        }
        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            Task.Run(() => MainFormViewModel.fillExpanded((TreeView)sender, e.Node, _busy));
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SetWorkerMode(button2.Text == "Продолжить");
        }
        private void SetWorkerMode(bool running)
        {
            if (running)
            {
                button2.Text = "Остановить";
                _busy.Set();
                workTimer.Start();
            }
            else
            {
                button2.Text = "Продолжить";
                _busy.Reset();
                workTimer.Stop();
            }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            SetWorkerMode(true);
            cancelTokenSource.Cancel();
            button2.Enabled = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string temp = SearchPathTextBox.Text;
            FolderBrowserDialog fd = new FolderBrowserDialog();
            fd.ShowDialog();
            SearchPathTextBox.Text = fd.SelectedPath == "" ? temp : fd.SelectedPath;
        }
        private void ClearForm()
        {
            label1.Text = "";
            treeView1.Nodes.Clear();
            button2.Text = "Остановить";
            button2.Enabled = true;
            button3.Enabled = true;
            count = 0;
            processed = 0;
            ProcessedDirs = 0;
        }
        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            try { Process.Start(treeView1.SelectedNode?.Name); }
            catch (Exception) { }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var elaps = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", workTimer.Elapsed.Hours,
                 workTimer.Elapsed.Minutes, workTimer.Elapsed.Seconds,
                 workTimer.Elapsed.Milliseconds / 10);
            label1.Invoke((Action)(() => label1.Text = $"Прошло: {elaps} Просмотрено папок: {ProcessedDirs} Обработано файлов: {processed} Найдено: {count} Поиск в: {CurrentSearchPath}"));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SearchPathTextBox.Text = Properties.Settings.Default.LastSearchPath;
            PaternTextBox.Text= Properties.Settings.Default.LastSearchPattern;
            SearchStringTextBox.Text= Properties.Settings.Default.LastSearchText;
        }
    }
}
