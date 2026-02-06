using System.Collections.Generic;

namespace Win32Metadata.Core.Models;

public record Win32Parameter(string Name, string Type, bool IsPointer, bool IsOptional);

public record Win32Function(string Name, string Dll, string ReturnType, List<Win32Parameter> Parameters);

public record TypeDefinition(string Kind, object Data);