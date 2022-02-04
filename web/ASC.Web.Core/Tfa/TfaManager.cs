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
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using ASC.Common;
using ASC.Common.Caching;
using ASC.Common.Utils;
using ASC.Core;
using ASC.Core.Common.Security;
using ASC.Core.Common.Settings;
using ASC.Core.Users;
using ASC.Security.Cryptography;
using ASC.Web.Core;
using ASC.Web.Core.PublicResources;

using Google.Authenticator;

using Microsoft.AspNetCore.WebUtilities;

namespace ASC.Web.Studio.Core.TFA;

[Serializable]
public class BackupCode
{
    public bool IsUsed { get; set; }

    public string Code { get; set; }

    public string GetEncryptedCode(InstanceCrypto InstanceCrypto, Signature Signature)
    {
            try
            {
            return InstanceCrypto.Decrypt(Code);
            }
            catch
            {
                //support old scheme stored in the DB
            return Signature.Read<string>(Code);
            }
        }

    public void SetEncryptedCode(InstanceCrypto InstanceCrypto, string code)
    {
        Code = InstanceCrypto.Encrypt(code);
    }
}

[Scope]
public class TfaManager
{
    private static readonly TwoFactorAuthenticator Tfa = new TwoFactorAuthenticator();
    private ICache Cache;

    private readonly SettingsManager _settingsManager;
    private readonly SecurityContext _securityContext;
    private readonly CookiesManager _cookiesManager;
    private readonly SetupInfo _setupInfo;
    private readonly Signature _signature;
    private readonly InstanceCrypto _instanceCrypto;
    private readonly MachinePseudoKeys _machinePseudoKeys;

    public TfaManager(
        SettingsManager settingsManager,
        SecurityContext securityContext,
        CookiesManager cookiesManager,
        SetupInfo setupInfo,
        Signature signature,
        InstanceCrypto instanceCrypto,
        MachinePseudoKeys machinePseudoKeys,
        ICache cache)
    {
        Cache = cache;
        _settingsManager = settingsManager;
        _securityContext = securityContext;
        _cookiesManager = cookiesManager;
        _setupInfo = setupInfo;
        _signature = signature;
        _instanceCrypto = instanceCrypto;
        _machinePseudoKeys = machinePseudoKeys;
    }

    public SetupCode GenerateSetupCode(UserInfo user)
    {
        return Tfa.GenerateSetupCode(_setupInfo.TfaAppSender, user.Email, GenerateAccessToken(user), false, 4);
    }

    public bool ValidateAuthCode(UserInfo user, string code, bool checkBackup = true)
    {
        if (!TfaAppAuthSettings.IsVisibleSettings
            || !_settingsManager.Load<TfaAppAuthSettings>().EnableSetting)
        {
            return false;
        }

        if (user == null || Equals(user, Constants.LostUser)) throw new Exception(Resource.ErrorUserNotFound);

        code = (code ?? "").Trim();

        if (string.IsNullOrEmpty(code)) throw new Exception(Resource.ActivateTfaAppEmptyCode);

        int.TryParse(Cache.Get<string>("tfa/" + user.ID), out var counter);
        if (++counter > _setupInfo.LoginThreshold)
        {
            throw new BruteForceCredentialException(Resource.TfaTooMuchError);
        }
        Cache.Insert("tfa/" + user.ID, counter.ToString(CultureInfo.InvariantCulture), DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)));

        if (!Tfa.ValidateTwoFactorPIN(GenerateAccessToken(user), code))
        {
            if (checkBackup && TfaAppUserSettings.BackupCodesForUser(_settingsManager, user.ID).Any(x => x.GetEncryptedCode(_instanceCrypto, _signature) == code && !x.IsUsed))
            {
                TfaAppUserSettings.DisableCodeForUser(_settingsManager, _instanceCrypto, _signature, user.ID, code);
            }
            else
            {
                throw new ArgumentException(Resource.TfaAppAuthMessageError);
            }
        }

        Cache.Insert("tfa/" + user.ID, (--counter).ToString(CultureInfo.InvariantCulture), DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)));

        if (!_securityContext.IsAuthenticated)
        {
            var cookiesKey = _securityContext.AuthenticateMe(user.ID);
            _cookiesManager.SetCookies(CookiesType.AuthKey, cookiesKey);
        }

        if (!TfaAppUserSettings.EnableForUser(_settingsManager, user.ID))
        {
            GenerateBackupCodes();
            return true;
        }

        return false;
    }

    public IEnumerable<BackupCode> GenerateBackupCodes()
    {
        var count = _setupInfo.TfaAppBackupCodeCount;
        var length = _setupInfo.TfaAppBackupCodeLength;

        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-_";

        var data = new byte[length];

        var list = new List<BackupCode>();

        for (var i = 0; i < count; i++)
        {
            data = RandomNumberGenerator.GetBytes(length);

            var result = new StringBuilder(length);
            foreach (var b in data)
            {
                result.Append(alphabet[b % (alphabet.Length)]);
            }

                var code = new BackupCode();
                code.SetEncryptedCode(_instanceCrypto, result.ToString());
                list.Add(code);
        }
        var settings = _settingsManager.LoadForCurrentUser<TfaAppUserSettings>();
        settings.CodesSetting = list;
        _settingsManager.SaveForCurrentUser(settings);

        return list;
    }

    private string GenerateAccessToken(UserInfo user)
    {
        var userSalt = TfaAppUserSettings.GetSalt(_settingsManager, user.ID);

        //from Signature.Create
        var machineSalt = Encoding.UTF8.GetString(_machinePseudoKeys.GetMachineConstant());
        var token = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(userSalt + machineSalt)));
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        return encodedToken.Substring(0, 10);
    }
}