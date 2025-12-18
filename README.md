# Estiblazor

Estiblazor is a online tool focused on delivering a versatile tool primarily designed for facilitating planning poker sessions, among other scenarios requiring collaborative input from multiple team members. |
The tool is already deployed and accessible live via [this url](https://app-syon-estiblazor.delightfulbay-3a864408.westeurope.azurecontainerapps.io/) and is also available on [Docker Hub](https://hub.docker.com/r/syonfoppen02/estiblazorui) for easy deployment. 

Users can launch it using the following docker command:
`docker run -p 8080:8080 -d syonfoppen02/estiblazorui`

## Local development with .NET Aspire

The repository includes an Aspire AppHost to run the Blazor Server app with a managed Redis instance for local testing. After installing the .NET 8 SDK with Aspire workloads, start everything with:

```
dotnet run --project src/Estiblazor.AppHost/Estiblazor.AppHost.csproj
```

The AppHost provisions Redis automatically and wires the `ConnectionStrings:Redis` value for the UI project so you can run the app statelessly without manual setup.
