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

using ASC.Common.Caching;
using ASC.Core;
using ASC.Core.Common.Notify.Push;

namespace ASC.Web.Core.Mobile;
public class CachedMobileAppInstallRegistrator : IMobileAppInstallRegistrator
{
    private readonly ICache _cache;
    private readonly TimeSpan _cacheExpiration;
    private readonly IMobileAppInstallRegistrator _registrator;
    private readonly TenantManager _tenantManager;

    public CachedMobileAppInstallRegistrator(MobileAppInstallRegistrator registrator, TenantManager tenantManager, ICache cache)
        : this(registrator, TimeSpan.FromMinutes(30), tenantManager, cache)
    {
    }

    public CachedMobileAppInstallRegistrator(MobileAppInstallRegistrator registrator, TimeSpan cacheExpiration, TenantManager tenantManager, ICache cache)
    {
        _cache = cache;
        _tenantManager = tenantManager;
        this._registrator = registrator ?? throw new ArgumentNullException("registrator");
        this._cacheExpiration = cacheExpiration;
    }

    public void RegisterInstall(string userEmail, MobileAppType appType)
    {
        if (string.IsNullOrEmpty(userEmail)) return;
        _registrator.RegisterInstall(userEmail, appType);
        _cache.Insert(GetCacheKey(userEmail, null), true, _cacheExpiration);
        _cache.Insert(GetCacheKey(userEmail, appType), true, _cacheExpiration);
    }

    public bool IsInstallRegistered(string userEmail, MobileAppType? appType)
    {
        if (string.IsNullOrEmpty(userEmail)) return false;

        var fromCache = _cache.Get<string>(GetCacheKey(userEmail, appType));


        if (bool.TryParse(fromCache, out var cachedValue))
        {
            return cachedValue;
        }

        var isRegistered = _registrator.IsInstallRegistered(userEmail, appType);
        _cache.Insert(GetCacheKey(userEmail, appType), isRegistered.ToString(), _cacheExpiration);
        return isRegistered;
    }

    private string GetCacheKey(string userEmail, MobileAppType? appType)
    {
        var cacheKey = appType.HasValue ? userEmail + "/" + appType.ToString() : userEmail;

        return string.Format("{0}:mobile:{1}", _tenantManager.GetCurrentTenant().TenantId, cacheKey);
    }
}