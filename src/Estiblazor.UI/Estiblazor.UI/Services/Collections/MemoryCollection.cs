using Estiblazor.UI.Services.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections;

namespace Estiblazor.UI.Services.Collections
{

    public abstract class MemoryCollection<TModel, TKey>(IMemoryCache memoryCache, IOptions<MemoryCollectionOptions<TModel>> options)
        : IMemoryCollection<TModel, TKey> where TModel : class
    {
        private readonly SynchronizedCollection<TKey> keys = [];

        /// <summary>
        /// Creating a nested type to ensure keys are unique for each set of type arguments.
        /// </summary>
        /// <param name="Key"></param>
        private record struct ModelKey(TKey Key) { }

        private readonly MemoryCollectionOptions<TModel> options = options.Value;

        #region Public

        public TModel? GetItem(TKey key)
        {
            return memoryCache.Get(new ModelKey(key)) as TModel;
        }

        public void SetItem(TKey key, TModel model)
        {
            var entry = memoryCache.CreateEntry(new ModelKey(key));
            InitCacheEntry(model, entry);
        }

        private void InitCacheEntry(TModel model, ICacheEntry entry)
        {
            entry.SetValue(model);
            entry.SetSlidingExpiration(options.RetentionTimespan);
            entry.RegisterPostEvictionCallback(OnEvict);
        }

        public void RemoveItem(TKey key)
        {
            memoryCache.Remove(new ModelKey(key));
        }

        #endregion

        protected TModel GetOrCreate(TKey key, Func<TModel> factory)
        {
            return memoryCache.GetOrCreate(new ModelKey(key),
                entry =>
                {
                    var value = factory();
                    InitCacheEntry(value, entry);
                    return value;
                })!;
        }

        private void OnEvict(object key, object? value, EvictionReason reason, object? state)
        {
            if (key is TKey tkey && value is TModel model)
            {
                OnItemRemoved(tkey, model);
            }
        }

        protected virtual void OnItemAdded(TKey key, TModel model)
        {
            keys.Add(key);
            ItemAdded?.Invoke(this, new MemoryCollectionChangedEventArgs<TModel, TKey>(model, key));
        }

        protected virtual void OnItemRemoved(TKey key, TModel model)
        {
            keys.Remove(key);
            ItemRemoved?.Invoke(this, new MemoryCollectionChangedEventArgs<TModel, TKey>(model, key));
        }

        public IEnumerable<TKey> GetKeys()
        {
            foreach(var key in keys)
            {
                yield return key;
            }
        }

        public IEnumerable<TModel> GetItems()
        {
            foreach (var key in keys)
            {
                var model = GetItem(key);
                if (model is not null)
                {
                    yield return model;
                }
            }
        }

        public event EventHandler<MemoryCollectionChangedEventArgs<TModel, TKey>>? ItemAdded;

        public event EventHandler<MemoryCollectionChangedEventArgs<TModel, TKey>>? ItemRemoved;
    }
}
