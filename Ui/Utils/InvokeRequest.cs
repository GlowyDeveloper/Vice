using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Vice.Ui.Utils;

public class InvokeRequest
{
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private TcpClient? _client;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task ConnectAsync()
    {
        _client = new TcpClient();

        await _client.ConnectAsync("127.0.0.1", 8423);

        var stream = _client.GetStream();

        var noBomUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        _reader = new StreamReader(stream, noBomUtf8, false, 4096, leaveOpen: true);
        _writer = new StreamWriter(stream, noBomUtf8, 4096, leaveOpen: true)
        {
            AutoFlush = true
        };

        Console.WriteLine("Connected to Rust TCP server.");
    }

    public async Task<string> SendRequestAsync(string cmd, object? args = null, bool wait_for_response = true)
    {
        if (_reader == null || _writer == null)
        {
            try
            {
                await ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IPC connect failed: {ex}");
                throw;
            }
        }

        var payload = new Dictionary<string, object?>
        {
            ["cmd"] = cmd,
            ["args"] = args ?? new Dictionary<string, object?>(),
            ["respond"] = wait_for_response
        };

        var json = JsonSerializer.Serialize(payload, JsonContext.Default.DictionaryStringObject);
        var message = json + "\n";

        await _lock.WaitAsync();
        try
        {
            if (_reader == null || _writer == null)
                throw new InvalidOperationException("Failed to establish connection.");

            await _writer.WriteAsync(message);
            await _writer.FlushAsync();

            if (!wait_for_response)
                return string.Empty;

            var readTask = _reader.ReadLineAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

            var completed = await Task.WhenAny(readTask, timeoutTask);
            if (completed == timeoutTask)
            {
                throw new TimeoutException("Server response timed out.");
            }

            var response = await readTask;
            return response ?? throw new IOException("Server disconnected.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TCP communication error: {ex}");

            _reader = null;
            _writer = null;
            _client?.Close();
            _client = null;

            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}