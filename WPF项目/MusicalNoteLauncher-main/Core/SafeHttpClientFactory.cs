using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public static class SafeHttpClientFactory
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<int, HttpClient> _clients = new Dictionary<int, HttpClient>();

        public static HttpClient CreateClient(int timeoutSeconds = 15)
        {
            lock (_lock)
            {
                if (_clients.TryGetValue(timeoutSeconds, out var existing))
                    return existing;

                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 5,
                    UseCookies = true
                };

                var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
                };

                ConfigureHeaders(client);
                _clients[timeoutSeconds] = client;

                return client;
            }
        }

        private static void ConfigureHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.minecraft.net/");
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            client.DefaultRequestHeaders.Connection.Add("keep-alive");
        }

        public static async Task<string> GetStringWithRetryAsync(HttpClient client, string url, int retryCount = 2)
        {
            int attempts = 0;
            Exception lastException = null;

            while (attempts <= retryCount)
            {
                try
                {
                    return await client.GetStringAsync(url).ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (IsTransientError(ex))
                {
                    lastException = ex;
                    attempts++;
                    if (attempts <= retryCount)
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts))).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == default)
                {
                    lastException = new TimeoutException("请求超时", ex);
                    attempts++;
                    if (attempts <= retryCount)
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts))).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            throw lastException ?? new HttpRequestException("请求失败");
        }

        private static bool IsTransientError(HttpRequestException ex)
        {
            if (ex.InnerException is WebException webEx)
            {
                switch (webEx.Status)
                {
                    case WebExceptionStatus.ConnectFailure:
                    case WebExceptionStatus.Timeout:
                    case WebExceptionStatus.ConnectionClosed:
                    case WebExceptionStatus.KeepAliveFailure:
                    case WebExceptionStatus.NameResolutionFailure:
                        return true;
                }
            }

            if (ex.StatusCode.HasValue)
            {
                switch (ex.StatusCode.Value)
                {
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.BadGateway:
                    case HttpStatusCode.ServiceUnavailable:
                    case HttpStatusCode.GatewayTimeout:
                    case HttpStatusCode.RequestTimeout:
                        return true;
                }
            }

            return false;
        }
    }
}