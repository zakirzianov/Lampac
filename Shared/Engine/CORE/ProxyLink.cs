﻿using Shared.Model.Online;
using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class ProxyLink
    {
        static ConcurrentDictionary<string, ProxyLinkModel> links = new ConcurrentDictionary<string, ProxyLinkModel>();

        public static string Encrypt(string uri, ProxyLinkModel p) => Encrypt(uri, p.reqip, p.headers, p.proxy, p.plugin);

        public static string Encrypt(string uri, string reqip, List<HeadersModel> headers = null, WebProxy proxy = null, string plugin = null)
        {
            if (!AppInit.conf.serverproxy.encrypt)
                return uri;

            string hash = CrypTo.md5(uri + (AppInit.conf.serverproxy.verifyip ? reqip : string.Empty));
            if (string.IsNullOrWhiteSpace(hash))
                return string.Empty;

            if (uri.Contains(".m3u"))
                hash += ".m3u8";
            else if (uri.Contains(".ts"))
                hash += ".ts";
            else if (uri.Contains(".m4s"))
                hash += ".m4s";
            else if (uri.Contains(".mp4"))
                hash += ".mp4";
            else if (uri.Contains(".mkv"))
                hash += ".mkv";
            else if (uri.Contains(".jpg") || uri.Contains(".jpeg") || uri.Contains(".png") || uri.Contains(".webp"))
                hash += ".jpg";

            if (!links.ContainsKey(hash))
            {
                var md = new ProxyLinkModel(reqip, headers, proxy, uri, plugin);
                links.AddOrUpdate(hash, md, (d, u) => md);
            }

            return hash;
        }

        public static ProxyLinkModel Decrypt(string hash, string reqip)
        {
            if (!AppInit.conf.serverproxy.encrypt)
                return null;

            if (links.TryGetValue(hash, out ProxyLinkModel val))
            {
                if (!AppInit.conf.serverproxy.verifyip || reqip == null || reqip == val.reqip)
                    return val;
            }

            return null;
        }


        async public static Task Cron()
        {
            await Task.Delay(TimeSpan.FromMinutes(1));

            while (true)
            {
                try
                {
                    foreach (var link in links)
                    {
                        if (DateTime.Now > link.Value.upd.AddHours(link.Value.uri.EndsWith(".jpg") ? 1 : 8))
                            links.TryRemove(link.Key, out _);
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(5));
            }
        }
    }
}
