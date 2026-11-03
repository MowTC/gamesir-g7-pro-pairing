// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace GameSirPairing
{
    public sealed class HidDeviceInfo
    {
        public string Path { get; internal set; }
        public string Product { get; internal set; }
        public ushort VendorId, ProductId, UsagePage, Usage, InputLength, OutputLength;

        public bool IsReceiver
        {
            get { return (VendorId == 0x3537 || VendorId == 0x2563) && ProductId == 0x0575; }
        }

        public bool IsPairingInterface
        {
            get
            {
                bool knownController = (VendorId == 0x3537 && ProductId == 0x5437) ||
                    ((VendorId == 0x2563 || VendorId == 0x0283) && ProductId == 0x028E);
                return UsagePage == 0xFFF0 && Usage == 0x0040 && InputLength == 64 &&
                    OutputLength == 64 && (IsReceiver || knownController);
            }
        }

        public string Label
        {
            get { return String.Format("{0}  ·  {1:X4}:{2:X4}", Product, VendorId, ProductId); }
        }

        public override string ToString()
        {
            return String.Format("{0:X4}:{1:X4} page={2:X4} usage={3:X4} in={4} out={5} {6}",
                VendorId, ProductId, UsagePage, Usage, InputLength, OutputLength, Product);
        }
    }

    internal static class NativeHid
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInterface
        {
            public int Size;
            public Guid ClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Attributes
        {
            public int Size;
            public ushort VendorId, ProductId, Version;
        }

        [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid,
            uint index, ref DeviceInterface data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DeviceInterface data,
            IntPtr buffer, int length, out int required, IntPtr device);
        [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
            uint disposition, uint flags, IntPtr template);
        [DllImport("hid.dll")] private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes data);
        [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
        [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, IntPtr caps);
        [DllImport("hid.dll")] private static extern bool HidD_GetProductString(SafeFileHandle handle, byte[] buffer, int length);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle handle, IntPtr buffer, int length, IntPtr sent, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetOverlappedResult(SafeFileHandle handle, IntPtr overlapped, out int count, bool wait);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);

        internal static List<HidDeviceInfo> Enumerate()
        {
            var devices = new List<HidDeviceInfo>();
            Guid guid;
            HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
            if (set == new IntPtr(-1)) throw new Win32Exception();
            try
            {
                for (uint index = 0; ; index++)
                {
                    var data = new DeviceInterface();
                    data.Size = Marshal.SizeOf(data);
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref data))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == 259) break; // ERROR_NO_MORE_ITEMS
                        throw new Win32Exception(error);
                    }
                    int required;
                    SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    if (required < 6) continue;
                    IntPtr detail = Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        using (var handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                        {
                            if (handle.IsInvalid) continue;
                            var attributes = new Attributes();
                            attributes.Size = Marshal.SizeOf(attributes);
                            if (!HidD_GetAttributes(handle, ref attributes)) continue;
                            // Do not inspect or open unrelated vendors for pairing.
                            if (attributes.VendorId != 0x3537 && attributes.VendorId != 0x2563 && attributes.VendorId != 0x0283) continue;
                            IntPtr preparsed;
                            if (!HidD_GetPreparsedData(handle, out preparsed)) continue;
                            IntPtr caps = Marshal.AllocHGlobal(64);
                            try
                            {
                                if (HidP_GetCaps(preparsed, caps) != 0x110000) continue;
                                var product = new byte[256];
                                HidD_GetProductString(handle, product, product.Length);
                                devices.Add(new HidDeviceInfo
                                {
                                    Path = path,
                                    Product = Encoding.Unicode.GetString(product).TrimEnd('\0'),
                                    VendorId = attributes.VendorId,
                                    ProductId = attributes.ProductId,
                                    Usage = (ushort)Marshal.ReadInt16(caps, 0),
                                    UsagePage = (ushort)Marshal.ReadInt16(caps, 2),
                                    InputLength = (ushort)Marshal.ReadInt16(caps, 4),
                                    OutputLength = (ushort)Marshal.ReadInt16(caps, 6)
                                });
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(caps);
                                HidD_FreePreparsedData(preparsed);
                            }
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return devices;
        }

        // Use the same handle as the pending read. A separate FileStream writer
        // serialized behind the pending read during hardware testing.
        internal static void Write(SafeFileHandle handle, byte[] report)
        {
            using (var completed = new EventWaitHandle(false, EventResetMode.ManualReset))
            {
                int size = IntPtr.Size == 8 ? 32 : 20;
                IntPtr overlapped = Marshal.AllocHGlobal(size);
                IntPtr buffer = Marshal.AllocHGlobal(report.Length);
                try
                {
                    for (int offset = 0; offset < size; offset++) Marshal.WriteByte(overlapped, offset, 0);
                    Marshal.WriteIntPtr(overlapped, IntPtr.Size == 8 ? 24 : 16,
                        completed.SafeWaitHandle.DangerousGetHandle());
                    Marshal.Copy(report, 0, buffer, report.Length);
                    if (!WriteFile(handle, buffer, report.Length, IntPtr.Zero, overlapped))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != 997) throw new Win32Exception(error); // ERROR_IO_PENDING
                        if (!completed.WaitOne(2000))
                        {
                            CancelIoEx(handle, overlapped);
                            int ignored;
                            GetOverlappedResult(handle, overlapped, out ignored, true);
                            throw new TimeoutException("The USB write timed out. Reconnect the device and try again.");
                        }
                    }
                    int sent;
                    if (!GetOverlappedResult(handle, overlapped, out sent, true)) throw new Win32Exception();
                    if (sent != report.Length) throw new IOException("The device accepted an incomplete USB report.");
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                    Marshal.FreeHGlobal(overlapped);
                }
            }
        }
    }
}
