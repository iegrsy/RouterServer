DATE=$(shell date +'%Y%m%d%H%M%S')
ARCHIVE=release_$(DATE).tar.gz

APP_FILE=RouterServer.csproj
GRPC_VERSION=1.16.0

all:
	dotnet restore $(APP_FILE)
	$(HOME)/.nuget/packages/grpc.tools/$(GRPC_VERSION)/tools/linux_x64/protoc -I ./proto --csharp_out ./ --grpc_out ./ --plugin=protoc-gen-grpc=$(HOME)/.nuget/packages/grpc.tools/$(GRPC_VERSION)/tools/linux_x64/grpc_csharp_plugin proto/*.proto
	dotnet restore $(APP_FILE)
	dotnet build -c Release $(APP_FILE)
clean:
	dotnet clean $(APP_FILE)
	rm -rf Nvr.cs NvrGrpc.cs *.tar.gz
	rm -rf bin obj
release: all
	dotnet publish -c Release -r linux-x64 $(APP_FILE)
	dotnet publish -c Release -r win-x64 $(APP_FILE)
	dotnet pack -c Release $(APP_FILE)
	tar -cvzf $(ARCHIVE) ./bin/Release
