﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using HtmlAgilityPack;
using Xamarin.Forms;

namespace GitTrends
{
    public class FavIconService
    {
        public const string DefaultFavIcon = "DefaultProfileImageGreen";

        static readonly Lazy<HttpClient> _clientHolder = new Lazy<HttpClient>(() => new HttpClient { Timeout = HttpClientTimeout });

        public static TimeSpan HttpClientTimeout { get; } = TimeSpan.FromSeconds(1);

        static HttpClient Client => _clientHolder.Value;

        public static async Task<ImageSource> GetFavIconImageSource(Uri site)
        {
            var baseUrl = $"{site.Scheme}://{getRootDomain(site.Host)}";

            try
            {
                var httpResponseMessage = await Client.GetAsync(baseUrl).ConfigureAwait(false);
                var html = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);

                var (shortcutIconUrlTask, appleTouchIconUrlTask, iconUrlTask, favIconUrlTask) = GetFavIconTasks(htmlDocument, baseUrl);

                var appleTouchIconUrl = await appleTouchIconUrlTask.ConfigureAwait(false);
                if (appleTouchIconUrl != null)
                    return appleTouchIconUrl;

                var shortcutIconUrl = await shortcutIconUrlTask.ConfigureAwait(false);
                if (shortcutIconUrl != null)
                    return shortcutIconUrl;

                var iconUrl = await iconUrlTask.ConfigureAwait(false);
                if (iconUrl != null)
                    return iconUrl;

                var favIconUrl = await favIconUrlTask.ConfigureAwait(false);
                if (favIconUrl != null)
                    return favIconUrl;

                return DefaultFavIcon;
            }
            catch (Exception e)
            {
                using var scope = ContainerService.Container.BeginLifetimeScope();
                scope.Resolve<AnalyticsService>().Report(e, new Dictionary<string, string>
                {
                    { nameof(baseUrl), baseUrl },
                    { nameof(site), site.ToString() }
                });

                return DefaultFavIcon;
            }

            //https://stackoverflow.com/a/35213737/5953643
            static string getRootDomain(in string host)
            {
                string[] domains = host.Split('.');

                if (domains.Length >= 3)
                {
                    int domainCount = domains.Length;
                    // handle international country code TLDs 
                    // www.amazon.co.uk => amazon.co.uk
                    if (domains[domainCount - 1].Length < 3 && domains[domainCount - 2].Length <= 3)
                        return string.Join(".", domains, domainCount - 3, 3);
                    else
                        return string.Join(".", domains, domainCount - 2, 2);
                }
                else
                {
                    return host;
                }
            }
        }

        static (Task<string?> ShortcutIconUrlTask, Task<string?> AppleTouchIconUrlTask, Task<string?> IconUrlTask, Task<string?> FavIconUrlTask) GetFavIconTasks(HtmlDocument htmlDoc, string siteUrl)
        {
            var shortcutIconUrlTask = GetShortcutIconUrl(htmlDoc, siteUrl);
            var appleTouchIconUrlTask = GetAppleTouchIconUrl(htmlDoc, siteUrl);
            var iconUrlTask = GetIconUrl(htmlDoc, siteUrl);
            var favIconUrlTask = GetFavIconUrl(siteUrl);

            return (shortcutIconUrlTask, appleTouchIconUrlTask, iconUrlTask, favIconUrlTask);
        }

        static async Task<string?> GetFavIconUrl(string url)
        {
            try
            {
                var faviconUrl = $"{url}favicon.ico";

                var isValid = await IsUrlValid(faviconUrl).ConfigureAwait(false);

                if (isValid)
                {
                    Debug.WriteLine($"{nameof(GetIconUrl)}: {faviconUrl}");
                    return faviconUrl;
                }
                else
                {
                    Debug.WriteLine($"{nameof(GetIconUrl)}: null");
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        static async Task<string?> GetShortcutIconUrl(HtmlDocument htmlDoc, string url)
        {
            try
            {
                var shortcutIconNode = htmlDoc.DocumentNode.SelectNodes("//head//link").SelectMany(x => x.Attributes.Where(x => x.Value is "shortcut icon")).First();
                var hrefValue = shortcutIconNode.OwnerNode.Attributes.First(x => x.Name is "href").Value;

                var shortcutIconUrl = hrefValue.Contains("http") ? hrefValue : url.Trim('/') + hrefValue;

                var isValid = await IsUrlValid(shortcutIconUrl).ConfigureAwait(false);
                if (isValid)
                {
                    Debug.WriteLine($"{nameof(GetIconUrl)}: {shortcutIconUrl}");
                    return shortcutIconUrl;
                }
                else
                {
                    Debug.WriteLine($"{nameof(GetIconUrl)}: null");
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        static async Task<string?> GetAppleTouchIconUrl(HtmlDocument htmlDoc, string url)
        {
            try
            {
                var shortcutIconNode = htmlDoc.DocumentNode.SelectNodes("//head//link").SelectMany(x => x.Attributes.Where(x => x.Value is "apple-touch-icon")).First();
                var hrefValue = shortcutIconNode.OwnerNode.Attributes.First(x => x.Name is "href").Value;

                var appleTouchIconUrl = hrefValue.Contains("http") ? hrefValue : url.Trim('/') + hrefValue;

                var isValid = await IsUrlValid(appleTouchIconUrl).ConfigureAwait(false);
                if (isValid)
                {
                    Debug.WriteLine($"{nameof(GetIconUrl)}: {appleTouchIconUrl}");
                    return appleTouchIconUrl;
                }
                else
                {
                    Debug.WriteLine($"{nameof(GetIconUrl)}: null");
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        static async Task<string?> GetIconUrl(HtmlDocument htmlDoc, string url)
        {
            try
            {
                var shortcutIconNode = htmlDoc.DocumentNode.SelectNodes("//head//link").SelectMany(x => x.Attributes.Where(x => x.Value is "icon")).First();
                var hrefValue = shortcutIconNode.OwnerNode.Attributes.First(x => x.Name is "href").Value;

                var iconUrl = hrefValue.Contains("http") ? hrefValue : url.Trim('/') + hrefValue;

                var isValid = await IsUrlValid(iconUrl).ConfigureAwait(false);
                if (isValid)
                {
                    Debug.WriteLine($"{nameof(GetIconUrl)}: {iconUrl}");
                    return iconUrl;
                }
                else
                {
                    Debug.WriteLine($"{nameof(GetIconUrl)}: null");
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        static async ValueTask<bool> IsUrlValid(string? url)
        {
            if (url is null || url.EndsWith(".svg"))
                return false;

            var response = await Client.GetAsync(url).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
    }
}
