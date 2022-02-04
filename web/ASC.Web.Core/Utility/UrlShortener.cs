using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web;

using ASC.Common;
using ASC.Core.Common.Configuration;
using ASC.FederatedLogin.LoginProviders;
using ASC.Security.Cryptography;
using ASC.Web.Studio.Utility;

using Microsoft.Extensions.Configuration;

namespace ASC.Web.Core.Utility;

public interface IUrlShortener
{
    string GetShortenLink(string shareLink);
}

[Scope]
public class UrlShortener
{
    public bool Enabled { get { return !(Instance is NullShortener); } }

    private IUrlShortener _instance;
    public IUrlShortener Instance
    {
        get
        {
            if (_instance == null)
            {
                if (_consumerFactory.Get<BitlyLoginProvider>().Enabled)
                {
                    _instance = new BitLyShortener(_consumerFactory);
                }
                else if (!string.IsNullOrEmpty(_configuration["web:url-shortener:value"]))
                {
                    _instance = new OnlyoShortener(_configuration, _commonLinkUtility, _machinePseudoKeys);
                }
                else
                {
                    _instance = new NullShortener();
                }
            }

            return _instance;
        }
        set
        {
            _instance = value;
        }
    }

    private readonly IConfiguration _configuration;
    private readonly ConsumerFactory _consumerFactory;
    private readonly CommonLinkUtility _commonLinkUtility;
    private readonly MachinePseudoKeys _machinePseudoKeys;

    public UrlShortener(
        IConfiguration configuration,
        ConsumerFactory consumerFactory,
        CommonLinkUtility commonLinkUtility,
        MachinePseudoKeys machinePseudoKeys)
    {
        _configuration = configuration;
        _consumerFactory = consumerFactory;
        _commonLinkUtility = commonLinkUtility;
        _machinePseudoKeys = machinePseudoKeys;
    }
}

public class BitLyShortener : IUrlShortener
{
    public BitLyShortener(ConsumerFactory consumerFactory)
    {
        _consumerFactory = consumerFactory;
    }

    private ConsumerFactory _consumerFactory;

    public string GetShortenLink(string shareLink)
    {
        return _consumerFactory.Get<BitlyLoginProvider>().GetShortenLink(shareLink);
    }
}

public class OnlyoShortener : IUrlShortener
{
    private readonly string _url;
    private readonly string _internalUrl;
    private readonly byte[] _sKey;
    private readonly CommonLinkUtility _commonLinkUtility;

    public OnlyoShortener(
        IConfiguration configuration,
        CommonLinkUtility commonLinkUtility,
        MachinePseudoKeys machinePseudoKeys)
    {
        _url = configuration["web:url-shortener:value"];
        _internalUrl = configuration["web:url-shortener:internal"];
        _sKey = machinePseudoKeys.GetMachineConstant();

        if (!_url.EndsWith("/"))
            _url += '/';
        _commonLinkUtility = commonLinkUtility;
    }

    public string GetShortenLink(string shareLink)
    {
        var request = new HttpRequestMessage();
        request.RequestUri = new Uri(_internalUrl + "?url=" + HttpUtility.UrlEncode(shareLink));
        request.Headers.Add("Authorization", CreateAuthToken());
        request.Headers.Add("Encoding", Encoding.UTF8.ToString());//todo check 

        using var httpClient = new HttpClient();
        using var response = httpClient.Send(request);
        using var stream = response.Content.ReadAsStream();
        using var rs = new StreamReader(stream);
        return _commonLinkUtility.GetFullAbsolutePath(_url + rs.ReadToEnd());
    }

    private string CreateAuthToken(string pkey = "urlShortener")
    {
        using var hasher = new HMACSHA1(_sKey);
        var now = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var hash = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", now, pkey))));
        return string.Format("ASC {0}:{1}:{2}", pkey, now, hash);
    }
}

public class NullShortener : IUrlShortener
{
    public string GetShortenLink(string shareLink)
    {
        return null;
    }
}