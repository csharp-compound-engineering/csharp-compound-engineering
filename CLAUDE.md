# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A C# implementation of the "compound-engineering" paradigm. This project is currently in the initial scaffolding phase with no source code yet.

## Build Commands

Once the project structure is created, standard .NET CLI commands will apply:

```bash
dotnet build              # Build the solution
dotnet test               # Run all tests
dotnet run                # Run the main project
dotnet test --filter "FullyQualifiedName~TestName"  # Run a specific test
```

## Project Status

This is a new repository. When adding source code:
- Create a solution file (*.sln) at the root
- Use standard .NET project structure with src/ and tests/ directories
- The .gitignore is configured for Visual Studio and .NET development
