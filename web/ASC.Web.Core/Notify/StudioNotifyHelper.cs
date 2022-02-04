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
using System.Linq;

using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Common.Settings;
using ASC.Core.Users;
using ASC.Notify.Model;
using ASC.Notify.Recipients;
using ASC.Web.Core.Utility.Skins;
using ASC.Web.Core.WhiteLabel;
using ASC.Web.Studio.Utility;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ASC.Web.Studio.Core.Notify;

[Scope]
public class StudioNotifyHelper
{
    public readonly string Helplink;
    public readonly StudioNotifySource NotifySource;
    public readonly ISubscriptionProvider SubscriptionProvider;

    private readonly IRecipientProvider _recipientsProvider;
    private readonly int _countMailsToNotActivated;
    private readonly string _notificationImagePath;
    private readonly UserManager _userManager;
    private readonly SettingsManager _settingsManager;
    private readonly CommonLinkUtility _commonLinkUtility;
    private readonly TenantManager _tenantManager;
    private readonly TenantExtra _tenantExtra;
    private readonly CoreBaseSettings _coreBaseSettings;
    private readonly WebImageSupplier _webImageSupplier;
    private readonly ILog _log;

    public StudioNotifyHelper(
        StudioNotifySource studioNotifySource,
        UserManager userManager,
        SettingsManager settingsManager,
        AdditionalWhiteLabelSettingsHelper additionalWhiteLabelSettingsHelper,
        CommonLinkUtility commonLinkUtility,
        TenantManager tenantManager,
        TenantExtra tenantExtra,
        CoreBaseSettings coreBaseSettings,
        WebImageSupplier webImageSupplier,
        IConfiguration configuration,
        IOptionsMonitor<ILog> option)
    {
        Helplink = commonLinkUtility.GetHelpLink(settingsManager, additionalWhiteLabelSettingsHelper, false);
        NotifySource = studioNotifySource;
        this._userManager = userManager;
        _settingsManager = settingsManager;
        _commonLinkUtility = commonLinkUtility;
        _tenantManager = tenantManager;
        _tenantExtra = tenantExtra;
        _coreBaseSettings = coreBaseSettings;
        _webImageSupplier = webImageSupplier;
        SubscriptionProvider = NotifySource.GetSubscriptionProvider();
        _recipientsProvider = NotifySource.GetRecipientsProvider();
        _log = option.CurrentValue;

        int.TryParse(configuration["core:notify:countspam"], out _countMailsToNotActivated);
        _notificationImagePath = configuration["web:notification:image:path"];
    }


    public IEnumerable<UserInfo> GetRecipients(bool toadmins, bool tousers, bool toguests)
    {
        if (toadmins)
        {
            if (tousers)
            {
                if (toguests)
                    return _userManager.GetUsers();

                return _userManager.GetUsers(EmployeeStatus.Default, EmployeeType.User);
            }

            if (toguests)
                return
                    _userManager.GetUsersByGroup(Constants.GroupAdmin.ID)
                                .Concat(_userManager.GetUsers(EmployeeStatus.Default, EmployeeType.Visitor));

            return _userManager.GetUsersByGroup(Constants.GroupAdmin.ID);
        }

        if (tousers)
        {
            if (toguests)
                return _userManager.GetUsers()
                                    .Where(u => !_userManager.IsUserInGroup(u.ID, Constants.GroupAdmin.ID));

            return _userManager.GetUsers(EmployeeStatus.Default, EmployeeType.User)
                                .Where(u => !_userManager.IsUserInGroup(u.ID, Constants.GroupAdmin.ID));
        }

        if (toguests)
            return _userManager.GetUsers(EmployeeStatus.Default, EmployeeType.Visitor);

        return new List<UserInfo>();
    }

    public IRecipient ToRecipient(Guid userId)
    {
        return _recipientsProvider.GetRecipient(userId.ToString());
    }

    public IRecipient[] RecipientFromEmail(string email, bool checkActivation)
    {
        return RecipientFromEmail(new List<string> { email }, checkActivation);
    }

    public IRecipient[] RecipientFromEmail(List<string> emails, bool checkActivation)
    {
        var res = new List<IRecipient>();

        if (emails == null) return res.ToArray();

        res.AddRange(emails.
                            Select(email => email.ToLower()).
                            Select(e => new DirectRecipient(e, null, new[] { e }, checkActivation)));

        if (!checkActivation
            && _countMailsToNotActivated > 0
            && _tenantExtra.Saas && !_coreBaseSettings.Personal)
        {
            var tenant = _tenantManager.GetCurrentTenant();
            var tariff = _tenantManager.GetTenantQuota(tenant.TenantId);
            if (tariff.Free || tariff.Trial)
            {
                var spamEmailSettings = _settingsManager.Load<SpamEmailSettings>();
                var sended = spamEmailSettings.MailsSended;

                var mayTake = Math.Max(0, _countMailsToNotActivated - sended);
                var tryCount = res.Count();
                if (mayTake < tryCount)
                {
                    res = res.Take(mayTake).ToList();

                    _log.Warn(string.Format("Free tenant {0} for today is trying to send {1} more letters without checking activation. Sent {2}", tenant.TenantId, tryCount, mayTake));
                }
                spamEmailSettings.MailsSended = sended + tryCount;
                _settingsManager.Save(spamEmailSettings);
            }
        }

        return res.ToArray();
    }

    public string GetNotificationImageUrl(string imageFileName)
    {
        if (string.IsNullOrEmpty(_notificationImagePath))
        {
            return
                _commonLinkUtility.GetFullAbsolutePath(
                    _webImageSupplier.GetAbsoluteWebPath("notification/" + imageFileName));
        }

        return _notificationImagePath.TrimEnd('/') + "/" + imageFileName;
    }


    public bool IsSubscribedToNotify(Guid userId, INotifyAction notifyAction)
    {
        return IsSubscribedToNotify(ToRecipient(userId), notifyAction);
    }

    public bool IsSubscribedToNotify(IRecipient recipient, INotifyAction notifyAction)
    {
        return recipient != null && SubscriptionProvider.IsSubscribed(_log, notifyAction, recipient, null);
    }

    public void SubscribeToNotify(Guid userId, INotifyAction notifyAction, bool subscribe)
    {
        SubscribeToNotify(ToRecipient(userId), notifyAction, subscribe);
    }

    public void SubscribeToNotify(IRecipient recipient, INotifyAction notifyAction, bool subscribe)
    {
        if (recipient == null) return;

        if (subscribe)
        {
            SubscriptionProvider.Subscribe(notifyAction, null, recipient);
        }
        else
        {
            SubscriptionProvider.UnSubscribe(notifyAction, null, recipient);
        }
    }
}