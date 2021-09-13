[![Build status](https://ci.appveyor.com/api/projects/status/l9b3ure2ihj4e5fl?svg=true)](https://ci.appveyor.com/project/joelweiss/entityframework-serilog)
[![NuGet Badge](https://buildstats.info/nuget/EntityFramework.Serilog?includePreReleases=true)](https://www.nuget.org/packages/EntityFramework.Serilog/)

# EntityFramework.Serilog

Use [Serilog](http://serilog.net/) to Log your EntityFramework connects, disconnects and SQLs.

# Installation
```powershell
PM> Install-Package EntityFramewok.Serilog
```
# Example
```csharp
using (var ctx = new TestContext())
{
	ctx.UseSerilog();
}
```