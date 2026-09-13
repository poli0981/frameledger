using FrameLedger.Application.Ipc;

namespace FrameLedger.Application.Tests.Ipc;

/// <summary>A publisher that remembers what it was handed, so a test reads the events off it in order.</summary>
internal sealed class RecordingPublisher : IIpcEventPublisher
{
    public bool HasClients { get; set; } = true;

    public List<(string Type, object Payload)> Published { get; } = [];

    public void Publish<T>(string type, T payload)
        where T : class => Published.Add((type, payload));

    public IEnumerable<T> Of<T>()
        where T : class => Published.Select(static p => p.Payload).OfType<T>();
}
