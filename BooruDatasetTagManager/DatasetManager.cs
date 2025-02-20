﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BooruDatasetTagManager
{
    public class DatasetManager
    {
        public ConcurrentDictionary<string, DataItem> DataSet;
        public ImageList Images;
        public List<TagValue> AllTags;
        public List<TagValue> CommonTags;

        private int originalHash;
        public DatasetManager()
        {
            DataSet = new ConcurrentDictionary<string, DataItem>();
            AllTags = new List<TagValue>();
            CommonTags = new List<TagValue>();
        }

        public void SaveAll()
        {
            foreach (var item in DataSet)
            {
                File.WriteAllText(item.Value.TextFilePath, string.Join(", ", item.Value.Tags));
            }
        }

        public void UpdateData()
        {
            AllTags = DataSet
                .SelectMany(x => x.Value.Tags)
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new TagValue(x))
                .ToList();
            CommonTags = DataSet
                .Skip(1).Aggregate(
                    new HashSet<string>(DataSet.First().Value.Tags),
                    (h, e) => { h.IntersectWith(e.Value.Tags); return h; }
                )
                .OrderBy(x => x)
                .Select(x => new TagValue(x))
                .ToList();
        }

        public void AddTagToAll(string tag, AddingType addType, int pos=-1)
        {
            tag = tag.ToLower();
            foreach (var item in DataSet)
            {
                if (item.Value.Tags.Contains(tag))
                {
                    item.Value.Tags.Remove(tag);
                }
                switch (addType)
                {
                    case AddingType.Top:
                        {
                            item.Value.Tags.Insert(0, tag);
                            break;
                        }
                    case AddingType.Center:
                        {
                            item.Value.Tags.Insert(item.Value.Tags.Count / 2, tag);
                            break;
                        }
                    case AddingType.Down:
                        {
                            item.Value.Tags.Add(tag);
                            break;
                        }
                    case AddingType.Custom:
                        {
                            if (pos >= item.Value.Tags.Count)
                            {
                                item.Value.Tags.Add(tag);
                            }
                            else if (pos < 0)
                            {
                                item.Value.Tags.Insert(0, tag);
                            }
                            else
                                item.Value.Tags.Insert(pos, tag);
                            break;
                        }
                }
            }
        }

        public void SetTagListToAll(List<string> tags, bool onlyEmpty)
        {
            foreach (var item in DataSet)
            {
                if (onlyEmpty)
                {
                    if (item.Value.Tags.Count == 0)
                    {
                        item.Value.Tags.AddRange(tags);
                    }
                }
                else
                {
                    item.Value.Tags.Clear();
                    item.Value.Tags.AddRange(tags);
                }
            }
        }

        public void DeleteTagFromAll(string tag)
        {
            tag = tag.ToLower();
            foreach (var item in DataSet)
            {
                if (item.Value.Tags.Contains(tag))
                    item.Value.Tags.Remove(tag);
            }
        }

        public void ReplaceTagInAll(string srcTag, string dstTag)
        {
            srcTag = srcTag.ToLower();
            dstTag = dstTag.ToLower();
            foreach (var item in DataSet)
            {
                int index = item.Value.Tags.IndexOf(srcTag);
                if (index != -1)
                {
                    int dstIndex = item.Value.Tags.IndexOf(dstTag);
                    if (dstIndex == -1)
                        item.Value.Tags[index] = dstTag;
                    else
                    {
                        item.Value.Tags.RemoveAt(index);
                    }
                }
            }
        }
        public List<string> FindTag(string tag)
        {
            List<string> foundedTags = new List<string>();
            foreach (var item in DataSet)
            {
                if (item.Value.Tags.Contains(tag.ToLower()))
                    foundedTags.Add(item.Key);
            }
            return foundedTags;
        }

        private List<TagValue> GetTagsForDel(List<TagValue> checkedList, List<string> srcList)
        {
            List<TagValue> delList = new List<TagValue>();
            foreach (var item in checkedList)
            {
                if (!srcList.Contains(item.Tag))
                    delList.Add(item);
            }
            return delList;
        }

        public void LoadFromFolder(string folder)
        {
            List<string> imagesExt = new List<string>() { ".jpg", ".png", ".bmp", ".jpeg" };
            string[] imgs = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);

            imgs = imgs.Where(a => imagesExt.Contains(Path.GetExtension(a).ToLower())).ToArray();

            imgs.AsParallel().ForAll(x =>
            {
                var dt = new DataItem(x);
                DataSet.TryAdd(dt.Name, dt);
            });
            Images = GetImageList(130, 130);
            UpdateDatasetHash();
        }

        private ImageList GetImageList(int w, int h)
        {
            ImageList imgList = new ImageList();
            imgList.ImageSize = new Size(w, h);
            foreach (var item in DataSet)
            {
                imgList.Images.Add(item.Key, item.Value.Img);
            }
            return imgList;
        }

        public void UpdateImageList(int w, int h)
        {
            Images.Images.Clear();
            Images = GetImageList(w, h);
        }

        public bool IsDataSetChanged()
        {
            return !originalHash.Equals(GetHashCode());
        }

        public void UpdateDatasetHash()
        {
            originalHash = GetHashCode();
        }

        public void LoadLossFromFile(string fPath)
        {
            string lossStatPrefix = "Loss statistics for file ";
            string lossPrefix = "loss";
            string lossPattern = "loss:([0-9]*[.]?[0-9]+)±";
            string lastlossPrefix = "recent";
            string lastLossPattern = "recent \\d+ loss:([0-9]*[.]?[0-9]+)±";

            string[] lines = File.ReadAllLines(fPath);
            for (int i = 0; i+2 < lines.Length; i++)
            {
                if (lines[i].StartsWith(lossStatPrefix))
                {
                    string fName = Path.GetFileNameWithoutExtension(lines[i].Replace(lossStatPrefix, ""));
                    if (lines[i + 1].StartsWith(lossPrefix))
                    {
                        var m1 = Regex.Match(lines[i + 1], lossPattern, RegexOptions.IgnoreCase);
                        if (m1.Success)
                        {
                            float loss = (float)Convert.ToDouble(m1.Groups[1].Value.Replace('.', ','));

                            if (lines[i + 2].StartsWith(lastlossPrefix))
                            {
                                var m2 = Regex.Match(lines[i + 2], lastLossPattern, RegexOptions.IgnoreCase);
                                if (m2.Success)
                                {
                                    float lastLoss = (float)Convert.ToDouble(m2.Groups[1].Value.Replace('.', ','));
                                    if (DataSet.ContainsKey(fName))
                                    {
                                        DataSet[fName].Loss = loss;
                                        DataSet[fName].LastLoss = lastLoss;
                                        i += 2;
                                    }
                                    else
                                        continue;
                                }
                                else
                                    continue;
                            }
                            else
                                continue;
                        }
                        else
                            continue;
                    }
                    else
                        continue;
                }
                else
                    continue;
            }
        }

        public override int GetHashCode()
        {
            int result = 0;
            unchecked
            {
                foreach (var item in DataSet)
                    result = result * 31 + item.Value.GetHashCode();
            }
            return result;
        }


        public enum AddingType
        {
            Top,
            Center,
            Down,
            Custom
        }


        public class DataItem
        {
            public string Name { get; set; }
            public Image Img { get; set; }
            public List<string> Tags { get; set; }
            public string TextFilePath { get; set; }
            public string ImageFilePath { get; set; }

            public float Loss { get; set; }
            public float LastLoss { get; set; }

            public DataItem()
            {
                Tags = new List<string>();
                Loss = -1;
                LastLoss = -1;
            }

            public DataItem(string imagePath)
            {
                Tags = new List<string>();
                ImageFilePath = imagePath;
                Name = Path.GetFileNameWithoutExtension(imagePath);
                TextFilePath = Path.Combine(Path.GetDirectoryName(imagePath), Name + ".txt");
                GetTagsFromFile();
                Img = MakeThumb(imagePath);
            }

            Image MakeThumb(string imagePath)
            {
                using (var img = Image.FromFile(imagePath))
                {
                    var aspect = img.Width / (float)img.Height;

                    int newHeight = img.Height * 130 / img.Width;
                    int newWidth = 130;

                    if (newHeight > 130)
                    {
                        newWidth = img.Width * 130 / img.Height;
                        newHeight = 130;
                    }

                    return img.GetThumbnailImage(newWidth, newHeight, () => false, IntPtr.Zero);
                }
            }

            public void GetTagsFromFile()
            {
                if (File.Exists(TextFilePath))
                {
                    string text = File.ReadAllText(TextFilePath);
                    Tags = text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    for (int i = 0; i < Tags.Count; i++)
                        Tags[i] = Tags[i].Trim();
                }
                else
                {
                    Tags = new List<string>();
                }

            }

            public override string ToString()
            {
                return String.Join(", ", Tags);
            }

            public override int GetHashCode()
            {
                return ToString().GetHashCode();
            }
        }
    }


}
