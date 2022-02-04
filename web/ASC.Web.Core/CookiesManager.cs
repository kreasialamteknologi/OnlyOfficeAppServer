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
using System.Linq;
using System.Security;
using System.Web;

using ASC.Common;
using ASC.Core;
using ASC.Core.Tenants;
using ASC.Core.Users;

using Microsoft.AspNetCore.Http;


using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Web.Core;

public enum CookiesType
{
    AuthKey,
    SocketIO
}

[Scope]
public class CookiesManager
{
    private const string AuthCookiesName = "asc_auth_key";
    private const string SocketIOCookiesName = "socketio.sid";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager _userManager;
    private readonly SecurityContext _securityContext;
    private readonly TenantCookieSettingsHelper _tenantCookieSettingsHelper;
    private readonly TenantManager _tenantManager;
    private readonly CoreBaseSettings _coreBaseSettings;

    public CookiesManager(
        IHttpContextAccessor httpContextAccessor,
        UserManager userManager,
        SecurityContext securityContext,
        TenantCookieSettingsHelper tenantCookieSettingsHelper,
        TenantManager tenantManager,
        CoreBaseSettings coreBaseSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _securityContext = securityContext;
        _tenantCookieSettingsHelper = tenantCookieSettingsHelper;
        _tenantManager = tenantManager;
        _coreBaseSettings = coreBaseSettings;
    }

    private static string GetCookiesName(CookiesType type)
    {
        return type switch
        {
            CookiesType.AuthKey => AuthCookiesName,
            CookiesType.SocketIO => SocketIOCookiesName,

            _ => string.Empty,
        };
    }

    public string GetRequestVar(CookiesType type)
    {
        if (_httpContextAccessor?.HttpContext == null) return "";

        var cookie = _httpContextAccessor.HttpContext.Request.Query[GetCookiesName(type)].FirstOrDefault() ?? _httpContextAccessor.HttpContext.Request.Form[GetCookiesName(type)].FirstOrDefault();

        return string.IsNullOrEmpty(cookie) ? GetCookies(type) : cookie;
    }

    public void SetCookies(CookiesType type, string value, bool session = false)
    {
        if (_httpContextAccessor?.HttpContext == null) return;

        var options = new CookieOptions
        {
            Expires = GetExpiresDate(session)
        };

        if (type == CookiesType.AuthKey)
        {
            options.HttpOnly = true;

            if (_httpContextAccessor.HttpContext.Request.GetUrlRewriter().Scheme == "https")
            {
                options.Secure = true;

                if (_coreBaseSettings.Personal)
                {
                    options.SameSite = SameSiteMode.None;
                }
            }
        }

        _httpContextAccessor.HttpContext.Response.Cookies.Append(GetCookiesName(type), value, options);
    }

    public void SetCookies(CookiesType type, string value, string domain, bool session = false)
    {
        if (_httpContextAccessor?.HttpContext == null) return;

        var options = new CookieOptions
        {
            Expires = GetExpiresDate(session),
            Domain = domain
        };

        if (type == CookiesType.AuthKey)
        {
            options.HttpOnly = true;

            if (_httpContextAccessor.HttpContext.Request.GetUrlRewriter().Scheme == "https")
            {
                options.Secure = true;

                if (_coreBaseSettings.Personal)
                {
                    options.SameSite = SameSiteMode.None;
                }
            }
        }

        _httpContextAccessor.HttpContext.Response.Cookies.Append(GetCookiesName(type), value, options);
    }

    public string GetCookies(CookiesType type)
    {
        if (_httpContextAccessor?.HttpContext != null)
        {
            var cookieName = GetCookiesName(type);

            if (_httpContextAccessor.HttpContext.Request.Cookies.ContainsKey(cookieName))
                return _httpContextAccessor.HttpContext.Request.Cookies[cookieName] ?? "";
        }
        return "";
    }

    public void ClearCookies(CookiesType type)
    {
        if (_httpContextAccessor?.HttpContext == null) return;

        if (_httpContextAccessor.HttpContext.Request.Cookies.ContainsKey(GetCookiesName(type)))
        {
            _httpContextAccessor.HttpContext.Response.Cookies.Delete(GetCookiesName(type), new CookieOptions() { Expires = DateTime.Now.AddDays(-3) });
        }
    }

    private DateTime? GetExpiresDate(bool session)
    {
        DateTime? expires = null;

        if (!session)
        {
            var tenant = _tenantManager.GetCurrentTenant().TenantId;
            expires = _tenantCookieSettingsHelper.GetExpiresTime(tenant);
        }

        return expires;
    }

    public void SetLifeTime(int lifeTime)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        if (!_userManager.IsUserInGroup(_securityContext.CurrentAccount.ID, Constants.GroupAdmin.ID))
        {
            throw new SecurityException();
        }

        var settings = _tenantCookieSettingsHelper.GetForTenant(tenant.TenantId);

        if (lifeTime > 0)
        {
            settings.Index += 1;
            settings.LifeTime = lifeTime;
        }
        else
        {
            settings.LifeTime = 0;
        }

        _tenantCookieSettingsHelper.SetForTenant(tenant.TenantId, settings);

        var cookie = _securityContext.AuthenticateMe(_securityContext.CurrentAccount.ID);

        SetCookies(CookiesType.AuthKey, cookie);
    }

    public int GetLifeTime(int tenantId)
    {
        return _tenantCookieSettingsHelper.GetForTenant(tenantId).LifeTime;
    }

    public void ResetUserCookie(Guid? userId = null)
    {
        var settings = _tenantCookieSettingsHelper.GetForUser(userId ?? _securityContext.CurrentAccount.ID);
        settings.Index += 1;
        _tenantCookieSettingsHelper.SetForUser(userId ?? _securityContext.CurrentAccount.ID, settings);

        if (!userId.HasValue)
        {
            var cookie = _securityContext.AuthenticateMe(_securityContext.CurrentAccount.ID);

            SetCookies(CookiesType.AuthKey, cookie);
        }
    }

    public void ResetTenantCookie()
    {
        var tenant = _tenantManager.GetCurrentTenant();

        if (!_userManager.IsUserInGroup(_securityContext.CurrentAccount.ID, Constants.GroupAdmin.ID))
        {
            throw new SecurityException();
        }

        var settings = _tenantCookieSettingsHelper.GetForTenant(tenant.TenantId);
        settings.Index += 1;
        _tenantCookieSettingsHelper.SetForTenant(tenant.TenantId, settings);

        var cookie = _securityContext.AuthenticateMe(_securityContext.CurrentAccount.ID);
        SetCookies(CookiesType.AuthKey, cookie);
    }
}