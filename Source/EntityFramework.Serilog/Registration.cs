using Serilog;
using System.Data.Entity.Infrastructure.Interception;

namespace EntityFramework.Serilog
{
    public static class Registration
    {
        public static void UseSerilog(ILogger? logger = null)
        {
            DbInterception.Add(new SerilogCommandInterceptor(logger));
        }
    }
}
