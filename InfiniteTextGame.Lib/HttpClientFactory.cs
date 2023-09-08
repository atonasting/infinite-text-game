using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace InfiniteTextGame.Lib
{
    /// <summary>
    /// 自定义支持代理的Http客户端工厂（支持http和socks5代理）
    /// </summary>
    internal class HttpClientFactoryWithProxy : IHttpClientFactory
    {
        private readonly string _proxy;
        public HttpClientFactoryWithProxy(string proxy = "")
        {
            _proxy = proxy;
        }

        public HttpClient CreateClient(string name)
        {
            var handler = new SocketsHttpHandler()
            {
                KeepAlivePingDelay = TimeSpan.FromSeconds(10),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always
            };
            if (!string.IsNullOrEmpty(_proxy))
            {
                handler.Proxy = new WebProxy(_proxy);
            }

            var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(180);
            return client;
        }
    }
}
