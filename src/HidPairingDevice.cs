// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;

namespace GameSirPairing
{
    public sealed class HidPairingDevice : IPairingDevice
    {
        private FileStream stream;
        private readonly HidDeviceInfo device;
        private readonly Action<string> log;

        public HidPairingDevice(HidDeviceInfo device, Action<string> log)
        {
            if (!device.IsPairingInterface) throw new InvalidOperationException("This is not a supported pairing interface.");
            this.device = device;
            this.log = log;
            var handle = NativeHid.CreateFile(device.Path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new System.ComponentModel.Win32Exception(error);
            }
            try { stream = new FileStream(handle, FileAccess.ReadWrite, device.InputLength, true); }
            catch { handle.Dispose(); throw; }
        }

        public PairingAddresses Read()
        {
            return Exchange(new byte[] { 0x0F, 0x42, 0x54, 0x01 }, 0x01);
        }

        public PairingAddresses Write(PairingAddresses addresses)
        {
            return Exchange(addresses.WriteCommand(), 0x02);
        }

        private PairingAddresses Exchange(byte[] command, byte operation)
        {
            if (stream == null) throw new ObjectDisposedException("HidPairingDevice");
            var input = new byte[device.InputLength];
            var output = new byte[device.OutputLength];
            Array.Copy(command, output, command.Length);
            var pendingRead = stream.BeginRead(input, 0, input.Length, null, null);
            try
            {
                log((device.IsReceiver ? "Receiver" : "Controller") + " TX " + BitConverter.ToString(output));
                NativeHid.Write(stream.SafeFileHandle, output);
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (true)
                {
                    int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                    using (var completion = pendingRead.AsyncWaitHandle)
                    {
                        if (remaining <= 0 || !completion.WaitOne(remaining))
                            throw new TimeoutException("No pairing reply arrived. Reconnect the device in USB pairing mode.");
                        int count = stream.EndRead(pendingRead);
                        if (count == 0) throw new IOException("The device disconnected during communication.");
                        log((device.IsReceiver ? "Receiver" : "Controller") + " RX " + BitConverter.ToString(input, 0, count));
                        if (count >= 16 && input[0] == 0x10 && input[1] == 0x42 && input[2] == 0x54 && input[3] == operation)
                            return PairingAddresses.Parse(input.Take(count).ToArray(), operation);
                    }
                    pendingRead = stream.BeginRead(input, 0, input.Length, null, null);
                }
            }
            catch
            {
                // Closing the handle cancels pending input. Never reuse a link after a timeout.
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }
    }
}
