using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ASC.ActiveDirectory.ComplexOperations;
using ASC.Common;
using ASC.Common.Threading;
using ASC.Core;

namespace ASC.ActiveDirectory.ComplexOperations
{
    [Scope]
    internal class LdapOperationScope
    {
        private TenantManager TenantManager { get; }
        private UserManager UserManager { get; }
        private CoreBaseSettings CoreBaseSettings { get; }

        public LdapOperationScope(TenantManager tenantManager,
            UserManager userManager,
            CoreBaseSettings coreBaseSettings)
        {
            TenantManager = tenantManager;
            UserManager = userManager;
            CoreBaseSettings = coreBaseSettings;
        }

        public void Deconstruct(out TenantManager tenantManager,
            out UserManager userManager,
            out CoreBaseSettings coreBaseSettings)
        {
            tenantManager = TenantManager;
            userManager = UserManager;
            coreBaseSettings = CoreBaseSettings;
        }
    }

    public class LdapOperationExtension
    {
        public static void Register(DIHelper services)
        {
            services.TryAdd<LdapOperationScope>();
            //services.AddDistributedTaskQueueService<LdapOperation>(5);
        }
    }

}
