FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src ./src
RUN dotnet publish src/Estiblazor.UI/Estiblazor.UI/Estiblazor.UI.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN mkdir -p /data/keys && chown -R app:app /data
COPY --from=build /out .
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Estiblazor.UI.dll"]
