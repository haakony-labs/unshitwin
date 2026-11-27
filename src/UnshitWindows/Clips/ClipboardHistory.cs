namespace UnshitWindows.Clips;

/// <summary>Newest-first list of recent copies, capped at <see cref="Capacity"/>, without duplicates.</summary>
internal sealed class ClipboardHistory(int capacity)
{
    private readonly List<ClipEntry> _items = [];
    private int _capacity = Math.Max(1, capacity);

    public event EventHandler? Changed;

    public IReadOnlyList<ClipEntry> Items => _items;

    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = Math.Max(1, value);
            if (Trim()) OnChanged();
        }
    }

    /// <summary>Adds a copy at the top. If the same content is already in the list, that entry
    /// moves to the top instead. Returns the entry that ended up at the top.</summary>
    public ClipEntry Add(ClipEntry entry)
    {
        ClipEntry kept = entry;
        int existing = _items.FindIndex(e => e.HasSameContent(entry));
        if (existing >= 0)
        {
            kept = _items[existing];
            _items.RemoveAt(existing);
        }
        _items.Insert(0, kept);
        Trim();
        OnChanged();
        return kept;
    }

    public void MoveToTop(ClipEntry entry)
    {
        if (!_items.Remove(entry)) return;
        _items.Insert(0, entry);
        OnChanged();
    }

    public bool Remove(ClipEntry entry)
    {
        if (!_items.Remove(entry)) return false;
        OnChanged();
        return true;
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        OnChanged();
    }

    private bool Trim()
    {
        if (_items.Count <= _capacity) return false;
        _items.RemoveRange(_capacity, _items.Count - _capacity);
        return true;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
