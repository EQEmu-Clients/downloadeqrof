﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO.Compression;
using MySqlConnector;
using System.Diagnostics;
using System.Xml.Linq;
using System.Net.Sockets;
using YamlDotNet.Core.Tokens;

namespace ROF_Downloader
{
    /* General Utility Methods */
    class UtilityLibrary
    {
        public static string EnvironmentPath()
        {
            return $"{Environment.GetEnvironmentVariable("PATH")}";
        }

        public static async Task<int> Download(int startProgress, int endProgress, string source, string outDir, string fileName, int sizeMB)
        {
            string result;
            StatusLibrary.SetProgress(startProgress);
            string path = $"{Application.StartupPath}\\{outDir}\\{fileName}";
            StatusLibrary.SetScope($"Downloading {source} to {path}");
            
            string outFullDir = path.Substring(0, path.LastIndexOf("\\"));
            if (!Directory.Exists(outFullDir))
            {
                StatusLibrary.Log($"Creating directory {outFullDir}");
                Directory.CreateDirectory(outFullDir);
            }

            if (File.Exists(path))
            {
                StatusLibrary.SetStatusBar($"Skipping {outDir}\\{fileName} download, already exists");
                return endProgress;
            }

            StatusLibrary.SetStatusBar($"Downloading {fileName} ({sizeMB} MB) to {outDir}...");
            try
            {
                WebClient client = new WebClient();
                client.Encoding = Encoding.UTF8;
                StatusLibrary.CancelToken().Register(client.CancelAsync);
                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) => {
                    StatusLibrary.SetProgress(startProgress + (int)((endProgress - startProgress) * (float)((float)e.ProgressPercentage / (float)100)));
                };
                StatusLibrary.Log($"download source: {source}, destination: {path}");
                await client.DownloadFileTaskAsync(source, path);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("request was cancelled"))
                {
                    if (File.Exists($"{outDir}\\{fileName}")) {
                        File.Delete($"{outDir}\\{fileName}");
                    }
                    StatusLibrary.SetStatusBar("Cancelled request");
                    return -1;
                }
                result = $"Failed to download {fileName}: {ex.Message}";
                StatusLibrary.SetStatusBar(result);
                MessageBox.Show(result, "Download {fileName}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
            StatusLibrary.SetStatusBar($"Downloaded {fileName} successfully");
            StatusLibrary.SetProgress(endProgress);
            return endProgress;
        }

        public static async Task<int> Extract(int startProgress, int endProgress, string srcDir, string fileName, string outDir, string targetCheckPath, int sizeMB)
        {
            StatusLibrary.SetProgress(startProgress);
            string srcPath = $"{Application.StartupPath}\\{srcDir}\\{fileName}";

            try
            {
                if (File.Exists(targetCheckPath))
                {
                    StatusLibrary.SetStatusBar($"Skipping {srcDir}\\{fileName} extract, already exists");
                    StatusLibrary.SetProgress(endProgress);
                    return endProgress;
                }
                StatusLibrary.SetScope($"Extracting {fileName} ({sizeMB} MB) to {outDir}...");
                //StatusLibrary.SetStatusBar($"Extracting {fileName} ({sizeMB} MB) to {outDir}...");
                if (!Directory.Exists($"{Application.StartupPath}\\{outDir}"))
                {
                    StatusLibrary.Log($"{Application.StartupPath}\\{outDir} doesn't exist, creating it");
                    Directory.CreateDirectory($"{Application.StartupPath}\\{outDir}");
                }

                using (ZipArchive archive = ZipFile.OpenRead(srcPath))
                {
                    int entryCount = archive.Entries.Count;
                    int entryReportStep = 1;
                    int entryReportCounter = 0;
                    if (entryCount > 100)
                    {
                        entryReportStep = 10;
                    }

                    for (int i = 0; i < entryCount; i++)
                    {
                        entryReportCounter++;
                        bool isReportNeeded = false;
                        if (entryReportCounter >= entryReportStep)
                        {
                            isReportNeeded = true;
                            entryReportCounter = 0;
                        }
                        ZipArchiveEntry entry = archive.Entries[i];

                        int value = startProgress + (int)((endProgress - startProgress) * (float)((float)i / (float)entryCount));
                        if (isReportNeeded)
                        {
                            StatusLibrary.SetProgress(value);
                        }

                        string zipPath = entry.FullName.Replace("/", "\\").Replace("EQEmuMaps-master\\", "").Replace("projecteqquests-master\\", "");
                        if (zipPath.StartsWith("c\\"))
                        {
                            zipPath = zipPath.Substring(2);
                        }
                        if (zipPath.Length == 0)
                        {
                            continue;
                        }
                        string outPath = $"{Application.StartupPath}\\{outDir}\\{zipPath}";
                        StatusLibrary.SetScope($"Extracting {zipPath} to {outPath}");
                        string zipDir = outPath.Substring(0, outPath.LastIndexOf("\\"));
                        if (!Directory.Exists(zipDir))
                        {
                            StatusLibrary.Log($"Creating directory {zipDir}");
                            Directory.CreateDirectory(zipDir);
                        }

                        if (outPath.EndsWith("\\"))
                        {
                            continue;
                        }
                        if (isReportNeeded)
                        {
                            StatusLibrary.SetStatusBar($"Extracting {outDir}\\{zipPath}...");
                            StatusLibrary.Log($"Extracting to {outPath}");
                        }
                        using (Stream zipStream = entry.Open())
                        {
                            FileStream fileStream = File.Create(outPath);
                            await zipStream.CopyToAsync(fileStream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string result = $"Failed to extract {srcPath}: {ex.Message}";
                StatusLibrary.SetStatusBar(result);
                MessageBox.Show(result, $"Extract {fileName}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
            StatusLibrary.SetStatusBar($"Extracted {fileName} successfully");
            StatusLibrary.SetProgress(endProgress);
            return endProgress;
        }

        public static string GetMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    
                    StringBuilder sb = new StringBuilder();

                    for (int i = 0; i < hash.Length; i++)
                    {
                        sb.Append(hash[i].ToString("X2"));
                    }

                    return sb.ToString();
                }
            }
        }

        public static string GetJson(string urlPath)
        {
            using (WebClient wc = new WebClient())
            {
                return wc.DownloadString(urlPath);
            }
        }



        public static string GetSHA1(string filePath)
        {
            //SHA1 sha = new SHA1CryptoServiceProvider();            
            //var stream = File.OpenRead(filePath);
            //return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty); ;
            /*Encoding enc = Encoding.UTF8;

            var sha = SHA1.Create();
            var stream = File.OpenRead(filePath);

            string hash = "commit " + stream.Length + "\0";
            return enc.GetString(sha.ComputeHash(stream));

            return BitConverter.ToString(sha.ComputeHash(stream));*/
            Encoding enc = Encoding.UTF8;

            string commitBody = File.OpenText(filePath).ReadToEnd();
            StringBuilder sb = new StringBuilder();
            sb.Append("commit " + Encoding.UTF8.GetByteCount(commitBody));
            sb.Append("\0");
            sb.Append(commitBody);

            var sss = SHA1.Create();
            var bytez = Encoding.UTF8.GetBytes(sb.ToString());
            return BitConverter.ToString(sss.ComputeHash(bytez));
            //var myssh = enc.GetString(sss.ComputeHash(bytez));
            //return myssh;
        }


    }
}
