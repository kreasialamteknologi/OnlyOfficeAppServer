/*
 *
 * (c) Copyright Ascensio System Limited 2010-2021
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/


using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ASC.ActiveDirectory.Base.Settings;
using ASC.ActiveDirectory.ComplexOperations;
using ASC.Common.DependencyInjection;
using ASC.Common.Threading;
using ASC.Core;
using ASC.Core.Common.Settings;
using ASC.Core.Tenants;
using ASC.Notify;
using ASC.Notify.Model;

using Autofac;

using Microsoft.Extensions.DependencyInjection;

namespace ASC.ActiveDirectory.Base
{
    public static class LdapNotifyHelper
    {
        private static readonly Dictionary<int, Tuple<INotifyClient, LdapNotifySource>> clients;
        private static readonly DistributedTaskQueue ldapTasks;
        private static TenantManager TenantManager;
        private static SettingsManager SettingsManager;
        private static IContainer Builder { get; set; }
        private static INotifySource studioNotify;
        private static INotifyClient notifyClient;
        private static IServiceProvider _serviceProvider;

        public static INotifyClient StudioNotifyClient
        {
            get
            {
                if (studioNotify == null)
                {
                    studioNotify = Builder.Resolve<INotifySource>();
                }

                using var scope = _serviceProvider.CreateScope();

                if (notifyClient == null)
                {
                    notifyClient = WorkContext.NotifyContext.NotifyService.RegisterClient(studioNotify, scope);
                }

                return notifyClient;
            }
        }

        static LdapNotifyHelper()
        {
            /*
            var container = AutofacConfigLoader.Load("ldap");
            if (container != null)
            {
                Builder = container.Build();
            }
            */
            //TODO: change AutofacConfigLoader

            clients = new Dictionary<int, Tuple<INotifyClient, LdapNotifySource>>();
            ldapTasks = new DistributedTaskQueue("ldapAutoSyncOperations");
        }

        public static void RegisterAll()
        {
            var task = new Task(() =>
            {
                var tenants =  TenantManager.GetTenants(new LdapSettings().GetTenants());
                foreach (var t in tenants)
                {
                    var tId = t.TenantId;

                    var ldapSettings = SettingsManager.LoadForTenant<LdapSettings>(tId);
                    
                    if (!ldapSettings.EnableLdapAuthentication) continue;

                    var cronSettings = SettingsManager.LoadForTenant<LdapCronSettings>(tId);
                    if (string.IsNullOrEmpty(cronSettings.Cron)) continue;

                    RegisterAutoSync(t, cronSettings.Cron);
                }
            }, TaskCreationOptions.LongRunning);

            task.Start();
        }

        public static void RegisterAutoSync(Tenant tenant, string cron)
        {
            if (!clients.ContainsKey(tenant.TenantId))
            {
                var source = new LdapNotifySource(tenant);
                using var scope = _serviceProvider.CreateScope();
                var client = WorkContext.NotifyContext.NotifyService.RegisterClient(source, scope);
                //client.RegisterSendMethod(source.AutoSync, cron); //TODO: no this method
                clients.Add(tenant.TenantId, new Tuple<INotifyClient, LdapNotifySource>(client, source));
            }
        }

        public static void UnregisterAutoSync(Tenant tenant)
        {
            if (clients.ContainsKey(tenant.TenantId))
            {
                var client = clients[tenant.TenantId];
                //client.Item1.UnregisterSendMethod(client.Item2.AutoSync); //TODO: no this method
                clients.Remove(tenant.TenantId);
            }
        }

        public static void AutoSync(Tenant tenant)
        {

            var ldapSettings = SettingsManager.LoadForTenant<LdapSettings>(tenant.TenantId);

            if (!ldapSettings.EnableLdapAuthentication)
            {
                var cronSettings = SettingsManager.LoadForTenant<LdapCronSettings>(tenant.TenantId);
                cronSettings.Cron = "";
                SettingsManager.SaveForTenant(cronSettings, tenant.TenantId);
                UnregisterAutoSync(tenant);
                return;
            }

            var op = new LdapSaveSyncOperation(ldapSettings, tenant, LdapOperationType.Sync);
            ldapTasks.QueueTask(op.RunJob, op.GetDistributedTask());
        }
    }
}
