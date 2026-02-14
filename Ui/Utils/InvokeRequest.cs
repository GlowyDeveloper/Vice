using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Vice.Ui.Utils;

public class InvokeRequest
{
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private NamedPipeClientStream? _pipe;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task ConnectAsync()
    {
        await Task.Run(() =>
        {
            _pipe = new NamedPipeClientStream(".", "ViceUiPipe", PipeDirection.InOut, PipeOptions.Asynchronous);
            _pipe.Connect(5000);

            var noBomUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            _reader = new StreamReader(_pipe, noBomUtf8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            _writer = new StreamWriter(_pipe, noBomUtf8, 4096, leaveOpen: true) { AutoFlush = true };
        });

        Console.WriteLine("Connected to Rust IPC server.");
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

        var payload = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["cmd"] = cmd,
            ["args"] = args ?? new System.Collections.Generic.Dictionary<string, object?>(),
            ["respond"] = wait_for_response
        };

        var json = JsonConvert.SerializeObject(payload);

        await _lock.WaitAsync();
        try
        {
            if (_reader == null || _writer == null)
                throw new InvalidOperationException("Failed to establish IPC connection.");

            await _writer.WriteAsync(json);
            await _writer.WriteAsync('\n');
            await _writer.FlushAsync();

            if (!wait_for_response)
                return string.Empty;

            var readTask = _reader.ReadLineAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

            var completed = await Task.WhenAny(readTask, timeoutTask);
            if (completed == timeoutTask)
            {
                throw new TimeoutException("IPC server response timed out.");
            }

            var response = await readTask;
            return response ?? throw new IOException("IPC server disconnected.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IPC communication error: {ex}");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}