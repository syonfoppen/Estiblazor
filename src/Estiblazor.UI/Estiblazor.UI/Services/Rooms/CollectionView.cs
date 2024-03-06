using System.Collections;
using System.Collections.Specialized;

namespace Estiblazor.UI.Services.Rooms
{
    public class CollectionView<T> : IDisposable
    {
        public event EventHandler? CollectionHasChanged;

        private readonly INotifyCollectionChanged notifyCollectionChanged;
        private readonly Predicate<T> filter;
        private bool disposedValue;

        public CollectionView(INotifyCollectionChanged notifyCollectionChanged, Predicate<T> filter)
        {
            notifyCollectionChanged.CollectionChanged += NotifyCollectionChanged_CollectionChanged;
            this.notifyCollectionChanged = notifyCollectionChanged;
            this.filter = filter;
        }

        public IEnumerable<T> Items => (notifyCollectionChanged as IEnumerable)!.OfType<T>();

        private void NotifyCollectionChanged_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if(e.Action == NotifyCollectionChangedAction.Reset)
            {
                OnCollectionChange();
            }
            else if(e.NewItems is { } newItems && newItems.OfType<T>().Any(x => filter(x)))
            {
                OnCollectionChange();
            }
            else if (e.OldItems is { } oldItems && oldItems.OfType<T>().Any(x => filter(x)))
            {
                OnCollectionChange();
            }
        }

        private void OnCollectionChange()
        {
            CollectionHasChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                notifyCollectionChanged.CollectionChanged -= NotifyCollectionChanged_CollectionChanged;
                disposedValue = true;
            }
        }

        ~CollectionView()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
