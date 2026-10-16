using System.Reflection;
using System.Runtime.Loader;

namespace RaTeX.Tests;

public class DeploymentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryParseAndLayout_WithMissingOrCorruptLibrary_Throws(bool corrupt)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "deployment-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "RaTeX.dll");
        File.Copy(typeof(Ratex).Assembly.Location, path);
        var libraryName = OperatingSystem.IsMacOS() ? "libratex_ffi.dylib" : "ratex_ffi.dll";
        var libraryPath = Path.Combine(directory, libraryName);
        if (corrupt)
        {
            File.WriteAllText(libraryPath, "not a native library");
        }

        var context = new IsolatedDeploymentContext(libraryPath);
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

    // Keep the test host's working native library out of these failure cases.
    private sealed class IsolatedDeploymentContext(string libraryPath) : AssemblyLoadContext(isCollectible: true)
    {
        /// <inheritdoc />
        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            return unmanagedDllName == "ratex_ffi" ? LoadUnmanagedDllFromPath(libraryPath) : 0;
        }
    }
}
