﻿using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Ags.ResourceProxy.Core {

	public class ProxyConfigService : IProxyConfigService {

		private ProxyConfig _config;
		private IWebHostEnvironment _hostingEnvironment { get; }

		public virtual ProxyConfig Config {
			get {
				if (_config == null) {
                    var jsonReadOptions = new JsonSerializerOptions(){ReadCommentHandling = JsonCommentHandling.Skip, IgnoreNullValues = true, PropertyNameCaseInsensitive = true};
                   _config =
                        JsonSerializer.Deserialize<ProxyConfig>(
                            File.ReadAllText(Path.Join(_hostingEnvironment.ContentRootPath, ConfigPath)), jsonReadOptions);
                }
                return _config;
			}
		}

		public string ConfigPath { get; }

		public ProxyConfigService(ProxyConfig config)
		{
			_config = config ?? throw new ArgumentException(nameof(config));
		}

		public ProxyConfigService(IWebHostEnvironment hostingEnvironment, string configPath) {
			_hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
			ConfigPath = configPath;
		}

		public NetworkCredential GetCredentials(ServerUrl serverUrlConfig) {
			NetworkCredential credentials = null;
			if (serverUrlConfig.UseAppPoolIdentity) {
				credentials = CredentialCache.DefaultNetworkCredentials;
			} else if (serverUrlConfig.Domain != null) {
				credentials = new NetworkCredential(serverUrlConfig.Username, serverUrlConfig.Password, serverUrlConfig.Domain);
			}
			return credentials;
		}

		public ServerUrl GetProxyServerUrlConfig(string queryStringUrl) {
			return Config.ServerUrls.FirstOrDefault(su => queryStringUrl.Contains(su.Url));
		}

		public List<KeyValuePair<string, string>> GetOAuth2FormData(ServerUrl su, string proxyReferrer) {
			var pairs = new List<KeyValuePair<string, string>> {
				new KeyValuePair<string, string>("client_id", su.ClientId),
				new KeyValuePair<string, string>("client_secret", su.ClientSecret),
				new KeyValuePair<string, string>("grant_type", "client_credentials"),
			};
			if (Config.TokenCacheMinutes > 0)
			{
				pairs.Add(new KeyValuePair<string, string>("expiration", Config.TokenCacheMinutes.ToString()));
			}
			return pairs;
		}

		public List<KeyValuePair<string, string>> GetPortalExchangeTokenFormData(ServerUrl su, string proxyReferrer, string portalCode) {
			return new List<KeyValuePair<string, string>> {
				new KeyValuePair<string, string>("client_id", su.ClientId),
				new KeyValuePair<string, string>("redirect_uri", proxyReferrer),
				new KeyValuePair<string, string>("grant_type", "authorization_code"),
				new KeyValuePair<string, string>("code", portalCode),
				new KeyValuePair<string, string>("f", "json")
			};
		}

		/// <summary>
		/// Determines if the referring URL is allowed to use the proxy.
		/// </summary>
		/// <param name="referer"></param>
		/// <returns></returns>
		public bool IsAllowedReferrer(string referer) {
			if (Config.AllowedReferrers == null || Config.AllowedReferrers.Length == 0) {
				return false; // Assume someone forgot to set this node in the config, take the safe path
			}
			if (Config.AllowedReferrers[0] == "*") {
				return true;  // User has defined all, let any site use proxy. Only use in development.
			}
			var uriAuthority = new Uri(referer).GetLeftPart(UriPartial.Authority);
			return Config.AllowedReferrers.Any(r => r.ToLower() == uriAuthority.ToLower());
		}

	}
}
