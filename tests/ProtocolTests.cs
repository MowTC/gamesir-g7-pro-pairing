// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameSirPairing;

internal static class ProtocolTests
{
    private static int count;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        count++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Reject(Action operation, string name)
    {
        try { operation(); }
        catch (IOException) { Check(true, name); return; }
        catch (InvalidDataException) { Check(true, name); return; }
        throw new Exception("FAIL: expected rejection: " + name);
    }

    private sealed class FakeDevice : IPairingDevice
    {
        internal PairingAddresses State;
        internal int Writes;
        internal bool WrongAcknowledgment, WrongReadback, ThrowOnWrite;
        internal List<string> Trace;
        internal string Name;
        public PairingAddresses Read()
        {
            Trace.Add(Name + " read");
            return WrongReadback && Writes > 0 ? new PairingAddresses(new byte[6], new byte[6]) : State;
        }
        public PairingAddresses Write(PairingAddresses target)
        {
            Writes++;
            Trace.Add(Name + " write");
            if (ThrowOnWrite) throw new IOException("Simulated lost connection");
            State = target;
            return WrongAcknowledgment ? new PairingAddresses(new byte[6], new byte[6]) : target;
        }
        public void Dispose() { }
    }

    private static FakeDevice Device(string name, int controller, int receiver, List<string> trace)
    {
        return new FakeDevice
        {
            Name = name, Trace = trace,
            State = new PairingAddresses(Enumerable.Repeat((byte)controller, 6).ToArray(), Enumerable.Repeat((byte)receiver, 6).ToArray())
        };
    }

    private static void Main()
    {
        var response = new byte[] { 16, 66, 84, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        var parsed = PairingAddresses.Parse(response, 1);
        Check(PairingAddresses.Display(parsed.Controller) == "06-05-04-03-02-01", "Address display byte order");
        Check(parsed.WriteCommand().SequenceEqual(new byte[] { 15, 66, 84, 2, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }), "Exact write bytes");
        Reject(() => PairingAddresses.Parse(new byte[15], 1), "Truncated response rejected");
        Reject(() => PairingAddresses.Parse(response, 2), "Wrong opcode rejected");
        response[0] = 0;
        Reject(() => PairingAddresses.Parse(response, 1), "Wrong report ID rejected");
        Check(!PairingAddresses.IsValid(new byte[6]), "Zero address rejected");
        Check(!PairingAddresses.IsValid(Enumerable.Repeat((byte)255, 6).ToArray()), "FF address rejected");
        Reject(() => new PairingAddresses(new byte[5], new byte[6]), "Wrong address length rejected");
        var supported = new HidDeviceInfo { VendorId = 0x3537, ProductId = 0x5437, UsagePage = 0xFFF0, Usage = 0x40, InputLength = 64, OutputLength = 64 };
        Check(supported.IsPairingInterface, "Controller service interface recognized");
        supported.ProductId = 0x109B;
        Check(!supported.IsPairingInterface, "Gaming-mode PID rejected");
        supported.ProductId = 0x0575;
        supported.UsagePage = 1;
        Check(!supported.IsPairingInterface, "Receiver keyboard interface rejected");

        var trace = new List<string>();
        var receiver = Device("receiver", 1, 2, trace);
        var controller = Device("controller", 3, 4, trace);
        var result = PairingService.Pair(receiver, controller, (r, c) => { trace.Add("backup"); return "backup.txt"; }, message => { });
        Check(result.Addresses.Matches(new PairingAddresses(Enumerable.Repeat((byte)3, 6).ToArray(), Enumerable.Repeat((byte)2, 6).ToArray())), "Device self identities preserved");
        Check(trace.SequenceEqual(new[] { "receiver read", "controller read", "backup", "receiver write", "controller write", "receiver read", "controller read" }), "Backup and transaction ordering");
        Check(receiver.Writes == 1 && controller.Writes == 1, "Both devices written once");
        result = PairingService.Pair(receiver, controller, (r, c) => { throw new Exception("Unexpected backup"); }, message => { });
        Check(result.AlreadyPaired && receiver.Writes == 1 && controller.Writes == 1, "Already paired devices are not rewritten");

        receiver = Device("receiver", 1, 2, trace);
        controller = Device("controller", 3, 4, trace);
        Reject(() => PairingService.Pair(receiver, controller, (r, c) => { throw new IOException("Disk full"); }, message => { }), "Backup failure aborts transaction");
        Check(receiver.Writes == 0 && controller.Writes == 0, "No writes after backup failure");
        Reject(() => PairingService.Pair(receiver, controller, (r, c) => null, message => { }), "Missing backup path rejected");
        Check(receiver.Writes == 0 && controller.Writes == 0, "No writes without saved backup");
        receiver.WrongAcknowledgment = true;
        Reject(() => PairingService.Pair(receiver, controller, (r, c) => "backup.txt", message => { }), "Wrong acknowledgment rejected");
        Check(controller.Writes == 0, "Controller untouched after receiver acknowledgment mismatch");
        receiver = Device("receiver", 1, 2, trace);
        controller = Device("controller", 3, 4, trace);
        controller.ThrowOnWrite = true;
        Reject(() => PairingService.Pair(receiver, controller, (r, c) => "backup.txt", message => { }), "Partial transaction reported as failure");
        receiver = Device("receiver", 1, 2, trace);
        controller = Device("controller", 3, 4, trace);
        controller.WrongReadback = true;
        Reject(() => PairingService.Pair(receiver, controller, (r, c) => "backup.txt", message => { }), "Readback mismatch cannot report success");
        receiver = Device("receiver", 1, 0, trace);
        controller = Device("controller", 3, 4, trace);
        Reject(() => PairingService.Pair(receiver, controller, (r, c) => { throw new Exception("Unexpected backup"); }, message => { }), "Invalid self identity aborts before backup");
        Check(receiver.Writes == 0 && controller.Writes == 0, "Invalid identity causes no writes");
        Console.WriteLine(count + " checks passed.");
    }
}

