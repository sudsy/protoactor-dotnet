FROM microsoft/aspnetcore-build:2.0 

RUN mkdir /src
ADD Node1.csproj /src
WORKDIR /src
RUN dotnet restore
ADD . /src
RUN dotnet build -o /app

WORKDIR /app
EXPOSE 12001
ENTRYPOINT [ "dotnet", "Node1.dll" ]