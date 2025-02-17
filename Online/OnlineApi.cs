﻿using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Web;
using System;
using IO = System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Shared.Engine.CORE;
using System.IO;
using Shared.Model.Online;
using Shared.Model.Base;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers
{
    public class OnlineApiController : BaseController
    {
        static OnlineApiController()
        {
            Directory.CreateDirectory("cache/externalids");
        }

        #region online.js
        [HttpGet]
        [Route("online.js")]
        public ActionResult Online()
        {
            if (!memoryCache.TryGetValue("ApiController:online.js", out string file))
            {
                file = IO.File.ReadAllText("plugins/online.js");
                memoryCache.Set("ApiController:online.js", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("http://127.0.0.1:9118", host);
            file = file.Replace("{localhost}", host);

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region lite.js
        [HttpGet]
        [Route("lite.js")]
        async public Task<ActionResult> Lite()
        {
            if (!memoryCache.TryGetValue("ApiController:lite.js", out string file))
            {
                file = await IO.File.ReadAllTextAsync("plugins/lite.js");
                memoryCache.Set("ApiController:lite.js", file, DateTime.Now.AddMinutes(5));
            }

            return Content(file.Replace("{localhost}", $"{host}/lite"), contentType: "application/javascript; charset=utf-8");
        }
        #endregion


        #region externalids
        static Dictionary<string, string> externalids = new Dictionary<string, string>();

        [Route("externalids")]
        async public Task<ActionResult> Externalids(long id, string imdb_id, long kinopoisk_id, int serial)
        {
            if (id == 0)
                return Json(new { });

            if (IO.File.Exists("cache/externalids/master.json"))
            {
                try
                {
                    externalids = JsonConvert.DeserializeObject<Dictionary<string, string>>(IO.File.ReadAllText("cache/externalids/master.json"));
                }
                catch { externalids = new Dictionary<string, string>(); }
            }

            #region getAlloha / getVSDN / getTabus
            async Task<string> getAlloha(string imdb)
            {
                var proxyManager = new ProxyManager("alloha", AppInit.conf.Alloha);
                string json = await HttpClient.Get("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1&imdb=" + imdb, timeoutSeconds: 4, proxy: proxyManager.Get());
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"id_kp\":([0-9]+),").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async Task<string> getVSDN(string imdb)
            {
                var proxyManager = new ProxyManager("vcdn", AppInit.conf.VCDN);
                string json = await HttpClient.Get("https://videocdn.tv/api/short?api_token=3i40G5TSECmLF77oAqnEgbx61ZWaOYaE&imdb_id=" + imdb, timeoutSeconds: 4, proxy: proxyManager.Get());
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"kp_id\":\"?([0-9]+)\"?").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async Task<string> getTabus(string imdb)
            {
                var proxyManager = new ProxyManager("collaps", AppInit.conf.Collaps);
                string json = await HttpClient.Get("https://api.bhcesh.me/franchise/details?token=eedefb541aeba871dcfc756e6b31c02e&imdb_id=" + imdb.Remove(0, 2), timeoutSeconds: 4, proxy: proxyManager.Get());
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"kinopoisk_id\":\"?([0-9]+)\"?").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }
            #endregion

            #region get imdb_id
            if (string.IsNullOrWhiteSpace(imdb_id))
            {
                string path = $"cache/externalids/{id}";
                if (IO.File.Exists(path))
                {
                    imdb_id = IO.File.ReadAllText(path);
                }
                else
                {
                    string mkey = $"externalids:locktmdb:{serial}:{id}";
                    if (!memoryCache.TryGetValue(mkey, out _))
                    {
                        memoryCache.Set(mkey, 0 , DateTime.Now.AddHours(1));

                        string cat = serial == 1 ? "tv" : "movie";
                        string json = await HttpClient.Get($"https://api.themoviedb.org/3/{cat}/{id}?api_key=4ef0d7355d9ffb5151e987764708ce96&append_to_response=external_ids", timeoutSeconds: 6);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            imdb_id = Regex.Match(json, "\"imdb_id\":\"(tt[0-9]+)\"").Groups[1].Value;
                            if (!string.IsNullOrWhiteSpace(imdb_id))
                                IO.File.WriteAllText(path, imdb_id);
                        }
                    }
                }
            }
            #endregion

            #region get kinopoisk_id
            string kpid = null;

            if (!string.IsNullOrWhiteSpace(imdb_id))
            {
                externalids.TryGetValue(imdb_id, out kpid);

                if (string.IsNullOrEmpty(kpid))
                { 
                    string path = $"cache/externalids/{imdb_id}";
                    if (IO.File.Exists(path))
                    {
                        kpid = IO.File.ReadAllText(path);
                        externalids.TryAdd(imdb_id, kpid);
                    }
                    else if (kinopoisk_id == 0)
                    {
                        string mkey = $"externalids:lockkpid:{imdb_id}";
                        if (!memoryCache.TryGetValue(mkey, out _))
                        {
                            memoryCache.Set(mkey, 0, DateTime.Now.AddDays(1));

                            switch (AppInit.conf.online.findkp ?? "all")
                            {
                                case "alloha":
                                    kpid = await getAlloha(imdb_id);
                                    break;
                                case "vsdn":
                                    kpid = await getVSDN(imdb_id);
                                    break;
                                case "tabus":
                                    kpid = await getTabus(imdb_id);
                                    break;
                                default:
                                    {
                                        var tasks = new Task<string>[] { getVSDN(imdb_id), getAlloha(imdb_id), getTabus(imdb_id) };
                                        await Task.WhenAll(tasks);

                                        kpid = tasks[0].Result ?? tasks[1].Result ?? tasks[2].Result;
                                        break;
                                    }
                            }

                            if (!string.IsNullOrEmpty(kpid))
                            {
                                externalids.TryAdd(imdb_id, kpid);
                                IO.File.WriteAllText(path, kpid);
                            }
                        }
                    }
                }
            }
            #endregion

            return Json(new { imdb_id, kinopoisk_id = (kpid != null ? kpid : kinopoisk_id > 0 ? kinopoisk_id.ToString() : null) });
        }
        #endregion

        #region events
        [HttpGet]
        [Route("lifeevents")]
        public ActionResult LifeEvents(long id, string imdb_id, long kinopoisk_id, int serial, string source)
        {
            string json = null;
            JsonResult error(string msg) => Json(new { accsdb = true, ready = true, online = new string[] { }, msg });

            if (memoryCache.TryGetValue(checkOnlineSearchKey(id, source), out (bool ready, int tasks, string online) res))
            {
                if (res.ready && (res.online == null || !res.online.Contains("\"show\":true")))
                {
                    if (string.IsNullOrEmpty(imdb_id) && 0 >= kinopoisk_id)
                        return error($"Добавьте \"IMDB ID\" {(serial == 1 ? "сериала" : "фильма")} на https://themoviedb.org/{(serial == 1 ? "tv" : "movie")}/{id}/edit?active_nav_item=external_ids");

                    return error($"Не удалось найти онлайн для {(serial == 1 ? "сериала" : "фильма")}");
                }

                string online = res.online?.Replace("{localhost}", $"{host}/lite") ?? string.Empty;
                json = "{"+ $"\"ready\":{res.ready.ToString().ToLower()},\"tasks\":{res.tasks},\"online\":[{Regex.Replace(online, ",$", "")}]" + "}";
            }

            return Content(json ?? "{\"ready\":false,\"tasks\":0,\"online\":[]}", contentType: "application/javascript; charset=utf-8");
        }


        [HttpGet]
        [Route("lite/events")]
        async public Task<ActionResult> Events(long id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, int year, string source, int serial = -1, bool life = false, string account_email = null)
        {
            string online = string.Empty;
            bool isanime = original_language == "ja";

            var conf = AppInit.conf;

            if (AppInit.modules != null)
            {
                foreach (var item in AppInit.modules)
                {
                    if (item.online.enable)
                    {
                        try
                        {
                            if (item.assembly.GetType(item.online.@namespace) is Type t && t.GetMethod("Events") is MethodInfo m)
                            {
                                string result = (string)m.Invoke(null, new object[] { host, account_email, id, imdb_id, kinopoisk_id, title, original_title, original_language, year, source, serial });
                                if (!string.IsNullOrWhiteSpace(result))
                                    online += result;
                            }
                        }
                        catch { }
                    }
                }
            }

            void send(string name, BaseSettings init, string plugin = null, string arg_title = null, string arg_url = null)
            {
                if (init.enable && !init.rip)
                {
                    string url = init.overridehost;
                    if (string.IsNullOrEmpty(url))
                        url = "{localhost}/" + (plugin ?? name.ToLower()) + arg_url;

                    online += "{\"name\":\"" + $"{init.displayname ?? name}{arg_title}" + "\",\"url\":\"" + url + "\"},";
                }
            }

            if (original_language is "ja" or "ko" or "zh")
                send("Kodik", conf.Kodik);

            if (serial == -1 || isanime)
            {
                send("Anilibria", conf.AnilibriaOnline);
                send("Animevost", conf.Animevost);
                send("Animebesst", conf.Animebesst);
                send("AnimeGo", conf.AnimeGo);
                send("AniMedia", conf.AniMedia);
            }

            if ((serial == -1 || serial == 0) && kinopoisk_id > 0)
                send("VoKino", conf.VoKino);

            send("Filmix", conf.Filmix, arg_url: (source == "filmix" ? $"?postid={id}" : ""));
            send("KinoPub", conf.KinoPub, arg_url: (source == "pub" ? $"?postid={id}" : ""));
            send("Filmix", conf.FilmixPartner, "fxapi", arg_url: (source == "filmix" ? $"?postid={id}" : ""));

            send("Alloha", conf.Alloha);
            send("Rezka", conf.Rezka);

            if (kinopoisk_id > 0)
                send("Zetflix", conf.Zetflix);

            send("VideoCDN", conf.VCDN, "vcdn");

            send("Kinobase", conf.Kinobase);

            if (serial == -1 || serial == 0)
                send("iRemux", conf.iRemux, "remux");

            send("Voidboost", conf.Voidboost);
            send("Ashdi (UKR)", conf.Ashdi, "ashdi");
            send("Eneyida (UKR)", conf.Eneyida, "eneyida");

            if (kinopoisk_id > 0)
                send("VideoDB", conf.VideoDB);

            if (serial == -1 || serial == 1)
                send("Seasonvar", conf.Seasonvar);

            if (serial == -1 || serial == 1)
                send("LostfilmHD", conf.Lostfilmhd);

            send("Collaps", conf.Collaps);
            send("HDVB", conf.HDVB);

            if (serial == -1 || serial == 0)
                send("Redheadsound", conf.Redheadsound);

            send("Kinotochka", conf.Kinotochka);

            if ((serial == -1 || (serial == 1 && !isanime)) && kinopoisk_id > 0)
                send("CDNmovies", conf.CDNmovies);

            if (serial == -1 || serial == 0)
                send("IframeVideo", conf.IframeVideo);

            if (!life && conf.litejac)
                online += "{\"name\":\"Jackett\",\"url\":\"{localhost}/jac\"},";

            #region checkOnlineSearch
            bool chos = conf.online.checkOnlineSearch && id > 0;

            if (chos && IO.File.Exists("isdocker"))
            {
                if ((await HttpClient.Get($"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/version", timeoutSeconds: 4)) != appversion)
                    chos = false;
            }

            if (chos)
            {
                string memkey = checkOnlineSearchKey(id, source);
                if (!memoryCache.TryGetValue(memkey, out (bool ready, int tasks, string online) cache) || !conf.multiaccess)
                {
                    memoryCache.Set(memkey, cache, DateTime.Now.AddSeconds(15));

                    var tasks = new List<Task>();
                    var links = new ConcurrentBag<(string code, int index, bool work)>();

                    var match = Regex.Match(online, "\\{\"name\":\"([^\"]+)\",\"url\":\"(\\{localhost\\}|https?://[^/]+/lite)/([^\"]+)\"\\},");
                    while (match.Success)
                    {
                        string _name = match.Groups[1].Value;
                        string _serv = match.Groups[2].Value;
                        string _plugin = match.Groups[3].Value;

                        if (!string.IsNullOrWhiteSpace(_name) && !string.IsNullOrWhiteSpace(_plugin))
                            tasks.Add(checkSearch(links, tasks, tasks.Count, _name, _plugin, _serv, id, imdb_id, kinopoisk_id, title, original_title, original_language, source, year, serial, life));

                        match = match.NextMatch();
                    }

                    if (life)
                        return Content("{\"life\":true}", contentType: "application/javascript; charset=utf-8");

                    await Task.WhenAll(tasks);

                    cache.ready = true;
                    cache.tasks = tasks.Count;
                    cache.online = string.Join("", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code));

                    memoryCache.Set(memkey, cache, DateTime.Now.AddMinutes(10));
                }

                if (life)
                    return Content("{\"life\":true}", contentType: "application/javascript; charset=utf-8");

                online = cache.online;
            }
            #endregion

            return Content($"[{Regex.Replace(online, ",$", "").Replace("{localhost}", $"{host}/lite")}]", contentType: "application/javascript; charset=utf-8");
        }
        #endregion


        #region checkSearch
        static string checkOnlineSearchKey(long id, string source) => $"ApiController:checkOnlineSearch:{id}:{source?.Replace("tmdb", "")?.Replace("cub", "")}";

        async Task checkSearch(ConcurrentBag<(string code, int index, bool work)> links, List<Task> tasks, int index, string name, string uri, string serv,
                               long id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, string source, int year, int serial, bool life)
        {
            bool isLocalhost = serv == "{localhost}";
            string srq = isLocalhost ? $"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/lite" : serv;
            var header = isLocalhost ? HeadersModel.Init("xhost", host) : null;

            string res = await HttpClient.Get($"{srq}/{uri}{(uri.Contains("?") ? "&" : "?")}id={id}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&original_language={original_language}&source={source}&year={year}&serial={serial}&checksearch=true", timeoutSeconds: 10, headers: header);

            bool work = !string.IsNullOrWhiteSpace(res) && res.Contains("data-json=");

            string quality = string.Empty;
            string balanser = uri.Split("?")[0];

            #region определение качества
            if (work && life)
            {
                if (serial == -1 || serial == 0)
                {
                    foreach (string q in new string[] { "2160", "1080", "720", "480", "360" })
                    {
                        if (res.Contains($"\"{q}p\"") || res.Contains($">{q}p<") || res.Contains($"<!--{q}p-->"))
                        {
                            if (q == "2160")
                            {
                                if (balanser == "kinopub")
                                    quality = " - 4K HDR";
                                else
                                {
                                    quality = res.Contains("HDR") ? " - 4K HDR" : " - 4K";
                                }
                            }
                            else
                            {
                                quality = $" - {q}p";
                            }

                            break;
                        }
                    }
                }

                if (quality == string.Empty && balanser == "vokino")
                    quality = res.Contains("4K HDR") ? " - 4K HDR" : res.Contains("4K ") ? " - 4K" : string.Empty;

                if (quality == string.Empty)
                {
                    switch (balanser)
                    {
                        case "fxapi":
                        case "kinopub":
                        case "vokino":
                        case "rezka":
                        case "alloha":
                        case "remux":
                        case "ashdi":
                            quality = " ~ 2160p";
                            break;
                        case "videodb":
                        case "kinobase":
                        case "zetflix":
                        case "vcdn":
                        case "eneyida":
                        case "hdvb":
                        case "anilibria":
                        case "animedia":
                        case "redheadsound":
                        case "iframevideo":
                        case "animego":
                        case "lostfilmhd":
                            quality = " ~ 1080p";
                            break;
                        case "voidboost":
                        case "animevost":
                        case "animebesst":
                        case "kodik":
                        case "kinotochka":
                            quality = " ~ 720p";
                            break;
                        case "kinokrad":
                        case "kinoprofi":
                        case "seasonvar":
                            quality = " - 480p";
                            break;
                        case "cdnmovies":
                            quality = " - 360p";
                            break;
                        default:
                            break;
                    }

                    if (balanser == "filmix")
                        quality = AppInit.conf.Filmix.pro ? " ~ 2160p" : " - 480p";

                    if (balanser == "collaps")
                        quality = AppInit.conf.Collaps.dash ? " ~ 1080p" : " ~ 720p";
                }
            }
            #endregion

            links.Add(("{" + $"\"name\":\"{name + quality}\",\"url\":\""+(isLocalhost ? "{localhost}" : serv) +$"/{uri}\",\"index\":{index},\"show\":{work.ToString().ToLower()},\"balanser\":\"{balanser}\"" + "},", index, work));

            memoryCache.Set(checkOnlineSearchKey(id, source), (links.Count == tasks.Count, tasks.Count, string.Join("", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code))), DateTime.Now.AddMinutes(10));
        }
        #endregion
    }
}