namespace Estiblazor.UI.Services.Collections
{
#pragma warning disable S2326 // Unused type parameters should be removed
                              // The model type argument is essential for DI
    public class MemoryCollectionOptions<TModel>
#pragma warning restore S2326 // Unused type parameters should be removed
    {
        public TimeSpan RetentionTimespan { get; set; }
    }
}
