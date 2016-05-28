using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Collections.Generic;
using System.Management.Automation;

namespace AnimetickNotificator
{
    class Program
    {
        private static string URL = "http://animetick.net/ticket/list/0.json";

        static void Main()
        {
            var count = 1;
            var data = GetAnimeData(1);
            {
                var text = data.Aggregate("", (current, anime) => current + $"{anime.Item2.Hour}:{anime.Item2.Minute + 1}\t{anime.Item1}\n");
                ToastNotification($"[Animetick Notificator]\n{text}");
            }

            while (true)
            {
                if (count == 0)
                {
                    data = GetAnimeData(1);
                    var text = data.Aggregate("", (current, anime) => current + $"{anime.Item2.Hour}:{anime.Item2.Minute + 1}\t{anime.Item1}\n");
                    ToastNotification($"[Animetick Notificator]\n{text}");
                }

                var now = DateTime.Now;
                foreach (var anime in data)
                {
                    if (anime.Item2.Day == now.Day && anime.Item2.Hour == now.Hour && anime.Item2.Minute == now.Minute)
                    {
                        ToastNotification($"[Animetick Notificator]\n" +
                                          $"{anime.Item2.Hour}から{anime.Item2.Minute + 1}\t{anime.Item1}が始まります");
                    }
                }

                count++;
                if (count == 2 * 30)
                {
                    count = 0;
                }
                Thread.Sleep(30 * 1000);
            }
        }

        private static void ToastNotification(string text)
        {
            //コマンドライン引数をトースト通知として表示します
            var code = new string[]
            {
            "$ErrorActionPreference = \"Stop\"",
            $"$notificationTitle = \"{text}\"",
            "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null",
            "$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)",
            "$toastXml = [xml] $template.GetXml()",
            "$toastXml.GetElementsByTagName(\"text\").AppendChild($toastXml.CreateTextNode($notificationTitle)) > $null",
            "$xml = New-Object Windows.Data.Xml.Dom.XmlDocument",
            "$xml.LoadXml($toastXml.OuterXml)",
            "$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)",
            "$toast.Tag = \"PowerShell\"",
            "$toast.Group = \"PowerShell\"",
            "$toast.ExpirationTime = [DateTimeOffset]::Now.AddMinutes(5)",
            "$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier(\"PowerShell\")",
            "$notifier.Show($toast);"
            };

            using (var invoker = new RunspaceInvoke())
            {
                invoker.Invoke(string.Join("\n", code), new object[] { });
            }
        }

        /// <summary>
        /// アニメデータの生成
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static List<Tuple<string, DateTime>> GetAnimeData(int t)
        {
            var json = JsonConvert.DeserializeObject<RootObject>(GetJson(URL));
            var today = DateTime.Now.Date.ToString().Split(' ')[0].Replace('/', '-');
            var data = new List<Tuple<string, DateTime>>();
            foreach (var list in json.list)
            {
                if (list.start_at.StartsWith(today))
                {
                    // アニメ放送のt分前の時刻にする
                    data.Add(new Tuple<string, DateTime>(list.title, DateTime.ParseExact(list.start_at.Replace(today + "T", "").Replace("+09:00", ""), "HH:mm:ss", null).AddMinutes((-1) * t)));
                }
            }
            return data;
        }

        /// <summary>
        /// Animetickからアニメデータを取得
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static string GetJson(string url)
        {
            var webreq = (HttpWebRequest)WebRequest.Create(url);
            webreq.CookieContainer = new CookieContainer();
            string cookie;
            using (var sw = new StreamReader("cookie.log", Encoding.UTF8))
            {
                cookie = sw.ReadToEnd().Trim();
            }
            webreq.CookieContainer.Add(new Uri("http://animetick.net"), new Cookie("_animetick_session", cookie));

            var webres = (HttpWebResponse)webreq.GetResponse();
            var st = webres.GetResponseStream();
            var sr = new StreamReader(st, Encoding.UTF8);

            var json = sr.ReadToEnd();
            sr.Close();

            return json;
        }
    }

    public class List
    {
        public int title_id { get; set; }
        public int count { get; set; }
        public object updated_at { get; set; }
        public bool watched { get; set; }
        public string title { get; set; }
        public string icon_path { get; set; }
        public string sub_title { get; set; }
        public string start_at { get; set; }
        public string end_at { get; set; }
        public List<object> flags { get; set; }
        public string ch_name { get; set; }
        public int ch_number { get; set; }
    }

    public class RootObject
    {
        public List<List> list { get; set; }
        public bool last_flag { get; set; }
    }
}
