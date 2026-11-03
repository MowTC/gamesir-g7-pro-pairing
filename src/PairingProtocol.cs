// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;

namespace GameSirPairing
{
    // Byte order here is the order used on the USB wire. Only display reverses it.
    public sealed class PairingAddresses
    {
        public byte[] Controller { get; private set; }
        public byte[] Receiver { get; private set; }

        public PairingAddresses(byte[] controller, byte[] receiver)
        {
            if (controller == null || receiver == null || controller.Length != 6 || receiver.Length != 6)
                throw new InvalidDataException("Each pairing address must contain six bytes.");
            Controller = (byte[])controller.Clone();
            Receiver = (byte[])receiver.Clone();
        }

        public static bool IsValid(byte[] address)
        {
            return address != null && address.Length == 6 &&
                !address.All(value => value == 0) && !address.All(value => value == 255);
        }

        public static PairingAddresses Parse(byte[] report, byte operation)
        {
            if (report == null || report.Length < 16 || report[0] != 0x10 ||
                report[1] != 0x42 || report[2] != 0x54 || report[3] != operation)
                throw new InvalidDataException("The device returned an invalid pairing report.");
            return new PairingAddresses(report.Skip(4).Take(6).ToArray(), report.Skip(10).Take(6).ToArray());
        }

        public byte[] WriteCommand()
        {
            if (!IsValid(Controller) || !IsValid(Receiver))
                throw new InvalidDataException("A device returned an empty or invalid identity. No write was sent.");
            return new byte[] { 0x0F, 0x42, 0x54, 0x02 }.Concat(Controller).Concat(Receiver).ToArray();
        }

        public bool Matches(PairingAddresses other)
        {
            return other != null && Controller.SequenceEqual(other.Controller) && Receiver.SequenceEqual(other.Receiver);
        }

        public static string Display(byte[] address)
        {
            return BitConverter.ToString(address.Reverse().ToArray());
        }

        public override string ToString()
        {
            return "Controller=" + Display(Controller) + " Receiver=" + Display(Receiver);
        }
    }

    public interface IPairingDevice : IDisposable
    {
        PairingAddresses Read();
        PairingAddresses Write(PairingAddresses addresses);
    }

    public sealed class PairingResult
    {
        public PairingAddresses Addresses { get; internal set; }
        public string BackupPath { get; internal set; }
        public bool AlreadyPaired { get; internal set; }
    }

    public static class PairingService
    {
        // The backup callback must finish successfully before either write can occur.
        // A device may retain a write even if its acknowledgment is lost, so failures
        // after this point must be reported as potentially partial transactions.
        public static PairingResult Pair(IPairingDevice receiver, IPairingDevice controller,
            Func<PairingAddresses, PairingAddresses, string> saveBackup, Action<string> log)
        {
            var receiverBefore = receiver.Read();
            var controllerBefore = controller.Read();
            var target = new PairingAddresses(controllerBefore.Controller, receiverBefore.Receiver);
            target.WriteCommand(); // Validate both self addresses before saving or writing.

            if (receiverBefore.Matches(target) && controllerBefore.Matches(target))
                return new PairingResult { Addresses = target, AlreadyPaired = true };

            string backup = saveBackup(receiverBefore, controllerBefore);
            if (String.IsNullOrWhiteSpace(backup))
                throw new IOException("A backup was not saved. No pairing write was sent.");
            log("Original addresses backed up to " + backup);

            try
            {
                log("Updating receiver pairing address...");
                Verify(receiver.Write(target), target);
                log("Updating controller pairing address...");
                Verify(controller.Write(target), target);
                log("Reading both devices again to verify the new pairing...");
                Verify(receiver.Read(), target);
                Verify(controller.Read(), target);
                return new PairingResult { Addresses = target, BackupPath = backup };
            }
            catch (Exception error)
            {
                throw new IOException("Pairing was not fully verified. A device may have changed. " +
                    "Keep the backup: " + backup + Environment.NewLine + error.Message, error);
            }
        }

        private static void Verify(PairingAddresses actual, PairingAddresses expected)
        {
            if (!expected.Matches(actual))
                throw new InvalidDataException("The returned pairing addresses did not match the requested addresses.");
        }
    }
}
