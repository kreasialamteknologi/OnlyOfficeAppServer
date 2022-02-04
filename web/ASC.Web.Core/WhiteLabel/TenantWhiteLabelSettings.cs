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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Common.Settings;
using ASC.Core.Common.WhiteLabel;
using ASC.Core.Tenants;
using ASC.Data.Storage;
using ASC.Web.Core.Users;
using ASC.Web.Core.Utility.Skins;

using Microsoft.Extensions.Options;

using SixLabors.ImageSharp;

using TMResourceData;

using UnknownImageFormatException = SixLabors.ImageSharp.UnknownImageFormatException;

namespace ASC.Web.Core.WhiteLabel;

[Serializable]
public class TenantWhiteLabelSettings : ISettings
{
    public const string DefaultLogoText = BaseWhiteLabelSettings.DefaultLogoText;

    #region Logos information: extension, isDefault, text for img auto generating

    internal string _logoLightSmallExt;

    [JsonPropertyName("DefaultLogoLightSmall")]
    internal bool _isDefaultLogoLightSmall;

    internal string _logoDarkExt;

    [JsonPropertyName("DefaultLogoDark")]
    internal bool _isDefaultLogoDark;

    internal string _logoFaviconExt;

    [JsonPropertyName("DefaultLogoFavicon")]
    internal bool _isDefaultLogoFavicon;

    internal string _logoDocsEditorExt;

    [JsonPropertyName("DefaultLogoDocsEditor")]
    internal bool _isDefaultLogoDocsEditor;

    internal string _logoDocsEditorEmbedExt;

    [JsonPropertyName("DefaultLogoDocsEditorEmbed")]
    internal bool _isDefaultLogoDocsEditorEmbed;

    public string LogoText { get; set; }

    public string GetLogoText(SettingsManager settingsManager)
    {
        if (!string.IsNullOrEmpty(LogoText) && LogoText != DefaultLogoText)
            return LogoText;

        var partnerSettings = settingsManager.LoadForDefaultTenant<TenantWhiteLabelSettings>();
        return string.IsNullOrEmpty(partnerSettings.LogoText) ? DefaultLogoText : partnerSettings.LogoText;
    }

    public void SetLogoText(string val)
    {
        LogoText = val;
    }

    #endregion

    #region Logo available sizes

    public static readonly Size logoLightSmallSize = new Size(284, 46);
    public static readonly Size logoDarkSize = new Size(432, 70);
    public static readonly Size logoFaviconSize = new Size(32, 32);
    public static readonly Size logoDocsEditorSize = new Size(172, 40);
    public static readonly Size logoDocsEditorEmbedSize = new Size(172, 40);

    #endregion

    #region ISettings Members

    public ISettings GetDefault(IServiceProvider serviceProvider)
    {
        return new TenantWhiteLabelSettings
        {
            _logoLightSmallExt = null,
            _logoDarkExt = null,
            _logoFaviconExt = null,
            _logoDocsEditorExt = null,
            _logoDocsEditorEmbedExt = null,

            _isDefaultLogoLightSmall = true,
            _isDefaultLogoDark = true,
            _isDefaultLogoFavicon = true,
            _isDefaultLogoDocsEditor = true,
            _isDefaultLogoDocsEditorEmbed = true,

            LogoText = null
        };
    }
    #endregion

    public Guid ID
    {
        get { return new Guid("{05d35540-c80b-4b17-9277-abd9e543bf93}"); }
    }

    #region Get/Set IsDefault and Extension

    internal bool GetIsDefault(WhiteLabelLogoTypeEnum type)
    {
        return type switch
        {
            WhiteLabelLogoTypeEnum.LightSmall => _isDefaultLogoLightSmall,
            WhiteLabelLogoTypeEnum.Dark => _isDefaultLogoDark,
            WhiteLabelLogoTypeEnum.Favicon => _isDefaultLogoFavicon,
            WhiteLabelLogoTypeEnum.DocsEditor => _isDefaultLogoDocsEditor,
            WhiteLabelLogoTypeEnum.DocsEditorEmbed => _isDefaultLogoDocsEditorEmbed,
            _ => true,
        };
    }

    internal void SetIsDefault(WhiteLabelLogoTypeEnum type, bool value)
    {
        switch (type)
        {
            case WhiteLabelLogoTypeEnum.LightSmall:
                _isDefaultLogoLightSmall = value;
                break;
            case WhiteLabelLogoTypeEnum.Dark:
                _isDefaultLogoDark = value;
                break;
            case WhiteLabelLogoTypeEnum.Favicon:
                _isDefaultLogoFavicon = value;
                break;
            case WhiteLabelLogoTypeEnum.DocsEditor:
                _isDefaultLogoDocsEditor = value;
                break;
            case WhiteLabelLogoTypeEnum.DocsEditorEmbed:
                _isDefaultLogoDocsEditorEmbed = value;
                break;
        }
    }

    internal string GetExt(WhiteLabelLogoTypeEnum type)
    {
        return type switch
        {
            WhiteLabelLogoTypeEnum.LightSmall => _logoLightSmallExt,
            WhiteLabelLogoTypeEnum.Dark => _logoDarkExt,
            WhiteLabelLogoTypeEnum.Favicon => _logoFaviconExt,
            WhiteLabelLogoTypeEnum.DocsEditor => _logoDocsEditorExt,
            WhiteLabelLogoTypeEnum.DocsEditorEmbed => _logoDocsEditorEmbedExt,
            _ => "",
        };
    }

    internal void SetExt(WhiteLabelLogoTypeEnum type, string fileExt)
    {
        switch (type)
        {
            case WhiteLabelLogoTypeEnum.LightSmall:
                _logoLightSmallExt = fileExt;
                break;
            case WhiteLabelLogoTypeEnum.Dark:
                _logoDarkExt = fileExt;
                break;
            case WhiteLabelLogoTypeEnum.Favicon:
                _logoFaviconExt = fileExt;
                break;
            case WhiteLabelLogoTypeEnum.DocsEditor:
                _logoDocsEditorExt = fileExt;
                break;
            case WhiteLabelLogoTypeEnum.DocsEditorEmbed:
                _logoDocsEditorEmbedExt = fileExt;
                break;
        }
    }

    #endregion
}

[Scope]
public class TenantWhiteLabelSettingsHelper
{
    private const string moduleName = "whitelabel";

    private readonly WebImageSupplier _webImageSupplier;
    private readonly UserPhotoManager _userPhotoManager;
    private readonly StorageFactory _storageFactory;
    private readonly WhiteLabelHelper _whiteLabelHelper;
    private readonly TenantManager _tenantManager;
    private readonly SettingsManager _settingsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILog _log;

    public TenantWhiteLabelSettingsHelper(
        WebImageSupplier webImageSupplier,
        UserPhotoManager userPhotoManager,
        StorageFactory storageFactory,
        WhiteLabelHelper whiteLabelHelper,
        TenantManager tenantManager,
        SettingsManager settingsManager,
        IServiceProvider serviceProvider,
        IOptionsMonitor<ILog> option)
    {
        _webImageSupplier = webImageSupplier;
        _userPhotoManager = userPhotoManager;
        _storageFactory = storageFactory;
        _whiteLabelHelper = whiteLabelHelper;
        _tenantManager = tenantManager;
        _settingsManager = settingsManager;
        _serviceProvider = serviceProvider;
        _log = option.CurrentValue;
    }

    #region Restore default

    public bool IsDefault(TenantWhiteLabelSettings tenantWhiteLabelSettings)
    {
        var defaultSettings = tenantWhiteLabelSettings.GetDefault(_serviceProvider) as TenantWhiteLabelSettings;

        if (defaultSettings == null) return false;

        return tenantWhiteLabelSettings._logoLightSmallExt == defaultSettings._logoLightSmallExt &&
                tenantWhiteLabelSettings._logoDarkExt == defaultSettings._logoDarkExt &&
                tenantWhiteLabelSettings._logoFaviconExt == defaultSettings._logoFaviconExt &&
                tenantWhiteLabelSettings._logoDocsEditorExt == defaultSettings._logoDocsEditorExt &&
                tenantWhiteLabelSettings._logoDocsEditorEmbedExt == defaultSettings._logoDocsEditorEmbedExt &&

                tenantWhiteLabelSettings._isDefaultLogoLightSmall == defaultSettings._isDefaultLogoLightSmall &&
                tenantWhiteLabelSettings._isDefaultLogoDark == defaultSettings._isDefaultLogoDark &&
                tenantWhiteLabelSettings._isDefaultLogoFavicon == defaultSettings._isDefaultLogoFavicon &&
                tenantWhiteLabelSettings._isDefaultLogoDocsEditor == defaultSettings._isDefaultLogoDocsEditor &&
                tenantWhiteLabelSettings._isDefaultLogoDocsEditorEmbed == defaultSettings._isDefaultLogoDocsEditorEmbed &&

                tenantWhiteLabelSettings.LogoText == defaultSettings.LogoText;
    }

    public void RestoreDefault(TenantWhiteLabelSettings tenantWhiteLabelSettings, TenantLogoManager tenantLogoManager, int tenantId, IDataStore storage = null)
    {
        tenantWhiteLabelSettings._logoLightSmallExt = null;
        tenantWhiteLabelSettings._logoDarkExt = null;
        tenantWhiteLabelSettings._logoFaviconExt = null;
        tenantWhiteLabelSettings._logoDocsEditorExt = null;
        tenantWhiteLabelSettings._logoDocsEditorEmbedExt = null;

        tenantWhiteLabelSettings._isDefaultLogoLightSmall = true;
        tenantWhiteLabelSettings._isDefaultLogoDark = true;
        tenantWhiteLabelSettings._isDefaultLogoFavicon = true;
        tenantWhiteLabelSettings._isDefaultLogoDocsEditor = true;
        tenantWhiteLabelSettings._isDefaultLogoDocsEditorEmbed = true;

        tenantWhiteLabelSettings.SetLogoText(null);

        var store = storage ?? _storageFactory.GetStorage(tenantId.ToString(), moduleName);

        try
        {
            store.DeleteFiles("", "*", false);
        }
        catch (Exception e)
        {
            _log.Error(e);
        }

        Save(tenantWhiteLabelSettings, tenantId, tenantLogoManager, true);
    }

    public void RestoreDefault(TenantWhiteLabelSettings tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum type)
    {
        if (!tenantWhiteLabelSettings.GetIsDefault(type))
        {
            try
            {
                tenantWhiteLabelSettings.SetIsDefault(type, true);
                var store = _storageFactory.GetStorage(_tenantManager.GetCurrentTenant().TenantId.ToString(), moduleName);
                DeleteLogoFromStore(tenantWhiteLabelSettings, store, type);
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }
    }

    #endregion

    #region Set logo

    public void SetLogo(TenantWhiteLabelSettings tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum type, string logoFileExt, byte[] data, IDataStore storage = null)
    {
        var store = storage ?? _storageFactory.GetStorage(_tenantManager.GetCurrentTenant().TenantId.ToString(), moduleName);

        #region delete from storage if already exists

        var isAlreadyHaveBeenChanged = !tenantWhiteLabelSettings.GetIsDefault(type);

        if (isAlreadyHaveBeenChanged)
        {
            try
            {
                DeleteLogoFromStore(tenantWhiteLabelSettings, store, type);
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }
        #endregion

        using (var memory = new MemoryStream(data))
        using (var image = Image.Load(memory))
        {
            var logoSize = image.Size();
            var logoFileName = BuildLogoFileName(type, logoFileExt, false);

            memory.Seek(0, SeekOrigin.Begin);
            store.Save(logoFileName, memory);
        }

        tenantWhiteLabelSettings.SetExt(type, logoFileExt);
        tenantWhiteLabelSettings.SetIsDefault(type, false);

        var generalSize = GetSize(type, true);
        var generalFileName = BuildLogoFileName(type, logoFileExt, true);
        ResizeLogo(generalFileName, data, -1, generalSize, store);
    }

    public void SetLogo(TenantWhiteLabelSettings tenantWhiteLabelSettings, Dictionary<int, string> logo, IDataStore storage = null)
    {
        var xStart = @"data:image/png;base64,";

        foreach (var currentLogo in logo)
        {
            var currentLogoType = (WhiteLabelLogoTypeEnum)(currentLogo.Key);
            var currentLogoPath = currentLogo.Value;

            if (!string.IsNullOrEmpty(currentLogoPath))
            {
                var fileExt = "png";
                byte[] data;
                if (!currentLogoPath.StartsWith(xStart))
                {
                    var fileName = Path.GetFileName(currentLogoPath);
                    fileExt = fileName.Split('.').Last();
                    data = _userPhotoManager.GetTempPhotoData(fileName);
                    try
                    {
                        _userPhotoManager.RemoveTempPhoto(fileName);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex);
                    }
                }
                else
                {
                    var xB64 = currentLogoPath.Substring(xStart.Length); // Get the Base64 string
                    data = System.Convert.FromBase64String(xB64); // Convert the Base64 string to binary data
                }

                if (data != null)
                {
                    SetLogo(tenantWhiteLabelSettings, currentLogoType, fileExt, data, storage);
                }
            }
        }
    }

    public void SetLogoFromStream(TenantWhiteLabelSettings tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum type, string fileExt, Stream fileStream, IDataStore storage = null)
    {
        byte[] data = null;
        using (var memoryStream = new MemoryStream())
        {
            fileStream.CopyTo(memoryStream);
            data = memoryStream.ToArray();
        }

        if (data != null)
        {
            SetLogo(tenantWhiteLabelSettings, type, fileExt, data, storage);
        }
    }

    #endregion

    #region Get logo path

    public string GetAbsoluteLogoPath(TenantWhiteLabelSettings tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum type, bool general = true)
    {
        if (tenantWhiteLabelSettings.GetIsDefault(type))
        {
            return GetAbsoluteDefaultLogoPath(type, general);
        }

        return GetAbsoluteStorageLogoPath(tenantWhiteLabelSettings, type, general);
    }

    private string GetAbsoluteStorageLogoPath(TenantWhiteLabelSettings tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum type, bool general)
    {
        var store = _storageFactory.GetStorage(_tenantManager.GetCurrentTenant().TenantId.ToString(), moduleName);
        var fileName = BuildLogoFileName(type, tenantWhiteLabelSettings.GetExt(type), general);

        if (store.IsFile(fileName))
        {
            return store.GetUri(fileName).ToString();
        }
        return GetAbsoluteDefaultLogoPath(type, general);
    }

    public string GetAbsoluteDefaultLogoPath(WhiteLabelLogoTypeEnum type, bool general)
    {
        var partnerLogoPath = GetPartnerStorageLogoPath(type, general);
        if (!string.IsNullOrEmpty(partnerLogoPath))
            return partnerLogoPath;

        return type switch
        {
            WhiteLabelLogoTypeEnum.LightSmall => general ? _webImageSupplier.GetAbsoluteWebPath("logo/light_small_general.svg") : _webImageSupplier.GetAbsoluteWebPath("logo/light_small.svg"),
            WhiteLabelLogoTypeEnum.Dark => general ? _webImageSupplier.GetAbsoluteWebPath("logo/dark_general.png") : _webImageSupplier.GetAbsoluteWebPath("logo/dark.png"),
            WhiteLabelLogoTypeEnum.DocsEditor => general ? _webImageSupplier.GetAbsoluteWebPath("logo/editor_logo_general.png") : _webImageSupplier.GetAbsoluteWebPath("logo/editor_logo.png"),
            WhiteLabelLogoTypeEnum.DocsEditorEmbed => general ? _webImageSupplier.GetAbsoluteWebPath("logo/editor_logo_embed_general.png") : _webImageSupplier.GetAbsoluteWebPath("logo/editor_logo_embed.png"),
            WhiteLabelLogoTypeEnum.Favicon => general ? _webImageSupplier.GetAbsoluteWebPath("logo/favicon_general.ico") : _webImageSupplier.GetAbsoluteWebPath("logo/favicon.ico"),
            _ => "",
        };
    }

    private string GetPartnerStorageLogoPath(WhiteLabelLogoTypeEnum type, bool general)
    {
        var partnerSettings = _settingsManager.LoadForDefaultTenant<TenantWhiteLabelSettings>();

        if (partnerSettings.GetIsDefault(type)) return null;

        var partnerStorage = _storageFactory.GetStorage(string.Empty, "static_partnerdata");

        if (partnerStorage == null) return null;

        var logoPath = BuildLogoFileName(type, partnerSettings.GetExt(type), general);

        return partnerStorage.IsFile(logoPath) ? partnerStorage.GetUri(logoPath).ToString() : null;
    }

    #endregion

    #region Get Whitelabel Logo Stream

    /// <summary>
    /// Get logo stream or null in case of default whitelabel
    /// </summary>
    public Stream GetWhitelabelLogoData(TenantWhiteLabelSettings tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum type, bool general)
    {
        if (tenantWhiteLabelSettings.GetIsDefault(type))
            return GetPartnerStorageLogoData(type, general);

        return GetStorageLogoData(tenantWhiteLabelSettings, type, general);
    }

    private Stream GetStorageLogoData(TenantWhiteLabelSettings tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum type, bool general)
    {
        var storage = _storageFactory.GetStorage(_tenantManager.GetCurrentTenant().TenantId.ToString(CultureInfo.InvariantCulture), moduleName);

        if (storage == null) return null;

        var fileName = BuildLogoFileName(type, tenantWhiteLabelSettings.GetExt(type), general);

        return storage.IsFile(fileName) ? storage.GetReadStream(fileName) : null;
    }

    private Stream GetPartnerStorageLogoData(WhiteLabelLogoTypeEnum type, bool general)
    {
        var partnerSettings = _settingsManager.LoadForDefaultTenant<TenantWhiteLabelSettings>();

        if (partnerSettings.GetIsDefault(type)) return null;

        var partnerStorage = _storageFactory.GetStorage(string.Empty, "static_partnerdata");

        if (partnerStorage == null) return null;

        var fileName = BuildLogoFileName(type, partnerSettings.GetExt(type), general);

        return partnerStorage.IsFile(fileName) ? partnerStorage.GetReadStream(fileName) : null;
    }

    #endregion

    public static string BuildLogoFileName(WhiteLabelLogoTypeEnum type, string fileExt, bool general)
    {
        return string.Format("logo_{0}{2}.{1}", type.ToString().ToLowerInvariant(), fileExt, general ? "_general" : "");
    }

    public static Size GetSize(WhiteLabelLogoTypeEnum type, bool general)
    {
        return type switch
        {
            WhiteLabelLogoTypeEnum.LightSmall => new Size(
                    general ? TenantWhiteLabelSettings.logoLightSmallSize.Width / 2 : TenantWhiteLabelSettings.logoLightSmallSize.Width,
                    general ? TenantWhiteLabelSettings.logoLightSmallSize.Height / 2 : TenantWhiteLabelSettings.logoLightSmallSize.Height),
            WhiteLabelLogoTypeEnum.Dark => new Size(
                    general ? TenantWhiteLabelSettings.logoDarkSize.Width / 2 : TenantWhiteLabelSettings.logoDarkSize.Width,
                    general ? TenantWhiteLabelSettings.logoDarkSize.Height / 2 : TenantWhiteLabelSettings.logoDarkSize.Height),
            WhiteLabelLogoTypeEnum.Favicon => new Size(
                    general ? TenantWhiteLabelSettings.logoFaviconSize.Width / 2 : TenantWhiteLabelSettings.logoFaviconSize.Width,
                    general ? TenantWhiteLabelSettings.logoFaviconSize.Height / 2 : TenantWhiteLabelSettings.logoFaviconSize.Height),
            WhiteLabelLogoTypeEnum.DocsEditor => new Size(
                    general ? TenantWhiteLabelSettings.logoDocsEditorSize.Width / 2 : TenantWhiteLabelSettings.logoDocsEditorSize.Width,
                    general ? TenantWhiteLabelSettings.logoDocsEditorSize.Height / 2 : TenantWhiteLabelSettings.logoDocsEditorSize.Height),
            WhiteLabelLogoTypeEnum.DocsEditorEmbed => new Size(
                    general ? TenantWhiteLabelSettings.logoDocsEditorEmbedSize.Width / 2 : TenantWhiteLabelSettings.logoDocsEditorEmbedSize.Width,
                    general ? TenantWhiteLabelSettings.logoDocsEditorEmbedSize.Height / 2 : TenantWhiteLabelSettings.logoDocsEditorEmbedSize.Height),
            _ => new Size(0, 0),
        };
    }

    private static void ResizeLogo(string fileName, byte[] data, long maxFileSize, Size size, IDataStore store)
    {
        //Resize synchronously
        if (data == null || data.Length <= 0) throw new UnknownImageFormatException("data null");
        if (maxFileSize != -1 && data.Length > maxFileSize) throw new ImageWeightLimitException();

        try
        {
            using var stream = new MemoryStream(data);
            using var img = Image.Load(stream, out var format);
            var imgFormat = format;
            if (size != img.Size())
            {
                using var img2 = CommonPhotoManager.DoThumbnail(img, size, false, true, false);
                data = CommonPhotoManager.SaveToBytes(img2);
            }
            else
            {
                data = CommonPhotoManager.SaveToBytes(img);
            }

            //fileExt = CommonPhotoManager.GetImgFormatName(imgFormat);

            using var stream2 = new MemoryStream(data);
            store.Save(fileName, stream2);
        }
        catch (ArgumentException error)
        {
            throw new UnknownImageFormatException(error.Message);
        }
    }

    #region Save for Resource replacement

    private static readonly List<int> AppliedTenants = new List<int>();

    public void Apply(TenantWhiteLabelSettings tenantWhiteLabelSettings, int tenantId)
    {
        if (AppliedTenants.Contains(tenantId)) return;

        SetNewLogoText(tenantWhiteLabelSettings, tenantId);

        if (!AppliedTenants.Contains(tenantId)) AppliedTenants.Add(tenantId);
    }

    public void Save(TenantWhiteLabelSettings tenantWhiteLabelSettings, int tenantId, TenantLogoManager tenantLogoManager, bool restore = false)
    {
        _settingsManager.SaveForTenant(tenantWhiteLabelSettings, tenantId);

        if (tenantId == Tenant.DEFAULT_TENANT)
        {
            AppliedTenants.Clear();
        }
        else
        {
            SetNewLogoText(tenantWhiteLabelSettings, tenantId, restore);
            tenantLogoManager.RemoveMailLogoDataFromCache();
        }
    }

    private void SetNewLogoText(TenantWhiteLabelSettings tenantWhiteLabelSettings, int tenantId, bool restore = false)
    {
        _whiteLabelHelper.DefaultLogoText = TenantWhiteLabelSettings.DefaultLogoText;
        var partnerSettings = _settingsManager.LoadForDefaultTenant<TenantWhiteLabelSettings>();

        if (restore && string.IsNullOrEmpty(partnerSettings.GetLogoText(_settingsManager)))
        {
            _whiteLabelHelper.RestoreOldText(tenantId);
        }
        else
        {
            _whiteLabelHelper.SetNewText(tenantId, tenantWhiteLabelSettings.GetLogoText(_settingsManager));
        }
    }

    #endregion

    #region Delete from Store

    private void DeleteLogoFromStore(TenantWhiteLabelSettings tenantWhiteLabelSettings, IDataStore store, WhiteLabelLogoTypeEnum type)
    {
        DeleteLogoFromStoreByGeneral(tenantWhiteLabelSettings, store, type, false);
        DeleteLogoFromStoreByGeneral(tenantWhiteLabelSettings, store, type, true);
    }

    private void DeleteLogoFromStoreByGeneral(TenantWhiteLabelSettings tenantWhiteLabelSettings, IDataStore store, WhiteLabelLogoTypeEnum type, bool general)
    {
        var fileExt = tenantWhiteLabelSettings.GetExt(type);
        var logo = BuildLogoFileName(type, fileExt, general);
        if (store.IsFile(logo))
        {
            store.Delete(logo);
        }
    }

    #endregion
}