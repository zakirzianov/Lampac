﻿using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System;
using Lampac.Models.Module;
using System.Reflection;
using Lampac.Models.AppConf;
using Lampac.Models.DLNA;
using Lampac.Models.Merchant;
using System.Collections.Concurrent;
using Shared.Model.Base;
using System.Text.RegularExpressions;

namespace Lampac
{
    public class AppInit : Shared.Model.AppInit
    {
        #region conf
        static (AppInit, DateTime) cacheconf = default;

        public static AppInit conf
        {
            get
            {
                if (!File.Exists("init.conf"))
                    return new AppInit();

                var lastWriteTime = File.GetLastWriteTime("init.conf");

                if (cacheconf.Item2 != lastWriteTime)
                {
                    var jss = new JsonSerializerSettings { Error = (se, ev) => 
                    { 
                        ev.ErrorContext.Handled = true; 
                        Console.WriteLine("init.conf - " + ev.ErrorContext.Error + "\n\n"); 
                    }};

                    cacheconf.Item1 = JsonConvert.DeserializeObject<AppInit>(File.ReadAllText("init.conf"), jss);
                    if (cacheconf.Item1 == null)
                        cacheconf.Item1 = new AppInit();

                    cacheconf.Item2 = lastWriteTime;

                    if (cacheconf.Item1 != null)
                    {
                        if (!string.IsNullOrEmpty(cacheconf.Item1.corsehost))
                            corseuhost = cacheconf.Item1.corsehost;
                    }

                    if (File.Exists("merchant/users.txt"))
                    {
                        long utc = DateTime.UtcNow.ToFileTimeUtc();
                        foreach (string line in File.ReadAllLines("merchant/users.txt"))
                        {
                            if (string.IsNullOrWhiteSpace(line) && !line.Contains("@"))
                                continue;

                            var data = line.Split(',');
                            if (data.Length > 1)
                            {
                                if (long.TryParse(data[1], out long ex) && ex > utc)
                                {
                                    DateTime e = DateTime.FromFileTimeUtc(ex);
                                    string email = data[0].Trim().ToLower();

                                    if (cacheconf.Item1.accsdb.accounts.TryGetValue(email, out DateTime _ex))
                                    {
                                        if (e > _ex)
                                            cacheconf.Item1.accsdb.accounts[email] = e;
                                    }
                                    else
                                        cacheconf.Item1.accsdb.accounts.TryAdd(email, e);
                                }
                            }
                        }
                    }
                }

                return cacheconf.Item1;
            }
        }

        public static string Host(HttpContext httpContext)
        {
            string scheme = string.IsNullOrEmpty(conf.listenscheme) ? httpContext.Request.Scheme : conf.listenscheme;

            if (!string.IsNullOrEmpty(conf.listenhost))
                return $"{scheme}://{conf.listenhost}";

            if (httpContext.Request.Headers.TryGetValue("xhost", out var xhost))
                return $"{scheme}://{Regex.Replace(xhost, "^https?://", "")}";

            return $"{scheme}://{httpContext.Request.Host.Value}";
        }
        #endregion

        #region modules
        public static List<RootModule> modules;

        static AppInit()
        {
            if (File.Exists("module/manifest.json"))
            {
                var jss = new JsonSerializerSettings { Error = (se, ev) => 
                { 
                    ev.ErrorContext.Handled = true; 
                    Console.WriteLine("module/manifest.json - " + ev.ErrorContext.Error + "\n\n"); 
                }};

                var mods = JsonConvert.DeserializeObject<List<RootModule>>(File.ReadAllText("module/manifest.json"), jss);
                if (mods == null)
                    return;

                modules = new List<RootModule>();
                foreach (var mod in mods)
                {
                    if (!mod.enable || mod.dll == "Jackett.dll")
                        continue;

                    string path = File.Exists(mod.dll) ? mod.dll : $"{Environment.CurrentDirectory}/module/{mod.dll}";
                    if (File.Exists(path))
                    {
                        try
                        {
                            mod.assembly = Assembly.LoadFile(path);

                            if (mod.initspace != null && mod.assembly.GetType(mod.initspace) is Type t && t.GetMethod("loaded") is MethodInfo m)
                                m.Invoke(null, new object[] { });

                            modules.Add(mod);
                        }
                        catch { }
                    }
                }
            }
        }
        #endregion

        public static bool Win32NT => Environment.OSVersion.Platform == PlatformID.Win32NT;


        public string listenip = "any";

        public string listenscheme = null;

        public string listenhost = null;

        public int listenport = 9118;

        public string localhost = "127.0.0.1";

        public bool isarm = false;

        public bool multiaccess = false;

        public bool mikrotik = false;

        public string typecache = "hybrid"; // mem|file|hybrid

        public bool pirate_store = true;

        public bool puppeteer_keepopen = false;

        public string puppeteer_ExecutablePath;

        public string apikey = null;

        public bool litejac = true;

        public bool filelog = false;

        public bool weblog = false;

        public string anticaptchakey;


        public FfprobeSettings ffprobe = new FfprobeSettings() { enable = true };

        public ServerproxyConf serverproxy = new ServerproxyConf() { enable = true, encrypt = true, verifyip = true, allow_tmdb = true };

        public CronTime crontime = new CronTime() { updateLampaWeb = 20, clearCache = 60, updateTrackers = 120 };


        public FileCacheConf fileCacheInactiveHour = new FileCacheConf() { html = 5, img = 8, torrent = 36, hls = 24 };

        public DLNASettings dlna = new DLNASettings() { enable = true,  autoupdatetrackers = true, path = "dlna" };

        public WebConf LampaWeb = new WebConf() { autoupdate = true, basetag = true, index = "lampa-main/index.html" };

        public OnlineConf online = new OnlineConf() { findkp = "all", checkOnlineSearch = true };

        public AccsConf accsdb = new AccsConf() { authMesage = "Войдите в аккаунт cub.red", denyMesage = "Добавьте {account_email} в init.conf", expiresMesage = "Время доступа для {account_email} истекло в {expires}", maxiptohour = 15 };

        public MerchantsModel Merchant = new MerchantsModel();

        public HashSet<Known> KnownProxies { get; set; }

        public bool real_ip_cf { get; set; }

        public ProxySettings proxy = new ProxySettings();

        public ConcurrentBag<ProxySettings> globalproxy = new ConcurrentBag<ProxySettings>()
        {
            new ProxySettings()
            {
                pattern = "\\.onion",
                list = new ConcurrentBag<string>() { "socks5://127.0.0.1:9050" }
            }
        };


        public ConcurrentBag<OverrideResponse> overrideResponse = new ConcurrentBag<OverrideResponse>()
        {
            new OverrideResponse()
            {
                pattern = "/over/text",
                action = "html",
                type = "text/plain; charset=utf-8",
                val = "text"
            },
            new OverrideResponse()
            {
                pattern = "/over/online.js",
                action = "file",
                type = "application/javascript; charset=utf-8",
                val = "plugins/online.js"
            },
            new OverrideResponse()
            {
                pattern = "/over/gogoole",
                action = "redirect",
                val = "https://www.google.com/"
            }
        };
    }
}
