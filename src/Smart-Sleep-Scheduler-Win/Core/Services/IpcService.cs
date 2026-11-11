using System.IO;
using System.IO.Pipes;
using System.Text;

namespace SmartSleepScheduler.Core.Services;

/// <summary>
/// Lightweight, zero-firewall local Named Pipe IPC service
/// to forward command-line arguments to an already-running instance.
/// </summary>
public static class IpcService
{
    private const string PipeName = "SmartSleepScheduler_IPC_Pipe_9E2F9F24";

    public static bool TrySendToExistingInstance(string[] args)
    {
        if (args == null || args.Length == 0) return false;

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(350); // Fast 350ms timeout

            string payload = string.Join("\u001F", args);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);

            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void StartServer(Action<string[]> onArgumentsReceived, CancellationToken ct = default)
    {
        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct);

                    using var ms = new MemoryStream();
                    byte[] buffer = new byte[1024];
                    int bytesRead;

                    while ((bytesRead = await server.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                        if (bytesRead < buffer.Length) break;
                    }

                    string payload = Encoding.UTF8.GetString(ms.ToArray());
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        string[] args = payload.Split('\u001F', StringSplitOptions.RemoveEmptyEntries);
                        onArgumentsReceived(args);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Delay before retry in case of pipe reset
                    try { await Task.Delay(500, ct); } catch { break; }
                }
            }
        }, ct);
    }
}

