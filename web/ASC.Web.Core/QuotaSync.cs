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

using ASC.Common.Threading;
using ASC.Core;
using ASC.Data.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace ASC.Web.Studio.Core.Quota;

public class QuotaSync
{
    private const string TenantIdKey = "tenantID";
    private readonly DistributedTask _taskInfo;
    private readonly int _tenantId;
    private readonly IServiceScopeFactory _scopeFactory;

    public QuotaSync(int tenantId, IServiceScopeFactory scopeFactory)
    {
        _tenantId = tenantId;
        _taskInfo = new DistributedTask();
        _scopeFactory = scopeFactory;
    }

    public void RunJob()//DistributedTask distributedTask, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var scopeClass = scope.ServiceProvider.GetService<QuotaSyncScope>();
        var (tenantManager, storageFactoryConfig, storageFactory) = scopeClass;
        tenantManager.SetCurrentTenant(_tenantId);

        var storageModules = storageFactoryConfig.GetModuleList(string.Empty).ToList();

        foreach (var module in storageModules)
        {
            var storage = storageFactory.GetStorage(_tenantId.ToString(), module);
            storage.ResetQuota("");

            var domains = storageFactoryConfig.GetDomainList(string.Empty, module).ToList();
            foreach (var domain in domains)
            {
                storage.ResetQuota(domain);
            }

        }
    }

    public virtual DistributedTask GetDistributedTask()
    {
        _taskInfo.SetProperty(TenantIdKey, _tenantId);
        return _taskInfo;
    }
}

class QuotaSyncScope
{
    private readonly TenantManager _tenantManager;
    private readonly StorageFactoryConfig _storageFactoryConfig;
    private readonly StorageFactory _storageFactory;

    public QuotaSyncScope(TenantManager tenantManager, StorageFactoryConfig storageFactoryConfig, StorageFactory storageFactory)
    {
        _tenantManager = tenantManager;
        _storageFactoryConfig = storageFactoryConfig;
        _storageFactory = storageFactory;
    }

    public void Deconstruct(out TenantManager tenantManager, out StorageFactoryConfig storageFactoryConfig, out StorageFactory storageFactory)
    {
        tenantManager = _tenantManager;
        storageFactoryConfig = _storageFactoryConfig;
        storageFactory = _storageFactory;
    }
}