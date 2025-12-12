using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
namespace SecureMessenger.Services;

public sealed class MessengerService : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    private readonly ConcurrentBag<TcpClient> _clients = new();

    public event Action<string>? MessageReceived;
    public event Action<string>? StatusChanged;

    public bool IsListening => _listener is not null;

    public async Task StartAsync(int port, string sharedKey)
    {
        Stop();

        _listenerCts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        StatusChanged?.Invoke($"Сервер слушает порт {port}");
        _ = Task.Run(() => ListenAsync(sharedKey, _listenerCts.Token));
    }

    public void Stop()
    {
        try
        {
            _listenerCts?.Cancel();
            _listener?.Stop();
            foreach (var client in _clients)
            {
                client.Dispose();
            }
        }
        finally
        {
            _listenerCts = null;
            _listener = null;
            StatusChanged?.Invoke("Сервер остановлен");
        }
    }

    public async Task SendAsync(string host, int port, string sharedKey, string message)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port);
        await using var stream = client.GetStream();

        var payload = EncryptionService.Encrypt(sharedKey, message);
        var lengthPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));

        await stream.WriteAsync(lengthPrefix);
        await stream.WriteAsync(payload);

        StatusChanged?.Invoke($"Отправлено сообщение на {host}:{port}");
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task ListenAsync(string sharedKey, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _listener is not null)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _clients.Add(client);
                _ = Task.Run(() => ReadClientAsync(client, sharedKey, cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            // stopping listener
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Ошибка сервера: {ex.Message}");
        }
    }

    private async Task ReadClientAsync(TcpClient client, string sharedKey, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = client.GetStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                var lengthBuffer = await ReadExactAsync(stream, sizeof(int), cancellationToken);
                var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer));
                if (length <= 0)
                {
                    return;
                }

                var payload = await ReadExactAsync(stream, length, cancellationToken);
                var message = EncryptionService.Decrypt(sharedKey, payload);
                MessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Ошибка клиента: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("Соединение закрыто");
            }

            offset += read;
        }

        return buffer;
    }
}
