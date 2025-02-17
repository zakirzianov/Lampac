﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Linq;
using Online;
using Shared.Engine.CORE;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class VoKino : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("vokino", AppInit.conf.VoKino);

        #region vokinotk
        [HttpGet]
        [Route("lite/vokinotk")]
        async public Task<ActionResult> Token(string login, string pass)
        {
            string html = string.Empty;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
            {
                html = "Введите данные аккаунта <a href='http://vokino.tv'>vokino.tv</a> <br> <br><form method=\"get\" action=\"/lite/vokinotk\"><input type=\"text\" name=\"login\" placeholder=\"email\"> &nbsp; &nbsp; <input type=\"text\" name=\"pass\" placeholder=\"пароль\"><br><br><button>Добавить устройство</button></form> ";
            }
            else
            {
                string deviceid = new string(DateTime.Now.ToBinary().ToString().Reverse().ToArray()).Substring(0, 8);
                var token_request = await HttpClient.Get<JObject>($"{AppInit.conf.VoKino.corsHost()}/v2/auth?email={HttpUtility.UrlEncode(login)}&passwd={HttpUtility.UrlEncode(pass)}&deviceid={deviceid}", proxy: proxyManager.Get());

                html = $"В init.conf для VoKino укажите token <br><br><b>{token_request.Value<string>("authToken")}</b>";
            }

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/vokino")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title)
        {
            var init = AppInit.conf.VoKino;

            if (!init.enable || kinopoisk_id == 0 || string.IsNullOrEmpty(init.token))
                return OnError();

            var proxy = proxyManager.Get();

            var oninvk = new VoKinoInvoke
            (
               null,
               init.corsHost(),
               init.token,
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy)
            );

            var content = await InvokeCache($"vokino:{kinopoisk_id}:{proxyManager.CurrentProxyIp}", cacheTime(20), () => oninvk.Embed(kinopoisk_id), proxyManager);
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, title, original_title), "text/html; charset=utf-8");
        }
    }
}
