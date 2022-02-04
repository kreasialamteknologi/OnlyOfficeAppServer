using ASC.Common;
using ASC.Common.Threading;
using ASC.Core;
using ASC.Core.Common.Settings;
using ASC.Core.Users;
using ASC.Web.Core;
using ASC.Web.Core.Users;

namespace ASC.ActiveDirectory
{
    [Scope]
    public class LdapOperationScope
    {
        private TenantManager TenantManager { get; set; }
        private UserManager UserManager { get; set; }
        private CoreBaseSettings CoreBaseSettings { get; set; }
        private SettingsManager SettingsManager { get; set; }
        private UserFormatter UserFormatter { get; set; }
        private WebItemSecurity WebItemSecurity { get; set; }
        private UserPhotoManager UserPhotoManager { get; set; }

        public LdapOperationScope(TenantManager tenantManager, UserManager userManager, CoreBaseSettings coreBaseSettings, SettingsManager settingsManager, UserFormatter userFormatter, WebItemSecurity webItemSecurity, UserPhotoManager userPhotoManager)
        {
            TenantManager = tenantManager;
            UserManager = userManager;
            CoreBaseSettings = coreBaseSettings;
            SettingsManager = settingsManager;
            UserFormatter = userFormatter;
            WebItemSecurity = webItemSecurity;
            UserPhotoManager = userPhotoManager;
        }


        public void Init(TenantManager tenantManager, UserManager userManager, CoreBaseSettings coreBaseSettings, SettingsManager settingsManager, UserFormatter userFormatter, WebItemSecurity webItemSecurity, UserPhotoManager userPhotoManager)
        {
            TenantManager = tenantManager;
            UserManager = userManager;
            CoreBaseSettings = coreBaseSettings;
            SettingsManager = settingsManager;
            UserFormatter = userFormatter;
            WebItemSecurity = webItemSecurity;
            UserPhotoManager = userPhotoManager;
        }

        public void Deconstruct(out TenantManager tenantManager,
            out UserManager userManager,
            out CoreBaseSettings coreBaseSettings,
            out SettingsManager settingsManager, 
            out UserFormatter userFormatter, 
            out WebItemSecurity webItemSecurity, 
            out UserPhotoManager userPhotoManager)
        {
            tenantManager = TenantManager;
            userManager = UserManager;
            coreBaseSettings = CoreBaseSettings;
            settingsManager = SettingsManager;
            userFormatter = UserFormatter;
            webItemSecurity = WebItemSecurity;
            userPhotoManager = UserPhotoManager;
        }
    }

    public class LdapOperationExtension
    {
        public static void Register(DIHelper services)
        {
            services.TryAdd<LdapOperationScope>();
            services.TryAdd<LdapOperation>();
            services.AddDistributedTaskQueueService<LdapOperation>(5);
        }
    }

}
