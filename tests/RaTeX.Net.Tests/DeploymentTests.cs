using System.Reflection;
using System.Runtime.Loader;

namespace RaTeX.Net.Tests;

public class DeploymentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryParseAndLayout_WithMissingOrCorruptLibrary_Throws(bool corrupt)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "deployment-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "RaTeX.Net.dll");
        File.Copy(typeof(Ratex).Assembly.Location, path);
        if (corrupt)
        {
            File.WriteAllText(Path.Combine(directory, "ratex_ffi.dll"), "not a native DLL");
        }

        var context = new AssemblyLoadContext("isolated-deployment-" + Guid.NewGuid(), isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(path);
            var method = assembly.GetType("RaTeX.Ratex")!.GetMethod("TryParseAndLayout")!;
            var failure = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, ["x", null, null, null]));
            if (corrupt)
            {
                Assert.IsType<BadImageFormatException>(failure.InnerException);
            }
            else
            {
                Assert.IsType<DllNotFoundException>(failure.InnerException);
            }
        }
        finally
        {
            context.Unload();
        }
    }
}
