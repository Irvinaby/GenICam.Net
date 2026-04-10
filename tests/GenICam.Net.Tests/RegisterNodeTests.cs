using GenICam.Net.GenApi;

namespace GenICam.Net.Tests;

[TestFixture]
public class RegisterNodeTests
{
    [Test]
    public void Get_WithoutPort_ReturnsCache()
    {
        var node = new RegisterNode { Address = 0x100, Length = 4 };
        node.SetCacheDirect(new byte[] { 1, 2, 3, 4 });

        var data = node.Get(4);

        Assert.That(data, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
    }

    [Test]
    public void Set_WithoutPort_UpdatesCache()
    {
        var node = new RegisterNode { Address = 0x100, Length = 4 };

        node.Set(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });
        var data = node.Get(4);

        Assert.That(data, Is.EqualTo(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }));
    }

    [Test]
    public void Get_WithPort_ReadsFromPort()
    {
        var port = new FakePort();
        port.Memory[0x100] = new byte[] { 10, 20, 30, 40 };

        var node = new RegisterNode { Address = 0x100, Length = 4 };
        node.Port = port;

        var data = node.Get(4);

        Assert.That(data, Is.EqualTo(new byte[] { 10, 20, 30, 40 }));
    }

    [Test]
    public void Set_WithPort_WritesToPort()
    {
        var port = new FakePort();
        var node = new RegisterNode { Address = 0x200, Length = 2 };
        node.Port = port;

        node.Set(new byte[] { 0x01, 0x02 });

        Assert.That(port.Memory.ContainsKey(0x200), Is.True);
        Assert.That(port.Memory[0x200], Is.EqualTo(new byte[] { 0x01, 0x02 }));
    }

    private class FakePort : IPort
    {
        public Dictionary<long, byte[]> Memory { get; } = new();

        public byte[] Read(long address, long length)
        {
            return Memory.TryGetValue(address, out var data) ? data : new byte[length];
        }

        public void Write(long address, byte[] data)
        {
            Memory[address] = data;
        }
    }
}
