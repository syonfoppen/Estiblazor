namespace Estiblazor.UI.Services.Collections
{
    public class MemoryCollectionChangedEventArgs<TModel, TKey>(TModel item, TKey key) : EventArgs
        where TModel : class
    {
        public TModel Item { get; } = item;
        public TKey Key { get; } = key;
    }
}
