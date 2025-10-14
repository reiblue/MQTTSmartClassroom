using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MQTTSmartClassroom
{
    public static class UserSessionLauncher
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);


        [DllImport("kernel32.dll")] static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQueryUserToken(uint SessionId, out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes,
            int ImpersonationLevel, int TokenType, out IntPtr phNewToken);

        [DllImport("userenv.dll", SetLastError = true)]
        static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]


        static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;     // precisa ser "winsta0\\default" p/ UI
            public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        const uint TOKEN_ALL_ACCESS = 0xF01FF;
        const int SecurityImpersonation = 2;
        const int TokenPrimary = 1;
        const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        const uint NORMAL_PRIORITY_CLASS = 0x00000020;

        public static Process StartInActiveUserSession(string exePath, string args = "")
        {
            uint sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId == 0xFFFFFFFF) throw new InvalidOperationException("Nenhuma sessão ativa.");

            if (!WTSQueryUserToken(sessionId, out var hUser))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSQueryUserToken falhou");

            try
            {
                if (!DuplicateTokenEx(hUser, TOKEN_ALL_ACCESS, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out var hPrimary))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx falhou");

                try
                {
                    if (!CreateEnvironmentBlock(out var env, hPrimary, false))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateEnvironmentBlock falhou");

                    try
                    {
                        var si = new STARTUPINFO
                        {
                            cb = Marshal.SizeOf<STARTUPINFO>(),
                            lpDesktop = @"winsta0\default",
                            wShowWindow = 1,               // SW_SHOWNORMAL
                            dwFlags = 0x00000001           // STARTF_USESHOWWINDOW
                        };

                        if (!CreateProcessAsUser(
                            hPrimary,
                            exePath,
                            string.IsNullOrWhiteSpace(args) ? null : $"\"{exePath}\" {args}",
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            CREATE_UNICODE_ENVIRONMENT | NORMAL_PRIORITY_CLASS,
                            env,
                            null,
                            ref si,
                            out var pi))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessAsUser falhou");
                        }

                        try
                        {
                            return Process.GetProcessById(pi.dwProcessId);
                        }
                        finally
                        {
                            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                            if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                        }
                    }
                    finally { DestroyEnvironmentBlock(env); }
                }
                finally { CloseHandle(hPrimary); }
            }
            finally { CloseHandle(hUser); }
        }
    }
}





