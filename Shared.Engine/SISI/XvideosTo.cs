﻿using Lampac.Models.SISI;
using Shared.Model.SISI;
using Shared.Model.SISI.Xvideos;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class XvideosTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string plugin, string? search, string? sort, string? c, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url;

            if (!string.IsNullOrWhiteSpace(search))
            {
                url = $"{host}/?k={HttpUtility.UrlEncode(search)}&p={pg}";
            }
            else
            {
                if (!string.IsNullOrEmpty(c))
                {
                    url = $"{host}/c/s:{(sort == "top"  ? "rating" : "uploaddate")}/{c}/{pg}";
                }
                else
                {
                    if (sort == "top")
                    {
                        url = $"{host}/{(plugin == "xdsgay" ? "best-of-gay" : plugin == "xdssml" ? "best-of-shemale" : "best")}/{DateTime.Today.AddMonths(-1):yyyy-MM}";
                    }
                    else
                    {
                        url = plugin == "xdsgay" ? $"{host}/gay" : plugin == "xdssml" ? $"{host}/shemale" : $"{host}/new";
                    }

                    url += $"/{pg}";
                }
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string? html, Func<PlaylistItem, PlaylistItem>? onplaylist = null, string site = "xds")
        {
            var playlists = new List<PlaylistItem>() { Capacity = 40 };

            if (string.IsNullOrEmpty(html))
                return playlists;

            foreach (string row in html.Split("<div class=\"thumb-inside\">").Skip(1))
            {
                var g = Regex.Match(row, "<a href=\"/(prof-video-click/[^\"]+|video[0-9]+/[^\"]+)\" title=\"([^\"]+)\"").Groups;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string qmark = Regex.Match(row, "<span class=\"video-hd-mark\">([^<]+)</span>").Groups[1].Value;
                    string duration = Regex.Match(row, "<span class=\"duration\">([^<]+)</span>").Groups[1].Value.Trim();

                    string img = Regex.Match(row, "data-src=\"([^\"]+)\"").Groups[1].Value;
                    img = Regex.Replace(img, "/videos/thumbs([0-9]+)/", "/videos/thumbs$1lll/");
                    img = Regex.Replace(img, "\\.THUMBNUM\\.(jpg|png)$", ".1.$1", RegexOptions.IgnoreCase);

                    var pl = new PlaylistItem()
                    {
                        name = g[2].Value,
                        video = $"{uri}?uri={g[1].Value}",
                        picture = img,
                        quality = string.IsNullOrWhiteSpace(qmark) ? null : qmark,
                        time = duration,
                        json = true,
                        bookmark = new Bookmark()
                        {
                            site = site,
                            href = g[1].Value,
                            image = img
                        }
                    };

                    if (onplaylist != null)
                        pl = onplaylist.Invoke(pl);

                    playlists.Add(pl);
                }
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string? host, string plugin, string? sort, string? c)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";
            string url = host + plugin;

            var menu = new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = url,
                },
                new MenuItem()
                {
                    title = $"Ориентация: {(plugin == "xdsgay" ? "Геи" : plugin == "xdssml" ? "Трансы" :"Гетеро")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Гетеро",
                            playlist_url = host + "xds",
                        },
                        new MenuItem()
                        {
                            title = "Геи",
                            playlist_url = host + "xdsgay",
                        },
                        new MenuItem()
                        {
                            title = "Трансы",
                            playlist_url = host + "xdssml",
                        }
                    }
                },
                new MenuItem()
                {
                    title = $"Сортировка: {(sort == "top" ? "Лучшие" : "Новое")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Новое",
                            playlist_url = url + $"?c={c}"
                        },
                        new MenuItem()
                        {
                            title = "Лучшие",
                            playlist_url = url + $"?c={c}&sort=top"
                        }
                    }
                }
            };

            if (plugin == "xds")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Все",
                        playlist_url = url
                    },
                    new MenuItem()
                    {
                        title = "Азиат",
                        playlist_url = url + $"?sort={sort}&c=Asian_Woman-32"
                    },
                    new MenuItem()
                    {
                        title = "Анал",
                        playlist_url = url + $"?sort={sort}&c=Anal-12"
                    },
                    new MenuItem()
                    {
                        title = "Арабки",
                        playlist_url = url + $"?sort={sort}&c=Arab-159"
                    },
                    new MenuItem()
                    {
                        title = "Бисексуалы",
                        playlist_url = url + $"?sort={sort}&c=Bi_Sexual-62"
                    },
                    new MenuItem()
                    {
                        title = "Блондинки",
                        playlist_url = url + $"?sort={sort}&c=Blonde-20"
                    },
                    new MenuItem()
                    {
                        title = "Большие Попы",
                        playlist_url = url + $"?sort={sort}&c=Big_Ass-24"
                    },
                    new MenuItem()
                    {
                        title = "Большие Сиськи",
                        playlist_url = url + $"?sort={sort}&c=Big_Tits-23"
                    },
                    new MenuItem()
                    {
                        title = "Большие яйца",
                        playlist_url = url + $"?sort={sort}&c=Big_Cock-34"
                    },
                    new MenuItem()
                    {
                        title = "Брюнетки",
                        playlist_url = url + $"?sort={sort}&c=Brunette-25"
                    },
                    new MenuItem()
                    {
                        title = "В масле",
                        playlist_url = url + $"?sort={sort}&c=Oiled-22"
                    },
                    new MenuItem()
                    {
                        title = "Веб камеры",
                        playlist_url = url + $"?sort={sort}&c=Cam_Porn-58"
                    },
                    new MenuItem()
                    {
                        title = "Гэнгбэнг",
                        playlist_url = url + $"?sort={sort}&c=Gangbang-69"
                    },
                    new MenuItem()
                    {
                        title = "Зияющие отверстия",
                        playlist_url = url + $"?sort={sort}&c=Gapes-167"
                    },
                    new MenuItem()
                    {
                        title = "Зрелые",
                        playlist_url = url + $"?sort={sort}&c=Mature-38"
                    },
                    new MenuItem()
                    {
                        title = "Индийский",
                        playlist_url = url + $"?sort={sort}&c=Indian-89"
                    },
                    new MenuItem()
                    {
                        title = "Испорченная семья",
                        playlist_url = url + $"?sort={sort}&c=Fucked_Up_Family-81"
                    },
                    new MenuItem()
                    {
                        title = "Кончает внутрь",
                        playlist_url = url + $"?sort={sort}&c=Creampie-40"
                    },
                    new MenuItem()
                    {
                        title = "Куколд / Горячая Жена",
                        playlist_url = url + $"?sort={sort}&c=Cuckold-237"
                    },
                    new MenuItem()
                    {
                        title = "Латинки",
                        playlist_url = url + $"?sort={sort}&c=Latina-16"
                    },
                    new MenuItem()
                    {
                        title = "Лесбиянки",
                        playlist_url = url + $"?sort={sort}&c=Lesbian-26"
                    },
                    new MenuItem()
                    {
                        title = "Любительское порно",
                        playlist_url = url + $"?sort={sort}&c=Amateur-65"
                    },
                    new MenuItem()
                    {
                        title = "Мамочки. МИЛФ",
                        playlist_url = url + $"?sort={sort}&c=Milf-19"
                    },
                    new MenuItem()
                    {
                        title = "Межрассовые",
                        playlist_url = url + $"?sort={sort}&c=Interracial-27"
                    },
                    new MenuItem()
                    {
                        title = "Минет",
                        playlist_url = url + $"?sort={sort}&c=Blowjob-15"
                    },
                    new MenuItem()
                    {
                        title = "Нижнее бельё",
                        playlist_url = url + $"?sort={sort}&c=Lingerie-83"
                    },
                    new MenuItem()
                    {
                        title = "Попки",
                        playlist_url = url + $"?sort={sort}&c=Ass-14"
                    },
                    new MenuItem()
                    {
                        title = "Рыжие",
                        playlist_url = url + $"?sort={sort}&c=Redhead-31"
                    },
                    new MenuItem()
                    {
                        title = "Сквиртинг",
                        playlist_url = url + $"?sort={sort}&c=Squirting-56"
                    },
                    new MenuItem()
                    {
                        title = "Соло",
                        playlist_url = url + $"?sort={sort}&c=Solo_and_Masturbation-33"
                    },
                    new MenuItem()
                    {
                        title = "Сперма",
                        playlist_url = url + $"?sort={sort}&c=Cumshot-18"
                    },
                    new MenuItem()
                    {
                        title = "Тинейджеры",
                        playlist_url = url + $"?sort={sort}&c=Teen-13"
                    },
                    new MenuItem()
                    {
                        title = "Фемдом",
                        playlist_url = url + $"?sort={sort}&c=Femdom-235"
                    },
                    new MenuItem()
                    {
                        title = "Фистинг",
                        playlist_url = url + $"?sort={sort}&c=Fisting-165"
                    },
                    new MenuItem()
                    {
                        title = "Черные Женщины",
                        playlist_url = url + $"?sort={sort}&c=bbw-51"
                    },
                    new MenuItem()
                    {
                        title = "Черный",
                        playlist_url = url + $"?sort={sort}&c=Black_Woman-30"
                    },
                    new MenuItem()
                    {
                        title = "Чулки,колготки",
                        playlist_url = url + $"?sort={sort}&c=Stockings-28"
                    },
                    new MenuItem()
                    {
                        title = "ASMR",
                        playlist_url = url + $"?sort={sort}&c=ASMR-229"
                    }
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"c={c}"))?.title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }

            return menu;
        }

        async public static ValueTask<StreamItem?> StreamLinks(string uri, string host, string? url, Func<string, ValueTask<string?>> onresult, Func<string, ValueTask<string?>>? onm3u = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            string? html = await onresult.Invoke($"{host}/{Regex.Replace(url ?? "", "^([^/]+)/.*", "$1/_")}");
            string stream_link = new Regex("html5player\\.setVideoHLS\\('([^']+)'\\);").Match(html ?? "").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(stream_link))
                return null;

            #region getRelated
            List<PlaylistItem>? getRelated()
            {
                var related = new List<PlaylistItem>();

                string json = Regex.Match(html!, "video_related=([^\n\r]+);window").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(json) || !json.StartsWith("[") || !json.EndsWith("]"))
                    return related;

                try
                {
                    foreach (var r in JsonSerializer.Deserialize<List<Related>>(json))
                    {
                        if (string.IsNullOrEmpty(r.tf) || string.IsNullOrEmpty(r.u) || string.IsNullOrEmpty(r.i))
                            continue;

                        related.Add(new PlaylistItem()
                        {
                            name = r.tf,
                            video = $"{uri}?uri={r.u.Remove(0, 1)}",
                            picture = r.i,
                            json = true
                        });
                    }
                }
                catch { }

                return related;
            }
            #endregion

            string? m3u8 = onm3u == null ? null : await onm3u.Invoke(stream_link);
            if (m3u8 == null)
            {
                return new StreamItem()
                {
                    qualitys = new Dictionary<string, string>()
                    {
                        ["auto"] = stream_link
                    },
                    recomends = getRelated()
                };
            }

            var stream_links = new Dictionary<int, string>();

            foreach (string line in m3u8.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("hls-"))
                    continue;

                string _q = new Regex("hls-([0-9]+)p").Match(line).Groups[1].Value;

                if (int.TryParse(_q, out int q) && q > 0)
                    stream_links.TryAdd(q, $"{Regex.Replace(stream_link, "/hls.m3u8.*", "")}/{line}");
            }

            return new StreamItem()
            {
                qualitys = stream_links.OrderByDescending(i => i.Key).ToDictionary(k => $"{k.Key}p", v => v.Value),
                recomends = getRelated()
            };
        }
    }
}
