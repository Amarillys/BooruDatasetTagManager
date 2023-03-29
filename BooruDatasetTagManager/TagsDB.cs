﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Translator.Crypto;
using static System.Net.Mime.MediaTypeNames;

namespace BooruDatasetTagManager
{
    [Serializable]
    public class TagsDB
    {
        public List<TagItem> Tags;
        public Dictionary<string, long> LoadedFiles;

        public TagsDB()
        {
            Tags = new List<TagItem>();
            LoadedFiles = new Dictionary<string, long>();
        }

        private string[] ReadAllLines(byte[] data, Encoding encoding)
        {
            
            List<string> list = new List<string>();
            using (MemoryStream ms = new MemoryStream(data))
            {
                using (StreamReader streamReader = new StreamReader(ms, encoding))
                {
                    string item;
                    while ((item = streamReader.ReadLine()) != null)
                    {
                        list.Add(item);
                    }
                }
            }
            return list.ToArray();
        }

        public void LoadCSVFromDir(string dir)
        {
            FileInfo[] csvFiles = new DirectoryInfo(dir).GetFiles("*.csv", SearchOption.TopDirectoryOnly);
            Tags.Clear();
            LoadedFiles.Clear();
            foreach (var item in csvFiles)
                LoadFromCSVFile(item.FullName);
        }


        public void LoadFromCSVFile(string fPath, bool append = true)
        {
            Regex r = new Regex("(.*?),(\\d+),(\\d+),(.*)");
            char[] splitter = { ',' };
            byte[] data = File.ReadAllBytes(fPath);
            long hash = Adler32.GenerateHash(data);
            string fName = Path.GetFileName(fPath);
            if (LoadedFiles.ContainsKey(fName))
            {
                if (LoadedFiles[fName] == hash)
                    return;
                else
                    LoadedFiles[fName] = hash;
            }
            else
            {
                LoadedFiles.Add(fName, hash);
            }


            string[] lines = ReadAllLines(data, Encoding.UTF8);
            if (!append)
                Tags.Clear();
            foreach (var item in lines)
            {
                Match match = r.Match(item);
                if (match.Success)
                {
                    string tagName = match.Groups[1].Value;
                    tagName = tagName.Replace('_', ' ');
                    tagName = tagName.Replace("(", "\\(");
                    tagName = tagName.Replace(")", "\\)");
                    string[] aliases = match.Groups[4].Value.Replace("\"", "").Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                    AddTag(tagName, Convert.ToInt32(match.Groups[3].Value));
                    foreach (var al in aliases)
                    {
                        AddTag(al, Convert.ToInt32(match.Groups[3].Value), true, tagName);
                    }
                }
            }
        }

        private void AddTag(string tag, int count, bool isAlias = false, string parent = null)
        {
            if (Tags.Exists(a => a.Parent == tag))
                return;
            tag = tag.Trim().ToLower();
            int existTagIndex = Tags.FindIndex(a => a.Tag == tag);
            TagItem tagItem = null;
            if (existTagIndex != -1)
            {
                tagItem = Tags[existTagIndex];
                tagItem.Count += count;
            }
            else
            {
                tagItem = new TagItem();
                tagItem.Tag = tag;
                tagItem.Count = count;
            }
            tagItem.IsAlias = isAlias;
            tagItem.Parent = parent;
            Tags.Add(tagItem);
        }

        public bool IsNeedUpdate(string dirToCheck)
        {
            FileInfo[] csvFiles = new DirectoryInfo(dirToCheck).GetFiles("*.csv", SearchOption.TopDirectoryOnly);
            if (LoadedFiles.Count != csvFiles.Length)
                return true;
            foreach (var item in csvFiles)
            {
                byte[] data = File.ReadAllBytes(item.FullName);
                long hash = Adler32.GenerateHash(data);
                if (!LoadedFiles.ContainsKey(item.Name))
                    return true;
                if(LoadedFiles[item.Name]!=hash) 
                    return true;
            }
            return false;
        }

        public void LoadTranslation(string fName)
        {
            if (!File.Exists(fName))
            {
                var sw = File.CreateText(fName);
                sw.WriteLine("//Translation format: <original>=<translation>");
                sw.Dispose();
                return;
            }
            //FileInfo[] txtFiles = new DirectoryInfo(dir).GetFiles("*.txt", SearchOption.TopDirectoryOnly);
            Dictionary<string, string> allTranslations = new Dictionary<string, string>();
            string[] lines = File.ReadAllLines(fName);
            foreach (var item in lines)
            {
                if (item.Trim().StartsWith("//"))
                    continue;
                int index = item.LastIndexOf('=');
                if (index == -1)
                    continue;
                string orig = item.Substring(0, index).Trim();
                string trans = item.Substring(index + 1).Trim();
                if (!allTranslations.ContainsKey(orig))
                    allTranslations[orig] = trans;
            }
            //foreach (var txt in txtFiles)
            //{
            //    string[] lines = File.ReadAllLines(txt.FullName);
            //    foreach (var item in lines)
            //    {
            //        int index = item.LastIndexOf('=');
            //        if (index == -1) 
            //            continue;
            //        string orig = item.Substring(0, index);
            //        string trans = item.Substring(index + 1);
            //        if (!allTranslations.ContainsKey(orig))
            //            allTranslations[orig] = trans;
            //    }
            //}
            foreach (var tag in Tags)
            {
                if(allTranslations.ContainsKey(tag.Tag))
                    tag.Translation = allTranslations[tag.Tag];
            }
        }

        public static TagsDB LoadFromTagFile(string fPath)
        {
            if (File.Exists(fPath))
            {
                return (TagsDB)Translator.IO.Extensions.LoadDataSet(File.ReadAllBytes(fPath));
            }
            else
                return new TagsDB();
        }

        public void SaveTags(string fPath)
        {
            Translator.IO.Extensions.SaveDataSet(this, fPath);
        }

        [Serializable]
        public class TagItem
        {
            public string Tag;
            public int Count;
            //public List<string> Aliases;
            public bool IsAlias;
            public string Parent;

            public string Translation;

            public TagItem()
            {
                //Aliases = new List<string>();
            }

            public string GetTag()
            {
                if (IsAlias)
                    return Parent;
                else
                    return Tag;
            }

            public override string ToString()
            {
                if (IsAlias)
                    return $"{Tag} -> {Parent}";
                else
                    return Tag;
            }
        }

    }
}
