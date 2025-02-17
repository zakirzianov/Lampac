﻿using Lampac;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shared.Engine
{
    public class PuppeteerTo : IDisposable
    {
        #region static
        static IBrowser browser_keepopen = null;

        static bool isdev = File.Exists(@"C:\ProgramData\lampac\disablesync");

        public static bool IsKeepOpen => AppInit.conf.multiaccess || AppInit.conf.puppeteer_keepopen;

        static List<string> tabs = new List<string>();

        public static void LaunchKeepOpen()
        {
            browser_keepopen = Launch()?.Result;

            if (browser_keepopen != null)
                browser_keepopen.Closed += Browser_keepopen_Closed;
        }

        async private static void Browser_keepopen_Closed(object sender, EventArgs e)
        {
            browser_keepopen.Closed -= Browser_keepopen_Closed;
            await Task.Delay(10_000);
            browser_keepopen = await Launch();

            if (browser_keepopen != null)
                browser_keepopen.Closed += Browser_keepopen_Closed;
        }

        async public static ValueTask<PuppeteerTo> Browser()
        {
            if (IsKeepOpen || browser_keepopen != null)
                return new PuppeteerTo(browser_keepopen);

            return new PuppeteerTo(await Launch());
        }

        static Task<IBrowser> Launch()
        {
            try
            {
                var option = new LaunchOptions()
                {
                    Headless = !isdev, /*false*/
                    Devtools = isdev,
                    IgnoreHTTPSErrors = true,
                    Args = new string[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage", "--disable-gpu", "--renderer-process-limit=1" },
                    Timeout = 15_000
                };

                if (!string.IsNullOrEmpty(AppInit.conf.puppeteer_ExecutablePath))
                    option.ExecutablePath = AppInit.conf.puppeteer_ExecutablePath;
                else if (AppInit.conf.isarm)
                    option.ExecutablePath = "/usr/bin/chromium-browser";

                return Puppeteer.LaunchAsync(option);
            }
            catch (Exception ex) 
            { 
                Console.WriteLine(ex.ToString()); 
                return null; 
            }
        }
        #endregion

        IBrowser browser;

        int tabIndex = 0;

        public PuppeteerTo(IBrowser browser)
        {
            this.browser = browser; 
        }

        public ValueTask<IPage> Page(string plugin, Dictionary<string, string> headers = null)
        {
            return Page(plugin, null, headers);
        }

        async public ValueTask<IPage> Page(string plugin, CookieParam[] cookies, Dictionary<string, string> headers = null)
        {
            try
            {
                if (browser == null)
                    return null;

                if (IsKeepOpen)
                {
                    if (!tabs.Contains(plugin))
                    {
                        tabs.Add(plugin);
                        await browser.NewPageAsync();
                    }

                    tabIndex = tabs.IndexOf(plugin);
                }

                var page = (await browser.PagesAsync())[tabIndex];

                if (headers != null && headers.Count > 0)
                    await page.SetExtraHttpHeadersAsync(headers);

                await page.SetCacheEnabledAsync(IsKeepOpen);
                await page.DeleteCookieAsync();

                if (cookies != null)
                    await page.SetCookieAsync(cookies);

                await page.SetRequestInterceptionAsync(true);
                page.Request += Page_Request;

                return page;
            }
            catch { return null; }
        }

        private void Page_Request(object sender, RequestEventArgs e)
        {
            if (Regex.IsMatch(e.Request.Url, "\\.(ico|png|jpe?g|WEBP|svg|css|EOT|TTF|WOFF2?|OTF)", RegexOptions.IgnoreCase) || e.Request.Url.StartsWith("data:image"))
            {
                e.Request.AbortAsync();
                return;
            }

            e.Request.ContinueAsync();
        }

        public void Dispose()
        {
            if (browser == null)
                return;

            try
            {
                if (!IsKeepOpen)
                    browser?.Dispose();
                else
                {
                    var pages = browser.PagesAsync().Result;

                    foreach (var pg in pages.Skip(tabs.Count))
                        pg.CloseAsync();

                    var page = pages[tabIndex];
                    page.GoToAsync("about:blank");
                    page.Request -= Page_Request;
                }
            }
            catch { }
        }
    }
}
