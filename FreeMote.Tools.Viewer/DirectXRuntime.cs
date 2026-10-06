using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace FreeMote.Tools.Viewer
{
    // Keep the runtime check independent of FreeMote.NET
    internal static class DirectXRuntime
    {
        private const string LibraryName = "d3dx9_43.dll";

        internal static bool TryLoad(out string error)
        {
            // The renderer also supports a runtime deployed next to the driver in lib.
            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", LibraryName);
            var isLocal = File.Exists(localPath);
            var module = LoadLibraryExW(isLocal ? localPath : LibraryName, IntPtr.Zero,
                isLocal ? 0x8u /* LOAD_WITH_ALTERED_SEARCH_PATH */ : 0u);
            if (module != IntPtr.Zero)
            {
                FreeLibrary(module);
                error = null;
                return true;
            }

            var code = Marshal.GetLastWin32Error();
            error = "FreeMote Viewer could not load " + LibraryName + ".\r\n\r\n"
                + "Install the Microsoft DirectX End-User Runtimes (June 2010).\r\n"
                + "After extracting the package, run DXSETUP.exe, then restart FreeMote Viewer.\r\n"
                + "DirectX 11/12 alone does not include this legacy DirectX 9 component.\r\n\r\n"
                + "https://www.microsoft.com/en-us/download/details.aspx?id=8109\r\n\r\n"
                + "Required architecture: " + (Environment.Is64BitProcess ? "x64" : "x86") + ".\r\n"
                + "Win32 error " + code + ": " + new Win32Exception(code).Message;
            return false;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);
    }
}
