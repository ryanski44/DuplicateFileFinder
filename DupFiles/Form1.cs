using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DupFiles
{
    public partial class Form1 : Form
    {
        private List<string> paths;

        public Form1()
        {
            InitializeComponent();
            paths = new List<string>();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                string path = dlg.SelectedPath;
                paths.Add(path);
            }
            UpdatePathList();
        }

        private void UpdatePathList()
        {
            listViewFolders.Clear();
            foreach (var path in paths)
            {
                listViewFolders.Items.Add(new ListViewItem(path));
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            paths.Clear();
            UpdatePathList();
        }

        private void UpdateProgressBar(double percentage)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => UpdateProgressBar(percentage)));
            }
            else
            {
                progressBar.Value = (int)percentage;
            }
        }

        private void UpdateStatus(string text, params string[] args)
        {
            UpdateStatus(string.Format(text, args));
        }

        private void UpdateStatus(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => UpdateStatus(text)));
            }
            else
            {
                labelStatus.Text = text;
            }
        }

        private void AddTreeNode(TreeNode node)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => AddTreeNode(node)));
            }
            else
            {
                treeViewMain.Nodes.Add(node);
            }
        }

        private void Compare()
        {
            treeViewMain.Nodes.Clear();

            Thread t = new Thread(new ThreadStart(CompareWorker));
            t.IsBackground = true;
            t.Start();
        }

        private void CompareWorker()
        {
            Dictionary<long, FileInfo> files = new Dictionary<long, FileInfo>();
            Dictionary<long, List<FileInfo>> dupes = new Dictionary<long, List<FileInfo>>();

            List<FileInfo> allFiles = new List<FileInfo>();
            foreach (string dirPath in paths)
            {
                DirectoryInfo di = new DirectoryInfo(dirPath);
                if (di.Exists)
                {
                    UpdateStatus("Looking for files in {0} ...", di.FullName);
                    GetAllFiles(di, allFiles);
                }
            }

            UpdateProgressBar(15);

            Dictionary<string, string> excludedExtensions = new Dictionary<string, string>();
            foreach (string excludedExtension in Config.ExcludedFileTypes)
            {
                excludedExtensions[excludedExtension] = String.Empty;
            }

            UpdateStatus("Limiting By File Length ...");
            int j = 0;
            foreach (FileInfo fi in allFiles)
            {
                if (!excludedExtensions.ContainsKey(fi.Extension.Trim('.')))
                {
                    if (files.ContainsKey(fi.Length))
                    {
                        if (!dupes.ContainsKey(fi.Length))
                        {
                            dupes.Add(fi.Length, new List<FileInfo>());
                            dupes[fi.Length].Add(files[fi.Length]);
                        }
                        dupes[fi.Length].Add(fi);
                    }
                    else
                    {
                        files.Add(fi.Length, fi);
                    }
                }
                if (j % 20 == 0)
                {
                    UpdateProgressBar(15.0 + 30.0 * j / allFiles.Count);
                }
                j++;
            }
                
            

            MD5CryptoServiceProvider hashProvider = new MD5CryptoServiceProvider();

            Dictionary<string, List<FileInfo>> probableDupes = new Dictionary<string, List<FileInfo>>();

            j = 0;
            foreach (long dupLength in dupes.Keys)
            {
                List<FileInfo> duplicateFiles = dupes[dupLength];

                int bufferLength = 1024;
                if (dupLength < bufferLength)
                {
                    bufferLength = (int)dupLength;
                }
                byte[] buffer = new byte[bufferLength];

                //find probable duplicates
                foreach (FileInfo file in duplicateFiles)
                {
                    UpdateStatus("Quick Hashing Pontential Duplicate {0}", file.FullName);
                    try
                    {
                        using (FileStream stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                        {
                            if (stream.Read(buffer, 0, buffer.Length) == buffer.Length)
                            {
                                byte[] hashValue = hashProvider.ComputeHash(buffer);
                                string hashValueStr = ToHEX(hashValue);
                                if (!probableDupes.ContainsKey(hashValueStr))
                                {
                                    probableDupes[hashValueStr] = new List<FileInfo>();
                                }
                                probableDupes[hashValueStr].Add(file);
                            }
                        }
                    }
                    catch (Exception ex) { }
                }
                
                j++;
                if (j % 5 == 0)
                {
                    UpdateProgressBar(50.0 + 40.0 * j / dupes.Keys.Count);
                }
            }

            Dictionary<string, List<FileInfo>> realDupes = new Dictionary<string, List<FileInfo>>();

            j = 0;
            foreach (string dupKey in probableDupes.Keys)
            {
                List<FileInfo> duplicateFiles = probableDupes[dupKey];
                if (duplicateFiles.Count > 1)
                {
                    //find true duplicates
                    foreach (FileInfo file in duplicateFiles)
                    {
                        UpdateStatus("Hashing Probable Duplicate {0}", file.FullName);
                        try
                        {
                            using (FileStream stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                            {
                                byte[] hashValue = hashProvider.ComputeHash(stream);
                                string hashValueStr = ToHEX(hashValue);
                                if (!realDupes.ContainsKey(hashValueStr))
                                {
                                    realDupes[hashValueStr] = new List<FileInfo>();
                                }
                                realDupes[hashValueStr].Add(file);
                            }
                        }
                        catch (Exception) { }
                    }
                }
                j++;
                if (j % 5 == 0)
                {
                    UpdateProgressBar(90.0 + 9.0 * j / probableDupes.Keys.Count);
                }
            }

            foreach (string dupKey in realDupes.Keys)
            {
                List<FileInfo> duplicateFiles = realDupes[dupKey];

                if (duplicateFiles.Count > 1)
                {
                    TreeNode top = new TreeNode(duplicateFiles[0].FullName);
                    top.Tag = duplicateFiles[0];
                    for (int i = 1; i < duplicateFiles.Count; i++)
                    {
                        TreeNode sub = new TreeNode(duplicateFiles[i].FullName);
                        sub.Tag = duplicateFiles[i];
                        top.Nodes.Add(sub);
                    }
                    AddTreeNode(top);
                }
            }
            UpdateProgressBar(0);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Compare();
        }

        private static string ToHEX(byte[] input)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in input)
            {
                sb.Append(b.ToString("X"));
            }
            return sb.ToString();
        }

        private static void GetAllFiles(DirectoryInfo di, List<FileInfo> files)
        {
            try
            {
                foreach (DirectoryInfo subDir in di.GetDirectories())
                {
                    GetAllFiles(subDir, files);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine(e.Message);
            }
            try
            {
                files.AddRange(di.GetFiles());
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            foreach (TreeNode node in treeViewMain.Nodes)
            {
                node.Checked = false;
                node.Expand();
                foreach (TreeNode subNode in node.Nodes)
                {
                    subNode.Checked = true;
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            List<FileInfo> toDelete = new List<FileInfo>();
            foreach (TreeNode node in treeViewMain.Nodes)
            {
                if (node.Checked)
                {
                    toDelete.Add(node.Tag as FileInfo);
                }
                foreach (TreeNode subNode in node.Nodes)
                {
                    if (subNode.Checked)
                    {
                        toDelete.Add(subNode.Tag as FileInfo);
                    }
                }
            }
            foreach (FileInfo file in toDelete)
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Exception: " + ex.ToString());
                }
            }

            Compare();
        }
    }
}
