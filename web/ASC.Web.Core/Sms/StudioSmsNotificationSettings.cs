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
using System.Text.Json.Serialization;

using ASC.Common;
using ASC.Core;
using ASC.Core.Common.Settings;
using ASC.Web.Core.Sms;
using ASC.Web.Studio.Utility;

namespace ASC.Web.Studio.Core.SMS;

[Serializable]
public class StudioSmsNotificationSettings : ISettings
{
    public Guid ID
    {
        get { return new Guid("{2802df61-af0d-40d4-abc5-a8506a5352ff}"); }
    }

    public ISettings GetDefault(IServiceProvider serviceProvider)
    {
        return new StudioSmsNotificationSettings { EnableSetting = false, };
    }

    [JsonPropertyName("Enable")]
    public bool EnableSetting { get; set; }
}

[Scope]
public class StudioSmsNotificationSettingsHelper
{
    private readonly TenantExtra _tenantExtra;
    private readonly CoreBaseSettings _coreBaseSettings;
    private readonly SetupInfo _setupInfo;
    private readonly SettingsManager _settingsManager;
    private readonly SmsProviderManager _smsProviderManager;

    public StudioSmsNotificationSettingsHelper(
        TenantExtra tenantExtra,
        CoreBaseSettings coreBaseSettings,
        SetupInfo setupInfo,
        SettingsManager settingsManager,
        SmsProviderManager smsProviderManager)
    {
        _tenantExtra = tenantExtra;
        _coreBaseSettings = coreBaseSettings;
        _setupInfo = setupInfo;
        _settingsManager = settingsManager;
        _smsProviderManager = smsProviderManager;
    }

    public bool IsVisibleSettings()
    {
        var quota = _tenantExtra.GetTenantQuota();
        return _coreBaseSettings.Standalone
                || ((!quota.Trial || _setupInfo.SmsTrial)
                    && !quota.NonProfit
                    && !quota.Free
                    && !quota.Open);
    }

    public bool Enable
    {
        get { return _settingsManager.Load<StudioSmsNotificationSettings>().EnableSetting && _smsProviderManager.Enabled(); }
        set
        {
            var settings = _settingsManager.Load<StudioSmsNotificationSettings>();
            settings.EnableSetting = value;
            _settingsManager.Save<StudioSmsNotificationSettings>(settings);
        }
    }
}