
namespace Estiblazor.UI.Services.Collections
{
    public interface IMemoryCollection<TModel, TKey> 
        where TModel : class
    {
        event EventHandler<MemoryCollectionChangedEventArgs<TModel, TKey>>? ItemAdded;
        event EventHandler<MemoryCollectionChangedEventArgs<TModel, TKey>>? ItemRemoved;

        TModel? GetItem(TKey key);
        void RemoveItem(TKey key);
        void SetItem(TKey key, TModel model);

        IEnumerable<TKey> GetKeys();
        IEnumerable<TModel> GetItems();

    }
}