/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using System;
using System.IO;
using System.Linq;

using ASC.Common;
using ASC.Common.Caching;
using ASC.Core;
using ASC.Core.Common.Settings;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ASC.Web.Core.WhiteLabel;

[Scope]
public class TenantLogoManager
{
    private string CacheKey
    {
        get { return "letterlogodata" + _tenantManager.GetCurrentTenant().TenantId; }
    }

    public bool WhiteLabelEnabled
    {
        get;
        private set;
    }

    private readonly ICache _cache;
    private readonly ICacheNotify<TenantLogoCacheItem> _cacheNotify;
    private readonly TenantWhiteLabelSettingsHelper _tenantWhiteLabelSettingsHelper;
    private readonly SettingsManager _settingsManager;
    private readonly TenantInfoSettingsHelper _tenantInfoSettingsHelper;
    private readonly TenantManager _tenantManager;
    private readonly AuthContext _authContext;
    private readonly IConfiguration _configuration;

    public TenantLogoManager(
        TenantWhiteLabelSettingsHelper tenantWhiteLabelSettingsHelper,
        SettingsManager settingsManager,
        TenantInfoSettingsHelper tenantInfoSettingsHelper,
        TenantManager tenantManager,
        AuthContext authContext,
        IConfiguration configuration,
        ICacheNotify<TenantLogoCacheItem> cacheNotify,
        ICache cache)
    {
        _tenantWhiteLabelSettingsHelper = tenantWhiteLabelSettingsHelper;
        _settingsManager = settingsManager;
        _tenantInfoSettingsHelper = tenantInfoSettingsHelper;
        _tenantManager = tenantManager;
        _authContext = authContext;
        _configuration = configuration;
        var hideSettings = (_configuration["web:hide-settings"] ?? "").Split(new[] { ',', ';', ' ' });
        WhiteLabelEnabled = !hideSettings.Contains("WhiteLabel", StringComparer.CurrentCultureIgnoreCase);
        _cache = cache;
        _cacheNotify = cacheNotify;
    }

    public string GetFavicon(bool general, bool timeParam)
    {
        string faviconPath;
        var tenantWhiteLabelSettings = _settingsManager.Load<TenantWhiteLabelSettings>();
        if (WhiteLabelEnabled)
        {
            faviconPath = _tenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.Favicon, general);
            if (timeParam)
            {
                var now = DateTime.Now;
                faviconPath = string.Format("{0}?t={1}", faviconPath, now.Ticks);
            }
        }
        else
        {
            faviconPath = _tenantWhiteLabelSettingsHelper.GetAbsoluteDefaultLogoPath(WhiteLabelLogoTypeEnum.Favicon, general);
        }

        return faviconPath;
    }

    public string GetTopLogo(bool general)//LogoLightSmall
    {
        var tenantWhiteLabelSettings = _settingsManager.Load<TenantWhiteLabelSettings>();

        if (WhiteLabelEnabled)
        {
            return _tenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.LightSmall, general);
        }
        return _tenantWhiteLabelSettingsHelper.GetAbsoluteDefaultLogoPath(WhiteLabelLogoTypeEnum.LightSmall, general);
    }

    public string GetLogoDark(bool general)
    {
        if (WhiteLabelEnabled)
        {
            var tenantWhiteLabelSettings = _settingsManager.Load<TenantWhiteLabelSettings>();
            return _tenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.Dark, general);
        }

        /*** simple scheme ***/
        return _tenantInfoSettingsHelper.GetAbsoluteCompanyLogoPath(_settingsManager.Load<TenantInfoSettings>());
        /***/
    }

    public string GetLogoDocsEditor(bool general)
    {
        var tenantWhiteLabelSettings = _settingsManager.Load<TenantWhiteLabelSettings>();

        if (WhiteLabelEnabled)
        {
            return _tenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.DocsEditor, general);
        }
        return _tenantWhiteLabelSettingsHelper.GetAbsoluteDefaultLogoPath(WhiteLabelLogoTypeEnum.DocsEditor, general);
    }

    public string GetLogoDocsEditorEmbed(bool general)
    {
        var tenantWhiteLabelSettings = _settingsManager.Load<TenantWhiteLabelSettings>();

        if (WhiteLabelEnabled)
        {
            return _tenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.DocsEditorEmbed, general);
        }
        return _tenantWhiteLabelSettingsHelper.GetAbsoluteDefaultLogoPath(WhiteLabelLogoTypeEnum.DocsEditorEmbed, general);
    }


    public string GetLogoText()
    {
        if (WhiteLabelEnabled)
        {
            var tenantWhiteLabelSettings = _settingsManager.Load<TenantWhiteLabelSettings>();

            return tenantWhiteLabelSettings.GetLogoText(_settingsManager) ?? TenantWhiteLabelSettings.DefaultLogoText;
        }
        return TenantWhiteLabelSettings.DefaultLogoText;
    }

    public bool IsRetina(HttpRequest request)
    {
        if (request != null)
        {
            var cookie = request.Cookies["is_retina"];
            if (cookie != null && !string.IsNullOrEmpty(cookie))
            {
                if (bool.TryParse(cookie, out var result))
                {
                    return result;
                }
            }
        }
        return !_authContext.IsAuthenticated;
    }

    public bool WhiteLabelPaid
    {
        get
        {
            return _tenantManager.GetTenantQuota(_tenantManager.GetCurrentTenant().TenantId).WhiteLabel;
        }
    }

    /// <summary>
    /// Get logo stream or null in case of default logo
    /// </summary>
    public Stream GetWhitelabelMailLogo()
    {
        if (WhiteLabelEnabled)
        {
            var tenantWhiteLabelSettings = _settingsManager.Load<TenantWhiteLabelSettings>();
            return _tenantWhiteLabelSettingsHelper.GetWhitelabelLogoData(tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.Dark, true);
        }

        /*** simple scheme ***/
        return _tenantInfoSettingsHelper.GetStorageLogoData(_settingsManager.Load<TenantInfoSettings>());
        /***/
    }


    public byte[] GetMailLogoDataFromCache()
    {
        return _cache.Get<byte[]>(CacheKey);
    }

    public void InsertMailLogoDataToCache(byte[] data)
    {
        _cache.Insert(CacheKey, data, DateTime.UtcNow.Add(TimeSpan.FromDays(1)));
    }

    public void RemoveMailLogoDataFromCache()
    {
        _cacheNotify.Publish(new TenantLogoCacheItem() { Key = CacheKey }, CacheNotifyAction.Remove);
    }
}