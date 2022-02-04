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


using ASC.Common;
using ASC.Core.Common.Settings;
using ASC.Web.Core.WhiteLabel;

namespace ASC.Web.Studio.Utility;

[Scope]
public class TenantLogoHelper
{
    private readonly TenantLogoManager _tenantLogoManager;
    private readonly SettingsManager _settingsManager;
    private readonly TenantWhiteLabelSettingsHelper _tenantWhiteLabelSettingsHelper;
    private readonly TenantInfoSettingsHelper _tenantInfoSettingsHelper;

    public TenantLogoHelper(
        TenantLogoManager tenantLogoManager,
        SettingsManager settingsManager,
        TenantWhiteLabelSettingsHelper tenantWhiteLabelSettingsHelper,
        TenantInfoSettingsHelper tenantInfoSettingsHelper)
    {
        _tenantLogoManager = tenantLogoManager;
        _settingsManager = settingsManager;
        _tenantWhiteLabelSettingsHelper = tenantWhiteLabelSettingsHelper;
        _tenantInfoSettingsHelper = tenantInfoSettingsHelper;
    }

    public string GetLogo(WhiteLabelLogoTypeEnum type, bool general = true, bool isDefIfNoWhiteLabel = false)
    {
        string imgUrl;
        if (_tenantLogoManager.WhiteLabelEnabled)
        {
            var _tenantWhiteLabelSettings = _settingsManager.Load<TenantWhiteLabelSettings>();
            return _tenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(_tenantWhiteLabelSettings, type, general);
        }
        else
        {
            if (isDefIfNoWhiteLabel)
            {
                imgUrl = _tenantWhiteLabelSettingsHelper.GetAbsoluteDefaultLogoPath(type, general);
            }
            else
            {
                if (type == WhiteLabelLogoTypeEnum.Dark)
                {
                    /*** simple scheme ***/
                    var _tenantInfoSettings = _settingsManager.Load<TenantInfoSettings>();
                    imgUrl = _tenantInfoSettingsHelper.GetAbsoluteCompanyLogoPath(_tenantInfoSettings);
                    /***/
                }
                else
                {
                    imgUrl = _tenantWhiteLabelSettingsHelper.GetAbsoluteDefaultLogoPath(type, general);
                }
            }
        }

        return imgUrl;

    }
}