// SPDX-License-Identifier: MIT
// Optional hardware check. Sends only read-address requests; never pairs devices.
using System;
using System.Linq;
using GameSirPairing;

internal static class HardwareReadCheck
{
    private static int Main()
    {
        try
        {
            int found = 0;
            foreach (var device in NativeHid.Enumerate().Where(device => device.IsPairingInterface))
            {
                found++;
                Console.WriteLine(device);
                using (var link = new HidPairingDevice(device, Console.WriteLine))
                    Console.WriteLine(link.Read());
            }
            Console.WriteLine(found + " supported pairing interface(s) read. No pairing writes sent.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
