using System.Threading.Channels;

namespace ToggleMesh.API.Infrastructure.Email;

public class EmailOutboxChannel
{
    private readonly Channel<byte> _channel;

    public EmailOutboxChannel()
    {
        var options = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<byte>(options);
    }

    public void Ping()
    {
        _channel.Writer.TryWrite(1);
    }

    public IAsyncEnumerable<byte> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken ct = default)
    {
        return _channel.Reader.WaitToReadAsync(ct);
    }
    
    public bool TryRead(out byte item)
    {
        return _channel.Reader.TryRead(out item);
    }
}
