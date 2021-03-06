﻿using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace MoeLoader.Core.Sites
{
    /// <summary>
    /// yuriimg.com Last change 20180924
    /// </summary>
    public class YuriimgSite : MoeSite
    {
        private const string User = "mluser1";
        private const string Pass = "ml1yuri";

        public override string HomeUrl => "http://yuriimg.com";
        public override string ShortName => "yuriimg";
        public override string DisplayName => "Yuriimg";

        public YuriimgSite()
        {
            SurpportState.IsSupportAutoHint = false;
            SurpportState.IsSupportRating = false;
            
            DownloadTypes.Add("原图", 4);
            DownloadTypes.Add("预览图", 2);
        }

        private bool IsLogin { get; set; }

        public override async Task<ImageItems> GetRealPageImagesAsync(SearchPara para, CancellationToken token)
        {
            if (!IsLogin)
            {
                Net = new NetSwap(Settings, HomeUrl);
                const string loginUrl = "http://yuriimg.com/account/login";

                string
                    boundary = $"---------------{DateTime.Now.Ticks:x}", //开始边界符
                    pboundary = $"--{boundary}", //分隔边界符
                    endBoundary = $"--{boundary}--\r\n", // 结束边界符
                    postData = $"{pboundary}\r\nContent-Disposition: form-data; name=\"username\"\r\n\r\n{User}\r\n{pboundary}\r\nContent-Disposition: form-data; name=\"password\"\r\n\r\n{Pass}\r\n{endBoundary}";

                var respose = await Net.Client.PostAsync(loginUrl, new StringContent(postData), token);
                if (respose.IsSuccessStatusCode) IsLogin = true;
                else return new ImageItems();
                //retData = Sweb.Post(loginUrl, postData, Settings.Proxy, shc);
                //cookie = Sweb.GetURLCookies(HomeUrl);

                //if (retData.Contains("-2"))
                //{
                //    throw new Exception("密码错误");
                //}
                //else if ((!cookie.Contains("otome_")))
                //{
                //    throw new Exception("登录时出错");
                //}
            }

            var url = $"{HomeUrl}/post/{para.PageIndex}.html";
            // string url = "http://yuriimg.com/show/ge407xd5o.jpg";

            if (para.Keyword.Length > 0)
            {
                //http://yuriimg.com/search/index/tags/?/p/?.html
                url = $"{HomeUrl}/search/index/tags/{para.Keyword}/p/{para.PageIndex}.html";
            }

            var pageSource = await Net.Client.GetAsync(url, token);
            var pageStr = await pageSource.Content.ReadAsStringAsync();

            var list = new ImageItems();

            var dococument = new HtmlDocument();
            dococument.LoadHtml(pageStr);
            var imageItems = dococument.DocumentNode.SelectNodes("//*[@class='image-list cl']");
            if (imageItems == null)
            {
                return list;
            }
            foreach (var imageItem in imageItems)
            {
                var imgNode = imageItem.SelectSingleNode("./div[1]/img");
                var tags = imgNode.Attributes["alt"].Value;
                var img = new ImageItem(this,para);
                img.Height = Convert.ToInt32(imageItem.SelectSingleNode(".//div[@class='image']").Attributes["data-height"].Value);
                img.Width = Convert.ToInt32(imageItem.SelectSingleNode(".//div[@class='image']").Attributes["data-width"].Value);
                img.Author = imageItem.SelectSingleNode("//small/a").InnerText;
                img.Description = tags;
                img.Id = StringToInt(imgNode.Attributes["id"].Value);
                img.DetailUrl = HomeUrl + imgNode.Attributes["data-href"].Value;
                img.Score = Convert.ToInt32(imageItem.SelectSingleNode(".//span[@class='num']").InnerText);
                img.Site = this;
                img.Net = null;
                img.Urls.Add(new UrlInfo("缩略图", 1, imgNode.Attributes["data-original"].Value.Replace("!single", "!320px"), HomeUrl));
                foreach (var tag in tags.Split(' '))
                {
                    //if (tag.Contains("画师：")) continue;
                    if(!string.IsNullOrWhiteSpace(tag)) img.Tags.Add(tag.Trim());
                }

                img.GetDetailAction = async () =>
                {
                    try
                    {
                        var html = await Net.Client.GetStringAsync(img.DetailUrl);

                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);
                        var showIndexs = doc.DocumentNode.SelectSingleNode("//div[@class='logo']");
                        var imgDownNode = showIndexs.SelectSingleNode("//div[@class='img-control']");
                        var nodeHtml = showIndexs.OuterHtml;
                        img.Date = TimeConvert(nodeHtml);

                        img.Source = nodeHtml.Contains("pixiv page") ?
                            showIndexs.SelectSingleNode(".//a[@target='_blank']").Attributes["href"].Value :
                            Regex.Match(nodeHtml, @"(?<=源地址).*?(?=</p>)").Value.Trim();
                        img.Urls.Add(new UrlInfo("缩略图", 1, doc.DocumentNode.SelectSingleNode("//figure[@class=\'show-image\']/img").Attributes["src"].Value, HomeUrl));
                        var previww = doc.DocumentNode.SelectSingleNode("//figure[@class=\'show-image\']/img").Attributes["src"].Value;
                        string file;
                        if (Regex.Matches(imgDownNode.OuterHtml, "href").Count > 1)
                        {
                            file = HomeUrl + imgDownNode.SelectSingleNode("./a[1]").Attributes["href"].Value;
                            //item.FileSize = Regex.Match(imgDownNode.SelectSingleNode("./a[1]").InnerText, @"(?<=().*?(?=))").Value;
                        }
                        else
                        {
                            file = HomeUrl + imgDownNode.SelectSingleNode("./a").Attributes["href"].Value;
                            //item.FileSize = Regex.Match(imgDownNode.SelectSingleNode("./a").InnerText, @"(?<=().*?(?=))").Value;
                        }
                        img.Urls.Add(new UrlInfo("原图", 4, file, HomeUrl));
                        img.Urls.Add(new UrlInfo("预览图", 2, previww.Length > 0 ? previww : file, HomeUrl));
                    }
                    catch (Exception ex)
                    {
                        App.Log(ex);
                    }
                    
                };

                list.Add(img);
                   
            }
            token.ThrowIfCancellationRequested();
            return list;

        }

        private int StringToInt(string id)
        {
            var str = id.Trim();                            // 去掉字符串首尾处的空格
            var charBuf = str.ToArray();                    // 将字符串转换为字符数组
            var charToASCII = new ASCIIEncoding();
            var TxdBuf = new byte[charBuf.Length];          // 定义发送缓冲区；
            TxdBuf = charToASCII.GetBytes(charBuf);
            var idOut = BitConverter.ToInt32(TxdBuf, 0);
            return idOut;
        }

        private string TimeConvert(string html)
        {
            var date = Regex.Match(html, @"(?<=<span>).*?(?=</span>)").Value;
            if (date.Contains("时前"))
            {
                date = DateTime.Now.AddHours(-Convert.ToDouble(Regex.Match(date, @"\d+").Value)).ToString("yyyy-MM-dd hh.mm");
            }
            else if (date.Contains("天前"))
            {
                date = DateTime.Now.AddDays(-Convert.ToDouble(Regex.Match(date, @"\d+").Value)).ToString("yyyy-MM-dd hh.mm");
            }
            else if (date.Contains("月前"))
            {
                date = DateTime.Now.AddMonths(-Convert.ToInt32(Regex.Match(date, @"\d+").Value)).ToString("yyyy-MM-dd hh.mm");
            }
            return date;
        }
    }
}
