namespace CarbonGuard.Core.Detection;

internal sealed class RollingHistory(int capacity)
{
    private readonly Queue<decimal> _values = new(capacity);

    public int Count => _values.Count;

    public void Add(decimal value)
    {
        if (_values.Count == capacity)
        {
            _values.Dequeue();
        }

        _values.Enqueue(value);
    }

    public void Clear() => _values.Clear();

    public decimal[] Snapshot() => _values.ToArray();
}
