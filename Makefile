.PHONY: setup build test clean restore

setup:
	dotnet restore

build:
	dotnet build --no-restore

test:
	dotnet test --no-build --verbosity normal

clean:
	dotnet clean

restore:
	dotnet restore
