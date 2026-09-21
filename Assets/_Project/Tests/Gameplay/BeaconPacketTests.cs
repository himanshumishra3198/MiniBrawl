using System.Text;
using MiniBrawl.Config;
using MiniBrawl.Networking.Discovery;
using NUnit.Framework;

namespace MiniBrawl.Gameplay.Tests
{
    /// <summary>
    /// The beacon is a wire format between separate devices and builds, so its parsing has to
    /// survive whatever else is on the network, not just its own output.
    /// </summary>
    public class BeaconPacketTests
    {
        [Test]
        public void RoundTrips()
        {
            BeaconPacket sent = BeaconPacket.Create("Galaxy S21", players: 3, gamePort: 7790);
            byte[] bytes = sent.ToBytes();

            Assert.IsTrue(BeaconPacket.TryParse(bytes, bytes.Length, out BeaconPacket received));
            Assert.AreEqual("Galaxy S21", received.HostName);
            Assert.AreEqual(3, received.Players);
            Assert.AreEqual(NetworkConstants.MaxPlayers, received.MaxPlayers);
            Assert.AreEqual(7790, received.GamePort, "the advertised port must be the one in use, not the default");
            Assert.IsTrue(received.IsCompatible);
        }

        [Test]
        public void RejectsForeignTraffic()
        {
            byte[] noise = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
            Assert.IsFalse(BeaconPacket.TryParse(noise, noise.Length, out _),
                "port 7771 is shared with whatever else is on the network");
        }

        [Test]
        public void RejectsTruncatedPacket()
        {
            byte[] bytes = BeaconPacket.Create("Host", 1, NetworkConstants.GamePort).ToBytes();
            Assert.IsFalse(BeaconPacket.TryParse(bytes, 6, out _), "a half-delivered packet is not valid");
        }

        [Test]
        public void RejectsEmptyAndOversized()
        {
            Assert.IsFalse(BeaconPacket.TryParse(null, 0, out _));
            Assert.IsFalse(BeaconPacket.TryParse(new byte[4096], 4096, out _));
        }

        [Test]
        public void LyingNameLength_DoesNotThrow()
        {
            // A hostile packet claiming a longer name than it carries must be rejected, not crash
            // the listener thread.
            byte[] bytes = BeaconPacket.Create("Host", 1, NetworkConstants.GamePort).ToBytes();
            bytes[10] = 200;

            Assert.IsFalse(BeaconPacket.TryParse(bytes, bytes.Length, out _));
        }

        [Test]
        public void OverlongNameIsTruncatedBeforeSending()
        {
            BeaconPacket packet = BeaconPacket.Create(new string('x', 100), 1, NetworkConstants.GamePort);
            Assert.AreEqual(BeaconPacket.MaxNameLength, packet.HostName.Length);
        }

        [Test]
        public void DifferentProtocolVersion_ParsesButIsIncompatible()
        {
            byte[] bytes = BeaconPacket.Create("Host", 1, NetworkConstants.GamePort).ToBytes();
            bytes[4] = (byte)(NetworkConstants.ProtocolVersion + 1);

            Assert.IsTrue(BeaconPacket.TryParse(bytes, bytes.Length, out BeaconPacket received),
                "an older build must be listed, so the player learns why they cannot join");
            Assert.IsFalse(received.IsCompatible);
        }
    }
}
