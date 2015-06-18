using EntityFramework.Serilog;
using Serilog;
using System.Data.Entity.Infrastructure.Interception;

namespace System.Data.Entity
{
    public static class RegistrationExtensions
    {
        public static void UseSerilog(this DbContext context, ILogger logger = null)
        {
            DbInterception.Add(new SerilogCommandInterceptor(context, logger));
        }
    }
}
