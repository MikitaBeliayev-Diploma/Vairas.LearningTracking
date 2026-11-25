FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY Vairas.LearningTracking.Domain/ Vairas.LearningTracking.Domain/
COPY Vairas.LearningTracking.Business/ Vairas.LearningTracking.Business/
COPY Vairas.LearningTracking.Persistance/ Vairas.LearningTracking.Persistance/
COPY Vairas.LearningTracking/ Vairas.LearningTracking/
RUN dotnet publish Vairas.LearningTracking/Vairas.LearningTracking.csproj -c Release -o /out --no-self-contained
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vairas.LearningTracking.dll"]
