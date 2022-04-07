using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewsDistribution;

public class ObservableHashSet<T> : INotifyCollectionChanged, ISet<T>, ICollection<T>, ICollection, IEnumerable<T>, IEnumerable
{
    private readonly HashSet<T> _set = new();

    #region INotifyCollectionChanged
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    #endregion

    #region ISet<T>
    public bool Add(T value)
    {
        var added = _set.Add(value);

        if (added)
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value));

        return added;
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        var oldCount = _set.Count;

        _set.ExceptWith(other);

        if (_set.Count != oldCount)
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        var oldCount = _set.Count;

        _set.IntersectWith(other);

        if (_set.Count != oldCount)
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        return _set.IsProperSubsetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        return _set.IsProperSupersetOf(other);
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        return _set.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        return _set.IsSupersetOf(other);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        return _set.Overlaps(other);
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        return _set.SetEquals(other);
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        var oldCount = _set.Count;

        _set.SymmetricExceptWith(other);

        if (_set.Count != oldCount)
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void UnionWith(IEnumerable<T> other)
    {
        var oldCount = _set.Count;

        _set.UnionWith(other);

        if (_set.Count != oldCount)
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
    #endregion

    #region ICollection<T>
    void ICollection<T>.Add(T value)
    {
        var added = _set.Add(value);

        if (added)
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value));
    }

    public bool Remove(T value)
    {
        var removed = _set.Remove(value);

        if (removed)
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        return removed;
    }

    public void Clear()
    {
        _set.Clear();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public bool Contains(T value)
    {
        return _set.Contains(value);
    }

    public void CopyTo(T[] array)
    {
        _set.CopyTo(array);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _set.CopyTo(array, arrayIndex);
    }

    public void CopyTo(T[] array, int arrayIndex, int arrayOffset)
    {
        _set.CopyTo(array, arrayIndex, arrayOffset);
    }

    public int Count => _set.Count;

    public bool IsReadOnly => false;
    #endregion

    #region ICollection
    public void CopyTo(Array array, int index)
    {
        _set.CopyTo((T[])array, index);
    }

    public bool IsSynchronized => true;

    public object SyncRoot => this;
    #endregion

    #region IEnumerable<T>
    public IEnumerator<T> GetEnumerator()
    {
        return _set.GetEnumerator();
    }
    #endregion

    #region IEnumerable
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    #endregion
}
