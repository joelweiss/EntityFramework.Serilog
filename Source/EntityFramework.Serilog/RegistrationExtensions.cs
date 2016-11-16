using EntityFramework.Serilog;
using Serilog;
using System.Data.Entity.Infrastructure.Interception;

namespace System.Data.Entity
{
    public static class RegistrationExtensions
    {
        public static IDisposable UseSerilog(this DbContext context, ILogger logger = null)
        {
            var interceptor = new SerilogCommandInterceptor(context, logger);
            DbInterception.Add(interceptor);
            return interceptor;
        }
    }
}
