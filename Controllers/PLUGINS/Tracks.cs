﻿using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Linq;

namespace Lampac.Controllers.PLUGINS
{
    public class TracksController : BaseController
    {
        [Route("ffprobe")]
        async public Task<ActionResult> Ffprobe(string media)
        {
            if (!AppInit.conf.ffprobe.enable || string.IsNullOrWhiteSpace(media) || !media.StartsWith("http"))
                return Content(string.Empty);

            if (media.Contains("/dlna/stream"))
            {
                string path = Regex.Match(media, "\\?path=([^&]+)").Groups[1].Value;
                if (!System.IO.File.Exists("dlna/" + HttpUtility.UrlDecode(path)))
                    return Content(string.Empty);

                string account_email = AppInit.conf.accsdb.enable ? AppInit.conf.accsdb?.accounts?.First() : "";
                media = $"{host}/dlna/stream?path={path}&account_email={HttpUtility.UrlEncode(account_email)}";
            }
            else if (media.Contains("/stream/"))
            {
                media = Regex.Replace(media, "[^a-z0-9_:\\-\\/\\.\\=\\?\\&]+", "", RegexOptions.IgnoreCase);
                media = Regex.Replace(media, "^(https?://[a-z0-9_:\\-\\.]+/stream/)[^\\?]+", "$1", RegexOptions.IgnoreCase);

                if (!string.IsNullOrWhiteSpace(AppInit.conf.ffprobe.tsuri))
                    media = Regex.Replace(media, "^https?://[^/]+", AppInit.conf.ffprobe.tsuri, RegexOptions.IgnoreCase);
            }
            else
            {
                return Content(string.Empty);
            }

            string memKey = $"tracks:ffprobe:{media}";
            if (!memoryCache.TryGetValue(memKey, out string outPut))
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.FileName = AppInit.conf.ffprobe.os == "linux" ? "ffprobe" : $"ffprobe/{AppInit.conf.ffprobe.os}.exe";
                process.StartInfo.Arguments = $"-v quiet -print_format json -show_format -show_streams {media}";
                process.Start();

                outPut = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (outPut == null)
                    outPut = string.Empty;

                if (Regex.Replace(outPut, "[\n\r\t ]+", "") == "{}")
                    outPut = string.Empty;

                memoryCache.Set(memKey, outPut, DateTime.Now.AddHours(1));
            }

            return Content(outPut, contentType: "application/json; charset=utf-8");
        }


        [HttpGet]
        [Route("tracks.js")]
        public ActionResult Tracks()
        {
            string file = System.IO.File.ReadAllText("plugins/tracks.js");
            file = file.Replace("{localhost}", host);

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
    }
}
