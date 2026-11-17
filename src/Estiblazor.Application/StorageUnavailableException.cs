namespace Estiblazor.Application;

public sealed class StorageUnavailableException(Exception? inner = null)
    : Exception(
        "Storage is temporarily unavailable. Nothing is confirmed saved. Reconnect and retry.",
        inner
    );
