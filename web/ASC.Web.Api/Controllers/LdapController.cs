using System;
using System.Diagnostics;
using System.Linq;
using System.Web;

using ASC.ActiveDirectory.Base;
using ASC.ActiveDirectory.Base.Data;
using ASC.ActiveDirectory.Base.Settings;
using ASC.ActiveDirectory.ComplexOperations;
using ASC.Common;
using ASC.Common.Caching;
using ASC.Common.Threading;
using ASC.Core;
using ASC.Core.Billing;
using ASC.Core.Common.Settings;
using ASC.Notify.Cron;
using ASC.Web.Api.Routing;
using ASC.Web.Core.PublicResources;
using ASC.Web.Studio.Core;
using ASC.Web.Studio.Utility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using HttpContext = Microsoft.AspNetCore.Http.HttpContext;

namespace ASC.Api.Settings
{

    public partial class SettingsController
    {
        //private static TenantManager TenantManager { get; set; }
        private new HttpContext HttpContext { get; set; }
        private SecurityContext SecurityContext { get; }
        //private IServiceProvider ServiceProvider { get; }
        //private PermissionContext PermissionContext { get; }
        //protected CoreBaseSettings CoreBaseSettings { get; }
        //private SettingsManager SettingsManager { get; }
        private static DistributedTaskQueueOptionsManager DistributedTaskQueueOptionsManager { get; }

        private static readonly DistributedTaskQueue ldapTasks = DistributedTaskQueueOptionsManager.Get("ldapOperations");

        private readonly ICache Cache;

        /*
        public LdapController(TenantManager tenantManager, HttpContext httpContext, SecurityContext securityContext, IServiceProvider serviceProvider, PermissionContext permissionContext, CoreBaseSettings coreBaseSettings, SettingsManager settingsManager, DistributedTaskQueue distributedTaskQueue, ICache cache)
        {
            TenantManager = tenantManager;
            HttpContext = httpContext;
            SecurityContext = securityContext;
            ServiceProvider = serviceProvider;
            PermissionContext = permissionContext;
            CoreBaseSettings = coreBaseSettings;
            SettingsManager = settingsManager;
            Cache = cache;
        }
        */





        /// <summary>
        /// Returns the current portal LDAP settings.
        /// </summary>
        /// <short>
        /// Get the LDAP settings
        /// </short>
        /// <category>LDAP</category>
        /// <returns>LDAP settings</returns>
        [Read("ldap")]
        public LdapSettings GetLdapSettings()
            {
                CheckLdapPermissions();

                var settings = SettingsManager.Load<LdapSettings>();

                settings = settings.Clone() as LdapSettings; // clone LdapSettings object for clear password (potencial AscCache.Memory issue)

                if (settings == null)
                    return new LdapSettings().GetDefault(ServiceProvider) as LdapSettings;

                settings.Password = null;
                settings.PasswordBytes = null;

                if (settings.IsDefault)
                    return settings;

                var defaultSettings = settings.GetDefault(ServiceProvider);

                if (settings.Equals(defaultSettings))
                    settings.IsDefault = true;

                return settings;
            }

            /// <summary>
            /// Returns the LDAP autosynchronous cron expression of the current portal if it exists.
            /// </summary>
            /// <short>
            /// Get the LDAP cron expression
            /// </short>
            /// <category>LDAP</category>
            /// <returns>Cron expression or null</returns>
            [Read("ldap/cron")]
            public string GetLdapCronSettings()
            {
                CheckLdapPermissions();

                var settings = SettingsManager.Load<LdapCronSettings>();

                if (settings == null)
                    settings = new LdapCronSettings().GetDefault(ServiceProvider) as LdapCronSettings;

                if (string.IsNullOrEmpty(settings.Cron))
                    return null; // TODO: Need to process the response.

            return settings.Cron;
            }

            /// <summary>
            /// Sets the LDAP autosynchronous cron expression of the current portal.
            /// </summary>
            /// <short>
            /// Set the LDAP cron expression
            /// </short>
            /// <category>LDAP</category>
            /// <param name="cron">Cron expression</param>
            [Create("ldap/cron")]
            public void SetLdapCronSettings(string cron)
            {
                CheckLdapPermissions();

                if (!string.IsNullOrEmpty(cron))
                {
                    new CronExpression(cron); // validate
                    
                    if (!SettingsManager.Load<LdapSettings>().EnableLdapAuthentication)
                    {
                        throw new Exception(Resource.LdapSettingsErrorCantSaveLdapSettings);
                    }
                }

                var settings = SettingsManager.Load<LdapCronSettings>();

                if (settings == null)
                    settings = new LdapCronSettings();

                settings.Cron = cron;
                SettingsManager.Save(settings);

                var t = TenantManager.GetCurrentTenant();
                if (!string.IsNullOrEmpty(cron))
                {
                    LdapNotifyHelper.UnregisterAutoSync(t);
                    LdapNotifyHelper.RegisterAutoSync(t, cron);
                }
                else
                {
                    LdapNotifyHelper.UnregisterAutoSync(t);
                }
            }

            /// <summary>
            /// Starts synchronizing users and groups by LDAP.
            /// </summary>
            /// <short>
            /// Synchronize by LDAP
            /// </short>
            /// <category>LDAP</category>
            /// <returns>Operation status</returns>
            [Read("ldap/sync")]
            public LdapOperationStatus SyncLdap()
            {
                CheckLdapPermissions();
                
                var operations = ldapTasks.GetTasks()
                    .Where(t => t.GetProperty<int>(LdapOperation.OWNER) == TenantManager.GetCurrentTenant().TenantId)
                    .ToList();

                var hasStarted = operations.Any(o =>
                {
                    var opType = o.GetProperty<LdapOperationType>(LdapOperation.OPERATION_TYPE);

                    return o.Status <= DistributedTaskStatus.Running &&
                           (opType == LdapOperationType.Sync || opType == LdapOperationType.Save);
                });

                if (hasStarted)
                {
                    return GetLdapOperationStatus();
                }

                if (operations.Any(o => o.Status <= DistributedTaskStatus.Running))
                {
                    return GetStartProcessError();
                }

                var ldapSettings = SettingsManager.Load<LdapSettings>();

                var ldapLocalization = new LdapLocalization(Resource.ResourceManager);

                var tenant = TenantManager.GetCurrentTenant();

                Cache.Insert("REWRITE_URL" + tenant.TenantId, HttpContext.Request.GetUrlRewriter().ToString(), TimeSpan.FromMinutes(5));

                var op = new LdapSaveSyncOperation(ldapSettings, tenant, LdapOperationType.Sync, ldapLocalization, SecurityContext.CurrentAccount.ID.ToString());

                return QueueTask(op);
            }

            /// <summary>
            /// Starts the process of collecting preliminary changes on the portal during the synchronization process according to the selected LDAP settings.
            /// </summary>
            /// <short>
            /// Test the LDAP synchronization
            /// </short>
            /// <category>LDAP</category>
            /// <returns>Operation status</returns>
            [Read("ldap/sync/test")]
            public LdapOperationStatus TestLdapSync()
            {
                CheckLdapPermissions();

                var operations = ldapTasks.GetTasks()
                    .Where(t => t.GetProperty<int>(LdapOperation.OWNER) == TenantManager.GetCurrentTenant().TenantId)
                    .ToList();

                var hasStarted = operations.Any(o =>
                {
                    var opType = o.GetProperty<LdapOperationType>(LdapOperation.OPERATION_TYPE);

                    return o.Status <= DistributedTaskStatus.Running &&
                           (opType == LdapOperationType.SyncTest || opType == LdapOperationType.SaveTest);
                });

                if (hasStarted)
                {
                    return GetLdapOperationStatus();
                }

                if (operations.Any(o => o.Status <= DistributedTaskStatus.Running))
                {
                    return GetStartProcessError();
                }

                var ldapSettings = SettingsManager.Load<LdapSettings>();

                var ldapLocalization = new LdapLocalization(Resource.ResourceManager);

                var tenant = TenantManager.GetCurrentTenant();

                Cache.Insert("REWRITE_URL" + tenant.TenantId, HttpContext.Request.GetUrlRewriter().ToString(), TimeSpan.FromMinutes(5));

                var op = new LdapSaveSyncOperation(ldapSettings, tenant, LdapOperationType.SyncTest, ldapLocalization);

                return QueueTask(op);
            }

            /// <summary>
            /// Saves the LDAP settings specified in the request and starts importing/synchronizing users and groups by LDAP.
            /// </summary>
            /// <short>
            /// Save the LDAP settings
            /// </short>
            /// <category>LDAP</category>
            /// <param name="settings">LDAP settings in the serialized string format</param>
            /// <param name="acceptCertificate">Specifies if the errors of checking certificates are allowed (true) or not (false)</param>
            /// <returns>Operation status</returns>
            [Create("ldap")]
            public LdapOperationStatus SaveLdapSettings(string settings, bool acceptCertificate)
            {
                CheckLdapPermissions();

                var operations = ldapTasks.GetTasks()
                    .Where(t => t.GetProperty<int>(LdapOperation.OWNER) == TenantManager.GetCurrentTenant().TenantId).ToList();

                if (operations.Any(o => o.Status <= DistributedTaskStatus.Running))
                {
                    return GetStartProcessError();
                }

                var ldapSettings = JsonConvert.DeserializeObject<LdapSettings>(settings);

                ldapSettings.AcceptCertificate = acceptCertificate;

                if (!ldapSettings.EnableLdapAuthentication)
                {
                    SetLdapCronSettings(null);
                }

                //ToDo
                ldapSettings.AccessRights.Clear();

                if (!ldapSettings.LdapMapping.ContainsKey(LdapSettings.MappingFields.MailAttribute) || string.IsNullOrEmpty(ldapSettings.LdapMapping[LdapSettings.MappingFields.MailAttribute]))
                {
                    ldapSettings.SendWelcomeEmail = false;
                }

                var ldapLocalization = new LdapLocalization(Resource.ResourceManager, WebstudioNotifyPatternResource.ResourceManager);

                var tenant = TenantManager.GetCurrentTenant();

                Cache.Insert("REWRITE_URL" + tenant.TenantId, HttpContext.Request.GetUrlRewriter().ToString(), TimeSpan.FromMinutes(5));

                var op = new LdapSaveSyncOperation(ldapSettings, tenant, LdapOperationType.Save, ldapLocalization, SecurityContext.CurrentAccount.ID.ToString());

                return QueueTask(op);
            }

            /// <summary>
            /// Starts the process of collecting preliminary changes on the portal during the saving process according to the LDAP settings.
            /// </summary>
            /// <short>
            /// Test the LDAP saving process
            /// </short>
            /// <category>LDAP</category>
            /// <param name="settings">LDAP settings in the serialized string format</param>
            /// <param name="acceptCertificate">Specifies if the errors of checking certificates are allowed (true) or not (false)</param>
            /// <returns>Operation status</returns>
            [Create("ldap/save/test")]
            public LdapOperationStatus TestLdapSave(string settings, bool acceptCertificate)
            {
                CheckLdapPermissions();

                var operations = ldapTasks.GetTasks()
                    .Where(t => t.GetProperty<int>(LdapOperation.OWNER) == TenantManager.GetCurrentTenant().TenantId)
                    .ToList();

                var hasStarted = operations.Any(o =>
                {
                    var opType = o.GetProperty<LdapOperationType>(LdapOperation.OPERATION_TYPE);

                    return o.Status <= DistributedTaskStatus.Running &&
                           (opType == LdapOperationType.SyncTest || opType == LdapOperationType.SaveTest);
                });

                if (hasStarted)
                {
                    return GetLdapOperationStatus();
                }

                if (operations.Any(o => o.Status <= DistributedTaskStatus.Running))
                {
                    return GetStartProcessError();
                }

                var ldapSettings = JsonConvert.DeserializeObject<LdapSettings>(settings);

                ldapSettings.AcceptCertificate = acceptCertificate;

                var ldapLocalization = new LdapLocalization(Resource.ResourceManager);

                var tenant = TenantManager.GetCurrentTenant();

                Cache.Insert("REWRITE_URL" + tenant.TenantId, HttpContext.Request.GetUrlRewriter().ToString(), TimeSpan.FromMinutes(5));

                var op = new LdapSaveSyncOperation(ldapSettings, tenant, LdapOperationType.SaveTest, ldapLocalization, SecurityContext.CurrentAccount.ID.ToString());

                return QueueTask(op);
            }

            /// <summary>
            /// Returns the LDAP synchronization process status.
            /// </summary>
            /// <short>
            /// Get the LDAP synchronization process status
            /// </short>
            /// <category>LDAP</category>
            /// <returns>Operation status</returns>
            [Read("ldap/status")]
            public LdapOperationStatus GetLdapOperationStatus()
            {
                CheckLdapPermissions();

                return ToLdapOperationStatus();
            }

            /// <summary>
            /// Returns the LDAP default settings.
            /// </summary>
            /// <short>
            /// Get the LDAP default settings
            /// </short>
            /// <category>LDAP</category>
            /// <returns>LDAP default settings</returns>
            [Read("ldap/default")]
            public LdapSettings GetDefaultLdapSettings()
            {
                CheckLdapPermissions();

                return new LdapSettings().GetDefault(ServiceProvider) as LdapSettings;
            }

            private LdapOperationStatus ToLdapOperationStatus()
            {
                var operations = ldapTasks.GetTasks().ToList();

                foreach (var o in operations)
                {
                    if (!string.IsNullOrEmpty(o.InstanceId.ToString()) &&
                        Process.GetProcesses().Any(p => p.Id == o.InstanceId))
                        continue;

                    o.SetProperty(LdapOperation.PROGRESS, 100);
                    ldapTasks.RemoveTask(o.Id);
                }

                var operation =
                    operations
                        .FirstOrDefault(t => t.GetProperty<int>(LdapOperation.OWNER) == TenantManager.GetCurrentTenant().TenantId);

                if (operation == null)
                {
                    return null;
                }

                if (DistributedTaskStatus.Running < operation.Status)
                {
                    operation.SetProperty(LdapOperation.PROGRESS, 100);
                    ldapTasks.RemoveTask(operation.Id);
                }

                var certificateConfirmRequest = operation.GetProperty<LdapCertificateConfirmRequest>(LdapOperation.CERT_REQUEST);

                var result = new LdapOperationStatus
                {
                    Id = operation.Id,
                    Completed = operation.GetProperty<bool>(LdapOperation.FINISHED),
                    Percents = operation.GetProperty<int>(LdapOperation.PROGRESS),
                    Status = operation.GetProperty<string>(LdapOperation.RESULT),
                    Error = operation.GetProperty<string>(LdapOperation.ERROR),
                    CertificateConfirmRequest = certificateConfirmRequest,
                    Source = operation.GetProperty<string>(LdapOperation.SOURCE),
                    OperationType = Enum.GetName(typeof(LdapOperationType),
                        (LdapOperationType)Convert.ToInt32(operation.GetProperty<string>(LdapOperation.OPERATION_TYPE))),
                    Warning = operation.GetProperty<string>(LdapOperation.WARNING)
                };

                if (!(string.IsNullOrEmpty(result.Warning)))
                { 
                    operation.SetProperty(LdapOperation.WARNING, ""); // "mark" as read
                }

                return result;
            }

            private void CheckLdapPermissions()
            {
            
                PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

                if (!CoreBaseSettings.Standalone
                    && (!SetupInfo.IsVisibleSettings(ManagementType.LdapSettings.ToString())
                        || !TenantManager.GetTenantQuota(TenantManager.GetCurrentTenant().TenantId).Ldap))
                {
                    throw new BillingException(Resource.ErrorNotAllowedOption, "Ldap");
                }
            }

            private LdapOperationStatus QueueTask(LdapOperation op)
            {
                ldapTasks.QueueTask(op.RunJob, op.GetDistributedTask());
                return ToLdapOperationStatus();
            }

            private LdapOperationStatus GetStartProcessError()
            {
                var result = new LdapOperationStatus
                {
                    Id = null,
                    Completed = true,
                    Percents = 0,
                    Status = "",
                    Error = Resource.LdapSettingsTooManyOperations,
                    CertificateConfirmRequest = null,
                    Source = ""
                };

                return result;
            }
        }

    }
